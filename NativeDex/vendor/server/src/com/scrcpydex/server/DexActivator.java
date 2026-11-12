// DX Manager modifications (2026-10-26): session ownership, cancellation and bounded resource cleanup.
// Derived from ScrcpyDeX v1.1, Apache-2.0; see NativeDex/upstream.json and vendor/LICENSE.
package com.scrcpydex.server;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.lang.reflect.InvocationHandler;
import java.lang.reflect.Method;
import java.lang.reflect.Proxy;
import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.net.InetSocketAddress;
import java.net.SocketTimeoutException;
import java.util.List;
import java.util.concurrent.CopyOnWriteArrayList;
import io.github.mazemei.dxmnativedex.RtspMessageReader;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;

import com.scrcpydex.server.wrappers.DisplayManager;

/**
 * Samsung DeX Activator via Loopback (127.0.0.1).
 * 
 * This module executes the core interoperability mechanism of this project:
 * instructs Samsung's WiFi Display subsystem to connect to the device itself
 * on localhost, enabling the FLAG_WIRELESS_DEX_DISPLAY (0x4000000)
 * and FLAG_EXTERNAL_DEX_HOSTING (0x20000) flags in system_server without root and without Wi-Fi.
 */
public class DexActivator {
    private static final int RTSP_PORT = 7236;
    private final String displayName;
    private final String peerAddress;
    private final List<DatagramSocket> udpSockets = new CopyOnWriteArrayList<>();

    private final DisplayManager displayManager;
    private final AtomicBoolean running = new AtomicBoolean(false);
    private final CountDownLatch rtspPlayCompleted = new CountDownLatch(1);

    private int rtpVideoPort;
    private int rtpAudioPort;
    private Socket rtspSocket;
    public static final String DEFAULT_VIDEO_FORMATS="40 00 01 10 0001bdeb 1fffffff 00003fff 10 0000 001f 11 0780 0438, 02 10 0001bdeb 1fffffff 00000fff 10 0000 001f 11 0780 0438";
    private final String videoFormats;

    public DexActivator(DisplayManager displayManager, String displayName, String peerAddress) {
        this(displayManager,displayName,peerAddress,null);
    }
    public DexActivator(DisplayManager displayManager,String displayName,String peerAddress,String videoFormats) {
        this.videoFormats=videoFormats==null?DEFAULT_VIDEO_FORMATS:videoFormats;
        this.displayManager = displayManager;
        this.displayName = displayName;
        this.peerAddress = peerAddress;
    }

    public boolean isRunning() { return running.get(); }

    /**
     * Initiates Samsung DeX activation.
     * 
     * @return true if RTSP handshake completed with PLAY OK
     * @throws Exception if an unrecoverable failure occurs
     */
    public boolean activate() throws Exception {
        Ln.i("Starting Samsung DeX activation via local loopback...");

        // 1. Refuse existing sessions; only this owner may later disconnect.
        displayManager.requireIdle();
        running.set(true);
        waitForPortFree(RTSP_PORT, 1000);

        // 2. Allocate dynamic ports for dummy RTP drain
        this.rtpVideoPort = openUdpDrain();
        this.rtpAudioPort = openUdpDrain();
        Ln.d("Dynamic RTP ports allocated: Video=" + rtpVideoPort + ", Audio=" + rtpAudioPort);



        // 3. Build SemWifiDisplayConfig via reflection
        Object config = buildSemWifiDisplayConfig(rtpVideoPort, rtpAudioPort);

        // 4. Create dynamic Proxy for IWifiDisplayConnectionCallback
        Object connectionCallback = createWifiDisplayCallback();

        // 5. Trigger IPC connection on Samsung DisplayManagerService
        displayManager.connectWifiDisplayWithConfig(config, connectionCallback);
        Ln.i("IPC connectWifiDisplayWithConfig command sent to system_server.");

        // 6. Connect to RTSP socket opened by RemoteDisplay at 127.0.0.1:7236
        this.rtspSocket = connectRtspLoopback(RTSP_PORT, 6000);
        if (rtspSocket == null) {
            Ln.e("Error: Android RemoteDisplay did not open port " + RTSP_PORT);
            stop();
            return false;
        }

        if (!running.get()) { rtspSocket.close(); return false; }

        // 7. Start RTSP handshake loop on a dedicated thread
        Thread rtspThread = new Thread(this::runRtspStateMachine, "ScrcpyDeX-RTSP");
        rtspThread.setDaemon(true);
        rtspThread.start();

        // 8. Wait for PLAY completion signal (DeX active)
        long readyDeadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(15);
        boolean ready = false;
        while (running.get() && System.nanoTime() < readyDeadline) {
            if (rtspPlayCompleted.await(100, TimeUnit.MILLISECONDS)) { ready = true; break; }
        }
        if (ready) {
            Ln.i("RTSP handshake completed. Samsung DeX active.");
            return true;
        } else {
            Ln.e("Timeout waiting for RTSP handshake completion (15s).");
            return false;
        }
    }

    /**
     * Terminates the DeX session and releases network ports.
     */
    public boolean stop() {
        running.set(false);
        for (DatagramSocket socket : udpSockets) socket.close();
        udpSockets.clear();
        if (rtspSocket != null) {
            try { rtspSocket.close(); } catch (IOException ignored) {}
        }
        try { return displayManager.disconnectOwned(peerAddress); }
        catch (Exception ex) { Ln.w("Owned native DeX cleanup needs recovery: " + ex); return false; }
    }

    /**
     * Replaces Thread.sleep(800): attempts local bind in a loop.
     * As soon as the OS releases port 7236, this method returns immediately.
     */
    private void waitForPortFree(int port, int timeoutMs) throws IOException {
        long deadline = System.currentTimeMillis() + timeoutMs;
        Ln.d("Waiting for port " + port + " to be released...");
        while (running.get() && System.currentTimeMillis() < deadline) {
            try (ServerSocket ss = new ServerSocket(port, 1, InetAddress.getByName("127.0.0.1"))) {
                // If bind succeeds, the port is free
                Ln.d("Port " + port + " confirmed free.");
                return;
            } catch (IOException e) {
                try {
                    Thread.sleep(100);
                } catch (InterruptedException ie) {
                    Thread.currentThread().interrupt();
                    throw new IOException("Interrupted while waiting for port " + port, ie);
                }
            }
        }
        throw new IOException("RTSP port is already in use; existing sessions are left untouched.");
    }

    private int openUdpDrain() throws IOException {
        final DatagramSocket socket = new DatagramSocket(0, InetAddress.getByName("127.0.0.1"));
        socket.setSoTimeout(1000);
        udpSockets.add(socket);
        Thread thread = new Thread(() -> {
            try {
                byte[] buffer = new byte[8192];
                DatagramPacket packet = new DatagramPacket(buffer, buffer.length);
                while (running.get() && !socket.isClosed()) {
                    try { socket.receive(packet); } catch (SocketTimeoutException ignored) {}
                }
            } catch (IOException ignored) {
            } finally { socket.close(); udpSockets.remove(socket); }
        }, "DXM-Native-RTP-Drain");
        thread.setDaemon(true);
        thread.start();
        return socket.getLocalPort();
    }

    private Object buildSemWifiDisplayConfig(int videoPort, int audioPort) throws Exception {
        Class<?> builderClass = Class.forName("android.hardware.display.SemWifiDisplayConfig$Builder");
        Object builder = builderClass.getDeclaredConstructor().newInstance();

        Method setApConnection = builderClass.getMethod("setApConnection", 
                String.class, String.class, String.class, String.class);
        setApConnection.invoke(builder, "127.0.0.1", String.valueOf(RTSP_PORT), displayName, peerAddress);

        Method setMode = builderClass.getMethod("setMode", int.class);
        setMode.invoke(builder, 2); // MODE_WIRELESS_DEX = 2

        Method build = builderClass.getMethod("build");
        return build.invoke(builder);
    }

    private Object createWifiDisplayCallback() {
        try {
            Class<?> callbackInterface = Class.forName("android.hardware.display.IWifiDisplayConnectionCallback");
            return Proxy.newProxyInstance(
                callbackInterface.getClassLoader(),
                new Class<?>[]{callbackInterface},
                new InvocationHandler() {
                    @Override
                    public Object invoke(Object proxy, Method method, Object[] args) {
                        String name = method.getName();
                        if ("onSuccess".equals(name)) {
                            Ln.i("IWifiDisplayConnectionCallback: Connection accepted by system_server (onSuccess).");
                        } else if ("onFailure".equals(name)) {
                            int reason = args != null && args.length > 0 ? (int) args[0] : -1;
                            Ln.e("IWifiDisplayConnectionCallback: Connection failed with code: " + reason);
                        } else if ("asBinder".equals(name)) {
                            return null;
                        }
                        return null;
                    }
                }
            );
        } catch (Exception e) {
            Ln.w("Could not register Proxy for IWifiDisplayConnectionCallback: " + e.getMessage());
            return null;
        }
    }

    private Socket connectRtspLoopback(int port, int timeoutMs) {
        long deadline = System.currentTimeMillis() + timeoutMs;
        while (running.get() && System.currentTimeMillis() < deadline) {
            Socket socket = new Socket();
            try {
                socket.connect(new InetSocketAddress("127.0.0.1", port), 500);
                socket.setTcpNoDelay(true);
                socket.setSoTimeout(35000); // 35s read timeout
                Ln.d("Successfully connected to RTSP socket at 127.0.0.1:" + port);
                return socket;
            } catch (Exception e) {
                try { socket.close(); } catch (IOException ignored) {}
                try {
                    Thread.sleep(150);
                } catch (InterruptedException ie) {
                    Thread.currentThread().interrupt();
                    return null;
                }
            }
        }
        return null;
    }

    private void runRtspStateMachine() {
        try {
            BufferedReader in = new BufferedReader(
                new InputStreamReader(rtspSocket.getInputStream(), StandardCharsets.US_ASCII));
            OutputStream out = rtspSocket.getOutputStream();

            String presentationUrl = "rtsp://127.0.0.1/wfd1.0/streamid=0";
            String sessionId = null;
            AtomicInteger clientCSeq = new AtomicInteger(1);
            int setupCSeq = -1;
            int playCSeq = -1;

            while (running.get()) {
                RtspMessageReader.Message message = RtspMessageReader.read(in);
                if (message == null) break;
                String firstLine = message.firstLine;
                String cseq = message.cseq;
                String sessionHeader = message.session;
                String body = message.body;

                // Process WFD RTSP handshake states
                if (firstLine.startsWith("OPTIONS")) {
                    String resp = "RTSP/1.0 200 OK\r\n" +
                                  "CSeq: " + cseq + "\r\n" +
                                  "Public: org.wfa.wfd1.0, GET_PARAMETER, SET_PARAMETER\r\n\r\n";
                    sendRtsp(out, resp);

                    String m2 = "OPTIONS * RTSP/1.0\r\n" +
                                "CSeq: " + clientCSeq.getAndIncrement() + "\r\n" +
                                "Require: org.wfa.wfd1.0\r\n\r\n";
                    sendRtsp(out, m2);

                } else if (firstLine.startsWith("GET_PARAMETER")) {
                    if (body.contains("wfd_video_formats")) {
                        String m3Body = "wfd_client_rtp_ports: RTP/AVP/UDP;unicast " + rtpVideoPort + " 0 mode=play\r\n" +
                                        "wfd_audio_codecs: LPCM 00000003 00, AAC 0000000f 00\r\n" +
                                        "wfd_video_formats: " + videoFormats + "\r\n" +
                                        "wfd_content_protection: none\r\n" +
                                        "wfd_uibc_capability: input_category_list=HIDC;hidc_cap_list=Keyboard/USB, Mouse/USB, MultiTouch/USB, Gesture/USB, RemoteControl/USB;port=none\r\n";
                        byte[] bodyBytes = m3Body.getBytes(StandardCharsets.US_ASCII);
                        String resp = "RTSP/1.0 200 OK\r\n" +
                                      "CSeq: " + cseq + "\r\n" +
                                      "Content-Type: text/parameters\r\n" +
                                      "Content-Length: " + bodyBytes.length + "\r\n\r\n" +
                                      m3Body;
                        sendRtsp(out, resp);
                    } else {
                        String resp = "RTSP/1.0 200 OK\r\n" +
                                      "CSeq: " + cseq + "\r\n\r\n";
                        sendRtsp(out, resp);
                    }

                } else if (firstLine.startsWith("SET_PARAMETER")) {
                    for (String bLine : body.split("\r?\n")) {
                        if(bLine.startsWith("wfd_video_formats:"))Ln.i("Selected initial video mode: "+bLine);
                        if (bLine.startsWith("wfd_presentation_URL:")) {
                            String[] parts = bLine.substring(21).trim().split("\\s+");
                            if (parts.length > 0 && parts[0].startsWith("rtsp://")) {
                                presentationUrl = parts[0];
                            }
                        }
                    }

                    String resp = "RTSP/1.0 200 OK\r\n" +
                                  "CSeq: " + cseq + "\r\n\r\n";
                    sendRtsp(out, resp);

                    if (body.contains("wfd_trigger_method: SETUP")) {
                        setupCSeq = clientCSeq.getAndIncrement();
                        String setupReq = "SETUP " + presentationUrl + " RTSP/1.0\r\n" +
                                          "CSeq: " + setupCSeq + "\r\n" +
                                          "Transport: RTP/AVP/UDP;unicast;client_port=" + rtpVideoPort + "-" + rtpAudioPort + "\r\n\r\n";
                        sendRtsp(out, setupReq);
                    }

                } else if (firstLine.startsWith("RTSP/1.0 200 OK")) {
                    if (sessionHeader != null && sessionId == null && Integer.toString(setupCSeq).equals(cseq)) {
                        sessionId = sessionHeader;
                        playCSeq = clientCSeq.getAndIncrement();
                        String playReq = "PLAY " + presentationUrl + " RTSP/1.0\r\n" +
                                         "CSeq: " + playCSeq + "\r\n" +
                                         "Session: " + sessionId + "\r\n\r\n";
                        sendRtsp(out, playReq);
                    } else if (sessionId != null && Integer.toString(playCSeq).equals(cseq)) {
                        Ln.i("Received 200 OK response for PLAY. Samsung DeX activated.");
                        rtspPlayCompleted.countDown();
                    }
                }
            }
        } catch (Exception e) {
            if (running.get()) {
                Ln.e("Error in RTSP loop: " + e.getMessage(), e);
            }
        } finally {
            running.set(false);
        }
    }

    private void sendRtsp(OutputStream out, String msg) throws IOException {
        out.write(msg.getBytes(StandardCharsets.US_ASCII));
        out.flush();
    }
}

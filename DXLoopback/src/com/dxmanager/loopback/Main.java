// SPDX-License-Identifier: Apache-2.0
package com.dxmanager.loopback;

import java.io.IOException;
import java.io.File;
import java.io.RandomAccessFile;
import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.nio.channels.FileLock;
import java.util.HashSet;
import java.util.Set;
import java.util.concurrent.atomic.AtomicBoolean;

/** app_process entry point. No APK, root, permission grants, or external network connection. */
public final class Main {
    private static final int RTSP_PORT = 7236;
    private final SessionPolicy lease;
    private final AtomicBoolean protocolReady = new AtomicBoolean(false);
    private final AtomicBoolean protocolFailed = new AtomicBoolean(false);
    private final AtomicBoolean cleaned = new AtomicBoolean(false);
    private final String token;
    private SamsungDisplay display;
    private Socket rtspSocket;
    private DatagramSocket video;
    private DatagramSocket audio;
    private boolean connectIssued;
    private boolean connectionWasOwned;

    private Main(String token) { this.token = token; lease = new SessionPolicy(token, System.nanoTime()); }

    public static void main(String[] args) {
        if (args.length != 1 || !args[0].matches("[0-9a-f]{32}")) {
            report("DXM_LOOPBACK_ERROR invalid_arguments");
            return;
        }
        Main instance = new Main(args[0]);
        instance.run(args[0]);
    }

    private void run(String token) {
        Runtime.getRuntime().addShutdownHook(new Thread(new Runnable() {
            @Override public void run() { cleanup(); }
        }, "DXM-loopback-shutdown"));
        // A device-local lock serializes our helpers across independent host transports.
        try (RandomAccessFile lockFile = new RandomAccessFile("/data/local/tmp/dxm-loopback.lock", "rw");
                FileLock lock = lockFile.getChannel().tryLock()) {
            if (lock == null) throw new IllegalStateException("another helper running");
            startControlReader();
            display = new SamsungDisplay(token);
            display.ensureIdle();
            Set<Integer> before = new HashSet<Integer>();
            for (SessionPolicy.Display item : display.displays()) before.add(Integer.valueOf(item.id));
            // Refuse an occupied port; never disconnect something merely to free the port.
            try (ServerSocket probe = new ServerSocket()) {
                probe.setReuseAddress(false);
                probe.bind(new InetSocketAddress(InetAddress.getByName("127.0.0.1"), RTSP_PORT));
            }
            openDrains();
            checkLease();
            display.ensureIdle();
            connectIssued = true;
            display.connect();
            long deadline = System.nanoTime() + 20_000_000_000L;
            // Some firmware publishes active identity only after RTSP negotiation.
            // Idle-display + free-port preflight permits negotiation, never a foreign active display.
            rtspSocket = connectRtsp(deadline);
            startRtsp();
            int displayId = -1;
            while (displayId < 0 || !protocolReady.get()) {
                checkLease();
                if (protocolFailed.get() || System.nanoTime() >= deadline) throw new IOException("activation failed");
                if (display.hasForeignActive()) throw new IOException("ownership lost");
                if (display.ownsActive()) {
                    connectionWasOwned = true;
                    displayId = SessionPolicy.newWifiDisplay(before, display.displays());
                }
                Thread.sleep(100);
            }
            report("DXM_LOOPBACK_READY " + displayId);
            while (!lease.expired(System.nanoTime()) && !protocolFailed.get()) {
                if (!display.ownsActive() || !containsDisplay(displayId)) break;
                Thread.sleep(250);
            }
        } catch (Exception failure) {
            // Do not print token, device identifiers, IP addresses or hidden-API exception text.
            String reason = failure instanceof IllegalStateException ? "display_busy"
                    : failure instanceof ReflectiveOperationException || failure instanceof UnsupportedOperationException
                    ? "unsupported_firmware" : "activation_failed";
            if (!lease.expired(System.nanoTime())) report("DXM_LOOPBACK_ERROR " + reason);
        } finally {
            cleanup();
            report("DXM_LOOPBACK_STOPPED");
        }
    }

    private boolean containsDisplay(int id) throws Exception {
        for (SessionPolicy.Display item : display.displays()) if (item.id == id && item.type == 3) return true;
        return false;
    }

    private void checkLease() throws IOException {
        if (lease.expired(System.nanoTime())) throw new IOException("host lease expired");
    }

    private void startControlReader() {
        Thread reader = new Thread(new Runnable() {
            @Override public void run() {
                try {
                    for (;;) {
                        String line = Rtsp.line(System.in, 96);
                        if (line == null) break;
                        lease.receive(line, System.nanoTime());
                        if (lease.expired(System.nanoTime())) break;
                    }
                } catch (IOException ignored) {
                    // Broken ADB control means this helper no longer has a host lease.
                } finally { lease.stop(); }
            }
        }, "DXM-loopback-control");
        reader.setDaemon(true);
        reader.start();
    }

    private void openDrains() throws IOException {
        InetAddress loopback = InetAddress.getByName("127.0.0.1");
        for (int attempt = 0; attempt < 30; attempt++) {
            video = new DatagramSocket(new InetSocketAddress(loopback, 0));
            try {
                if (video.getLocalPort() >= 65535) throw new IOException("port pair unavailable");
                audio = new DatagramSocket(new InetSocketAddress(loopback, video.getLocalPort() + 1));
                startDrain(video, "DXM-loopback-rtp");
                startDrain(audio, "DXM-loopback-rtcp");
                return;
            } catch (IOException unavailable) { video.close(); }
        }
        throw new IOException("port pair unavailable");
    }

    private void startDrain(final DatagramSocket socket, String threadName) {
        Thread drain = new Thread(new Runnable() {
            @Override public void run() {
                byte[] bytes = new byte[65536];
                DatagramPacket packet = new DatagramPacket(bytes, bytes.length);
                try {
                    while (!socket.isClosed()) {
                        packet.setLength(bytes.length);
                        socket.receive(packet); // Discard dummy WFD traffic; scrcpy provides actual media.
                    }
                } catch (IOException ignored) {
                    if (!socket.isClosed()) protocolFailed.set(true);
                }
            }
        }, threadName);
        drain.setDaemon(true);
        drain.start();
    }

    private Socket connectRtsp(long deadline) throws Exception {
        while (System.nanoTime() < deadline) {
            checkLease();
            if (display.hasForeignActive()) throw new IOException("ownership lost");
            Socket socket = new Socket();
            try {
                socket.connect(new InetSocketAddress("127.0.0.1", RTSP_PORT), 500);
                socket.setTcpNoDelay(true);
                socket.setSoTimeout(35000);
                return socket;
            } catch (IOException failure) {
                socket.close();
                Thread.sleep(100);
            }
        }
        throw new IOException("RTSP unavailable");
    }

    private void startRtsp() {
        Thread protocol = new Thread(new Runnable() {
            @Override public void run() {
                try {
                    Rtsp.Handshake handshake = new Rtsp.Handshake(video.getLocalPort(), audio.getLocalPort());
                    for (;;) {
                        Rtsp.Message message = Rtsp.read(rtspSocket.getInputStream());
                        if (message == null) break;
                        handshake.accept(message, rtspSocket.getOutputStream());
                        if (handshake.isReady()) {
                            // Idle native WFD keepalive cadence varies by firmware. The separate
                            // 15-second host lease closes this socket; an idle RTSP link is not failure.
                            rtspSocket.setSoTimeout(0);
                            protocolReady.set(true);
                        }
                    }
                } catch (IOException ignored) {
                    // The lease loop handles protocol loss and performs scoped cleanup.
                } finally { protocolFailed.set(true); }
            }
        }, "DXM-loopback-rtsp");
        protocol.setDaemon(true);
        protocol.start();
    }

    private void cleanup() {
        if (!cleaned.compareAndSet(false, true)) return;
        lease.stop();
        try { if (rtspSocket != null) rtspSocket.close(); } catch (IOException ignored) { }
        if (video != null) video.close();
        if (audio != null) audio.close();
        if (connectIssued && display != null) {
            try {
                boolean requested = false;
                boolean confirmed = false;
                long deadline = System.nanoTime() + 1_000_000_000L;
                do {
                    if (!requested && display.disconnectOwned()) requested = true;
                    if ((requested || connectionWasOwned) && display.isWifiDisconnected()) {
                        confirmed = true;
                        break;
                    }
                    if (display.hasForeignActive()) break;
                    Thread.sleep(50);
                } while (System.nanoTime() < deadline);
                // A pending connect may not have published identity yet: never report it as
                // confirmed cleanup merely because a safe ownership check declined to disconnect.
                if (!confirmed) {
                    report("DXM_LOOPBACK_ERROR cleanup_unconfirmed");
                }
            }
            catch (Exception ignored) { report("DXM_LOOPBACK_ERROR cleanup_unconfirmed"); }
        }
        // Only this run's validated token path; no glob, shared jar, APK, or user data cleanup.
        File ownJar = new File("/data/local/tmp/dxm-loopback-" + token + ".jar");
        if (ownJar.exists() && !ownJar.delete()) report("DXM_LOOPBACK_ERROR helper_file_cleanup_unconfirmed");
    }

    private static void report(String line) {
        System.out.println(line);
        System.out.flush();
    }
}

package io.github.mazemei.dxmnativedex;

import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.util.Base64;
import java.util.concurrent.*;
import java.util.concurrent.atomic.AtomicInteger;

/** Token-authenticated loopback control. PC disconnect retains the Android display. */
public final class NativeControlServer implements AutoCloseable {
    public interface RecoveryPolicy {
        void configure(boolean companion, boolean restore, String original) throws Exception;
    }
    public interface FeaturePolicy { void enabled(boolean value) throws Exception; }
    public static final int PORT = 37125;
    private final String token;
    private final NativeRetentionState state;
    private final ServerSocket listener;
    private final CountDownLatch ready = new CountDownLatch(1), stopped = new CountDownLatch(1);
    private final AtomicInteger stopReplies = new AtomicInteger();
    private final java.util.Set<Socket> clients = ConcurrentHashMap.newKeySet();
    private volatile String geometry, error;
    private volatile boolean clean, closing;
    private volatile boolean companionPolicyAllowed = true;
    private final RecoveryPolicy recovery;
    private final FeaturePolicy feature;
    private final NativeAudioRouter audio;
    private final NativeBrowserCleanup browser;
    private final java.util.ArrayDeque<String> diagnostics=new java.util.ArrayDeque<>();
    private long diagnosticSequence;
    private volatile PrintWriter attachedOutput;
    public NativeControlServer(String token, NativeRetentionState state) throws IOException {
        this(token, state, null);
    }
    public NativeControlServer(String token, NativeRetentionState state, RecoveryPolicy recovery) throws IOException {
        this(token,state,recovery,true);
    }
    public NativeControlServer(String token, NativeRetentionState state, RecoveryPolicy recovery, boolean companion) throws IOException {
        this(token,state,recovery,companion,null,null);
    }
    public NativeControlServer(String token, NativeRetentionState state, RecoveryPolicy recovery, boolean companion,
        FeaturePolicy feature, NativeAudioRouter audio) throws IOException {
        this(token,state,recovery,companion,feature,audio,null);
    }
    public NativeControlServer(String token, NativeRetentionState state, RecoveryPolicy recovery, boolean companion,
        FeaturePolicy feature, NativeAudioRouter audio, NativeBrowserCleanup browser) throws IOException {
        this.token = NativeIdentity.validate(token); this.state = state;
        this.recovery = recovery;
        this.feature=feature;this.audio=audio;this.browser=browser;
        this.companionPolicyAllowed = companion;
        listener = new ServerSocket(PORT, 4, InetAddress.getByName("127.0.0.1"));
        Thread accept = new Thread(() -> {
            while (!closing) try {
                Socket client = listener.accept();
                if (clients.size() >= 6) { client.close(); continue; }
                clients.add(client);
                Thread worker = new Thread(() -> handle(client), "DXM-Native-Client");
                worker.setDaemon(true); worker.start();
            } catch (IOException e) { if (!closing) state.stop("Native control listener failed"); }
        }, "DXM-Native-Control");
        accept.setDaemon(true); accept.start();
    }
    private static long now() { return System.nanoTime() / 1000000L; }
    public void ready(String value) { geometry = value; ready.countDown(); }
    public void failed(String value) { error = value; ready.countDown(); }
    public void stopped(boolean value) { clean = value; stopped.countDown(); }
    private void emit(PrintWriter out, String kind, String data) {
        synchronized(out) {
        out.println("DXND1|" + token + "|" + kind + "|" + data);
        if (out.checkError()) throw new IllegalStateException("Native client closed.");
        }
    }
    public void diagnostic(String text) {
        String safe=text.replaceAll("[^A-Za-z0-9 _.:=@(),/\\-]","_");
        if(safe.length()>384)safe=safe.substring(0,384);
        String encoded;
        synchronized(diagnostics) {
            encoded=Base64.getEncoder().encodeToString((++diagnosticSequence+" "+safe).getBytes(StandardCharsets.UTF_8));
            if(diagnostics.size()>=32)diagnostics.removeFirst();diagnostics.addLast(encoded);
        }
        PrintWriter out=attachedOutput;
        if(out!=null)try{emit(out,"POLICY_LOG",encoded);}catch(RuntimeException ignored){}
    }
    private void handle(Socket socket) {
        long owner = -1;
        boolean requestedViewer=false,requestedPower=false;
        PrintWriter outputRef=null;
        try (Socket client = socket) {
            client.setTcpNoDelay(true); client.setSoTimeout(7000);
            BufferedReader input = new BufferedReader(new InputStreamReader(client.getInputStream(), StandardCharsets.US_ASCII));
            PrintWriter out = new PrintWriter(new OutputStreamWriter(client.getOutputStream(), StandardCharsets.UTF_8), true);
            outputRef=out;
            String line;
            while ((line = readLine(input, 256)) != null) {
                String[] args = line.split(" ");
                if (args.length < 2 || !token.equals(args[1])) return;
                if (args[0].equals("ATTACH") && args.length == 2 && owner < 0) {
                    if (!ready.await(40, TimeUnit.SECONDS)) throw new IOException("Native activation timed out.");
                    if (error != null) { synchronized(diagnostics){for(String record:diagnostics)emit(out,"POLICY_LOG",record);} emit(out,"ERROR",Base64.getEncoder().encodeToString(error.getBytes(StandardCharsets.UTF_8))); return; }
                    if (state.reason() != null) return;
                    owner = state.attach(now());attachedOutput=out;
                    synchronized(diagnostics){for(String record:diagnostics)emit(out,"POLICY_LOG",record);}
                    emit(out,"READY",geometry);
                } else if (args[0].equals("PING") && args.length == 2 && owner >= 0) {
                    state.ping(owner,now());
                    state.viewer(owner,requestedViewer,requestedPower);
                } else if (args[0].equals("VIEWER") && args.length == 4 && owner >= 0) {
                    if((!args[2].equals("0")&&!args[2].equals("1"))||(!args[3].equals("0")&&!args[3].equals("1")))return;
                    requestedViewer=args[2].equals("1");requestedPower=args[3].equals("1");
                    boolean current=state.viewer(owner,requestedViewer,requestedPower);
                    emit(out,"VIEWER",current?Long.toString(state.snapshot().revision):"STALE");
                } else if (args[0].equals("SESSION") && args.length == 2) {
                    emit(out,"SESSION",state.snapshot().frame());return;
                } else if (args[0].equals("ROUTE") && args.length == 4 && audio!=null) {
                    if(!args[3].equals("PHONE")&&!args[3].equals("DEX"))return;
                    long expected=Long.parseLong(args[2]);boolean dex=args[3].equals("DEX");String result;
                    try{result=state.route(expected,dex,()->audio.select(dex));}
                    catch(Exception error){result="ERROR";diagnostic("Audio error="+error.getClass().getSimpleName());}
                    diagnostic("Audio rev="+expected+" target="+args[3]+" result="+result);
                    emit(out,"ROUTE",result);return;
                } else if (args[0].equals("BROWSER") && args.length == 3 && browser!=null) {
                    long expected=Long.parseLong(args[2]);String result;
                    try{result=state.stopBrowser(expected,browser::stop);}
                    catch(Exception error){result="ERROR";diagnostic("Browser error="+error.getClass().getSimpleName());}
                    diagnostic("Browser rev="+expected+" result="+result);
                    emit(out,"BROWSER",result);return;
                } else if (args[0].equals("REPORT") && args.length == 4 && state.snapshot().enabled) {
                    byte[] bytes=Base64.getDecoder().decode(args[3]);if(bytes.length>128)return;
                    diagnostic("Companion rev="+Long.parseLong(args[2])+" "+new String(bytes,StandardCharsets.UTF_8));
                    emit(out,"REPORT","1");return;
                } else if (args[0].equals("STATUS") && args.length == 2) {
                    emit(out,"STATE",geometry == null ? "STARTING" : (state.connected()?"ACTIVE":"PENDING") + "|" + geometry); return;
                } else if (args[0].equals("CONFIGURE") && (args.length == 6||args.length == 7||args.length == 8) && state.reason() == null && recovery != null) {
                    int delay = NativeRetentionState.validateDelay(Integer.parseInt(args[2]));
                    if ((!args[3].equals("companion") && !args[3].equals("basic")) ||
                        (!args[4].equals("1") && !args[4].equals("0"))) return;
                    boolean companion = args[3].equals("companion");
                    // Basic native sessions use the same fixed grace, without an APK dependency.
                    if(args.length>=7&&!args[6].equals("1")&&!args[6].equals("0"))return;
                    if(args.length==8&&!args[7].equals("1")&&!args[7].equals("0"))return;
                    String original = NativePowerRecovery.original(args[5]);
                    recovery.configure(companion, args[4].equals("1"), original);
                    boolean enabled=args.length>=7&&args[6].equals("1")&&companion;
                    if(feature!=null)feature.enabled(enabled);
                    state.enablePolicy(enabled);
                    state.enableBrowserPolicy(args.length==8&&args[7].equals("1")&&enabled);
                    companionPolicyAllowed = companion;
                    state.policy(delay);
                    emit(out,"CONFIGURED",Integer.toString(delay)); return;
                } else if (args[0].equals("POLICY") && args.length == 3 && state.reason() == null && companionPolicyAllowed) {
                    state.policy(Integer.parseInt(args[2])); emit(out,"POLICY",Integer.toString(state.policy())); return;
                } else if (args[0].equals("STOP") && args.length == 2) {
                    stopReplies.incrementAndGet();
                    try { state.stop("Explicit stop"); stopped.await(15,TimeUnit.SECONDS); emit(out,"STOPPED",clean?"1":"0"); }
                    finally { stopReplies.decrementAndGet(); }
                    return;
                } else return;
            }
        } catch (Exception ignored) {
        } finally { if (owner >= 0) state.detach(owner,now());if(attachedOutput==outputRef)attachedOutput=null;clients.remove(socket); }
    }
    public static String readLine(Reader reader, int limit) throws IOException {
        StringBuilder line = new StringBuilder(); int c;
        while ((c = reader.read()) != -1) {
            if (c == '\n') return line.toString();
            if (c == '\r') continue;
            if (line.length() >= limit) throw new IOException("Control line too long.");
            line.append((char)c);
        }
        return line.length() == 0 ? null : line.toString();
    }
    public static String request(String token, String command, int timeoutMs) throws Exception {
        NativeIdentity.validate(token);
        try (Socket socket = new Socket()) {
            socket.connect(new InetSocketAddress("127.0.0.1",PORT),1000);
            socket.setSoTimeout(timeoutMs);
            PrintWriter out = new PrintWriter(new OutputStreamWriter(socket.getOutputStream(),StandardCharsets.US_ASCII),true);
            out.println(command + " " + token);
            return readLine(new InputStreamReader(socket.getInputStream(),StandardCharsets.UTF_8),8192);
        }
    }
    public void finishReplies() {
        long until = now() + 1500;
        while (stopReplies.get() > 0 && now() < until) try { Thread.sleep(20); } catch (InterruptedException e) { return; }
    }
    public void close() {
        closing = true;
        try { listener.close(); } catch (IOException ignored) {}
        for (Socket socket : clients) try { socket.close(); } catch (IOException ignored) {}
        clients.clear();
    }
}

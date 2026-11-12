package io.github.mazemei.dxmnativedex;

import android.os.Handler;
import android.os.HandlerThread;
import android.os.Looper;
import android.system.Os;
import com.scrcpydex.server.*;
import com.scrcpydex.server.wrappers.DisplayManager;
import java.io.*;
import java.nio.channels.FileLock;
import java.nio.charset.StandardCharsets;
import java.util.Base64;
import java.util.Properties;
import java.util.concurrent.*;
import java.util.concurrent.atomic.AtomicBoolean;

/** Native DeX owner with a fixed reconnect grace; Companion timing is separate. */
public final class NativeDexServer {
    private static final AtomicBoolean cleaned = new AtomicBoolean(false);
    private static volatile DexActivator activator;
    private static DisplayManager manager;
    private static String token;
    private static Guard guard;
    private static boolean ownRecord;
    private static volatile boolean ready;
    private static DisplayWatch watch;
    private static HandlerThread displayThread;
    private static NativeControlServer control;
    private static NativeRetentionState lifetime;
    private static NativePolicyNotifier policyNotifier;
    private static NativeBluetoothPreserver bluetoothStartup;
    private static long now() { return System.nanoTime() / 1000000L; }
    private static void emit(String kind, String data) {
        System.out.println("DXND1|" + token + "|" + kind + "|" + data); System.out.flush();
    }
    public static void main(String[] args) {
        int code = 0;
        RandomAccessFile lockFile = null; FileLock lock = null;
        boolean serve = false;
        try {
            if (args.length < 2) throw new IllegalArgumentException("Expected native command and token.");
            token = NativeIdentity.validate(args[1]);
            if ("launch".equals(args[0])) { launch(args); return; }
            if ("status".equals(args[0])) {
                Properties record = NativeRecovery.read();
                emit("PRESENCE",record == null || !token.equals(record.getProperty("token")) ? "ABSENT" :
                    NativeRecovery.helperAlive(record) ? "PRESENT" : "STALE"); return;
            }
            Looper.prepareMainLooper(); Workarounds.apply(); manager = new DisplayManager();
            if ("recover".equals(args[0])) {
                Properties record = NativeRecovery.read();
                if (record != null && token.equals(record.getProperty("token")) && NativeRecovery.helperAlive(record)) {
                    String reply = NativeControlServer.request(token,"STOP",17000);
                    emit("RECOVERED",("DXND1|"+token+"|STOPPED|1").equals(reply)?"1":"0");
                } else {
                    emit("RECOVERED",NativeRecovery.cleanup(manager,token,6000)?"1":"0");
                }
                return;
            }
            if (!"serve".equals(args[0]) || (args.length != 10&&args.length != 11&&args.length != 12)) throw new IllegalArgumentException("Invalid native daemon mode.");
            // A new session and redirected stdio prevent ADB disconnection from owning this process lifetime.
            Os.setsid(); serve = true;
            int width = Integer.parseInt(args[2]), height = Integer.parseInt(args[3]), dpi = Integer.parseInt(args[4]);
            int policy = NativeRetentionState.validateDelay(Integer.parseInt(args[5]));
            if ((!args[6].equals("companion") && !args[6].equals("basic")) ||
                (!args[7].equals("1") && !args[7].equals("0")) || (args[6].equals("basic") && policy != NativeRetentionState.DISCONNECT_GRACE_SECONDS))
                throw new IllegalArgumentException("Invalid native recovery mode.");
            String powerOriginal=NativePowerRecovery.original(args[8]);
            if(!args[9].equals("0")&&!args[9].equals("1"))throw new IllegalArgumentException("Invalid retained policy feature.");
            boolean retainedPolicy=args[9].equals("1")&&args[6].equals("companion");
            if(args.length>=11&&!args[10].equals("0")&&!args[10].equals("1"))throw new IllegalArgumentException("Invalid startup Bluetooth option.");
            boolean keepBluetooth=args.length>=11&&args[10].equals("1");
            boolean initialMetrics=args.length==12&&args[11].equals("1");
            if(args.length==12&&(!args[11].equals("1")&&!args[11].equals("0")))throw new IllegalArgumentException("Invalid initial metrics flag.");
            if(initialMetrics&&!NativeInitialMode.supported(width,height))throw new IllegalArgumentException("Unvalidated initial native resolution.");
            if (width < 320 || height < 320 || width > 8192 || height > 8192 || dpi < 48 || dpi > 960)
                throw new IllegalArgumentException("Unsupported display metrics.");
            lockFile = new RandomAccessFile("/data/local/tmp/dxm-github-native-dex.lock","rw");
            lock = lockFile.getChannel().tryLock();
            if (lock == null) throw new IllegalStateException("Another native helper owns this phone.");
            Properties previous = NativeRecovery.read();
            if (previous != null) {
                if (NativeRecovery.helperAlive(previous)) throw new IllegalStateException("Existing native session must be reattached, not replaced.");
                String previousToken = previous.getProperty("token");
                if (!NativeRecovery.cleanup(manager,previousToken,6000)) throw new IllegalStateException("Previous native session needs explicit recovery.");
            }
            manager.requireIdle();
            lifetime = new NativeRetentionState(policy,now());
            lifetime.enablePolicy(retainedPolicy);
            Properties record = new Properties();
            record.setProperty("version","1"); record.setProperty("protocol","7"); record.setProperty("token",token);
            record.setProperty("retained.policy",Boolean.toString(retainedPolicy));
            record.setProperty("pid",Integer.toString(android.os.Process.myPid())); record.setProperty("phase","starting");
            NativePowerRecovery.configure(record,args[6].equals("companion"),args[7].equals("1"),powerOriginal);
            NativeRecovery.save(record); ownRecord = true;
            control = new NativeControlServer(token,lifetime,(companion,restore,original) -> NativeRecovery.configurePower(token,companion,restore,original),NativePowerRecovery.usesCompanion(record),
                enabled->NativeRecovery.configureFeature(token,enabled),NativeAudioRouter.samsung(()->ready&&watch!=null&&watch.isDisplayPresent()&&activator!=null&&activator.isRunning()),
                NativeBrowserCleanup.samsung(()->!cleaned.get()&&ready&&watch!=null&&watch.isDisplayPresent()&&activator!=null&&activator.isRunning()));
            policyNotifier=new NativePolicyNotifier(NativeDexServer::notifyPolicy);
            bluetoothStartup=new NativeBluetoothPreserver(NativeAudioRouter.samsungBluetooth(),
                ()->!cleaned.get()&&lifetime.reason()==null&&watch!=null&&watch.isDisplayPresent()&&activator!=null&&activator.isRunning(),control::diagnostic);
            try (FileOutputStream boot = new FileOutputStream(bootFile(token))) {
                boot.write(Integer.toString(android.os.Process.myPid()).getBytes(StandardCharsets.US_ASCII)); boot.getFD().sync();
            }

            Runtime.getRuntime().addShutdownHook(new Thread(NativeDexServer::cleanup,"DXM-Native-Shutdown"));
            guard = new Guard(token); guard.start(); guard.send("ARM");
            Thread supervisor = new Thread(() -> {
                while (!cleaned.get()) try {
                    if(lifetime.reason()!=null&&!ready&&activator!=null)activator.stop();
                    guard.send("PING"); Thread.sleep(500);
                } catch (Exception e) { lifetime.stop("Independent guard failed"); break; }
            },"DXM-Native-Engine-Liveness");
            supervisor.setDaemon(true); supervisor.start();
            activator = new DexActivator(manager,NativeIdentity.name(token),NativeIdentity.address(token),initialMetrics?NativeInitialMode.formats(width,height):null);
            displayThread = new HandlerThread("DXM-Native-Display"); displayThread.start();
            watch = new DisplayWatch(NativeIdentity.name(token)); watch.startListening(new Handler(displayThread.getLooper()));
            bluetoothStartup.capture(keepBluetooth);
            if (!activator.activate()) throw new IllegalStateException("Samsung native DeX activation did not complete.");
            int displayId=-1;
            for(int i=0;i<15&&lifetime.reason()==null&&displayId<=0;i++)displayId=watch.waitForDisplay(1);
            if (displayId <= 0) throw new IllegalStateException("Owned native display was not detected.");
            // Correct startup audio as soon as ownership is known, before wm setup or PC READY.
            bluetoothStartup.ready(now());
            NativeMetricsVerifier.Metrics verified=initialMetrics?NativeMetrics.verifyInitial(record,watch,width,height,dpi,()->lifetime.reason()!=null,control::diagnostic):NativeMetrics.apply(record,watch,width,height,dpi,()->lifetime.reason()!=null,control::diagnostic);
            record.setProperty("phase","active"); NativeRecovery.save(record);
            ready=true;
            control.ready(displayId+"|"+verified.width+"|"+verified.height+"|"+verified.dpi+"|"+android.os.Process.myPid()+"|"+guard.pid);
            NativeSessionFallback fallback = new NativeSessionFallback(lifetime,
                NativeAudioRouter.samsung(()->ready&&!cleaned.get()&&watch!=null&&watch.isDisplayPresent()&&activator!=null&&activator.isRunning()),
                NativeRecovery::setManagedStayAwake,control::diagnostic,
                ()->ready&&!cleaned.get()&&watch!=null&&watch.isDisplayPresent()&&activator!=null&&activator.isRunning(),
                ()->{try{notifyPolicy(lifetime.snapshot());}catch(Exception error){control.diagnostic("Native fallback delegate refresh deferred");}});
            long lastCheck = 0;int lastReportedDpi=verified.dpi;
            while (!lifetime.tick(now()) && activator.isRunning() && guard.process.isAlive()) {
                bluetoothStartup.tick(now());
                policyNotifier.publish(lifetime.snapshot());
                fallback.tick();
                if (now()-lastCheck >= 1000) {
                    if (!watch.isDisplayPresent()) break;
                    control.ready(displayId+"|"+watch.getWidth()+"|"+watch.getHeight()+"|"+watch.getDpi()+"|"+android.os.Process.myPid()+"|"+guard.pid);
                    int currentDpi=watch.getDpi();
                    if(currentDpi!=lastReportedDpi){control.diagnostic("Metrics "+(currentDpi==dpi?"response":"warning")+" running density changed requested="+dpi+" actual="+currentDpi);lastReportedDpi=currentDpi;}
                    lastCheck=now();
                }
                Thread.sleep(100);
            }
            Ln.i("Native session ending: "+lifetime.reason());
        } catch (Throwable e) {
            code = 1;
            String message=e.getMessage()==null?e.getClass().getSimpleName():e.getMessage();
            if (control != null) control.failed(message);
            else if (token != null) emit("ERROR",Base64.getEncoder().encodeToString(message.getBytes(StandardCharsets.UTF_8)));
            Ln.e("Native DeX: "+message,e);
        } finally {
            if (serve) cleanup();
            try { if (lock != null) lock.release(); } catch (Exception ignored) {}
            try { if (lockFile != null) lockFile.close(); } catch (Exception ignored) {}
            System.exit(code);
        }
    }
    private static File bootFile(String id) { return new File("/data/local/tmp/dxm-github-detached-"+NativeIdentity.validate(id)); }
    private static void launch(String[] args) throws Exception {
        if (args.length != 10&&args.length != 11&&args.length != 12) throw new IllegalArgumentException("Invalid native launch.");
        String jar=System.getenv("CLASSPATH");
        if (jar==null || !jar.matches("/data/local/tmp/dxm-github-native-[0-9a-f]{16}\\.jar")) throw new IllegalStateException("Unexpected classpath.");
        ProcessBuilder builder = new ProcessBuilder("/system/bin/app_process","/",NativeDexServer.class.getName(),"serve",token,args[2],args[3],args[4],args[5],args[6],args[7],args[8],args[9],args.length>=11?args[10]:"0",args.length==12?args[11]:"0");
        builder.environment().put("CLASSPATH",jar);
        builder.redirectInput(new File("/dev/null")); builder.redirectErrorStream(true);
        builder.redirectOutput(new File("/data/local/tmp/dxm-github-native-dex.log"));
        Process daemon=builder.start();
        long until=now()+12000;
        while (now()<until && daemon.isAlive() && !bootFile(token).exists()) Thread.sleep(25);
        if (!bootFile(token).exists() || !daemon.isAlive()) throw new IllegalStateException("Detached native owner did not start; no transport-bound fallback is used.");
        if (!bootFile(token).delete()) Ln.w("Could not remove native bootstrap marker.");
        emit("LAUNCHED","1");
    }
    private static void cleanup() {
        if (!cleaned.compareAndSet(false,true)) return;
        if(bluetoothStartup!=null)bluetoothStartup.close();
        if(lifetime!=null)lifetime.stop("Native owner ending");
        if(policyNotifier!=null)policyNotifier.close();
        boolean clean=!ownRecord;
        boolean notify=false;
        try {
            if (ownRecord) {
                notify=NativeRecovery.usesCompanion(token);
                // This owned display is being destroyed. Keep its final metrics until removal,
                // so Android apps cannot save bounds from a transient default configuration.
                if (activator != null) activator.stop();
                boolean offline=lifetime!=null&&!lifetime.connected()&&!"Explicit stop".equals(lifetime.reason());
                clean=NativeRecovery.cleanup(manager,token,6000,offline);
            }
        } catch (Exception e) { Ln.w("Native cleanup pending: "+e); }
        finally {
            if (watch!=null) watch.close(); if (displayThread!=null) displayThread.quitSafely();
            if (guard!=null) try {
                if (clean) guard.send("DISARM"); guard.writer.close(); guard.process.waitFor(7,TimeUnit.SECONDS);
                if (!clean) clean=guard.clean;
            } catch (Exception e) { Ln.w("Native guard cleanup: "+e); }
            if (clean && ownRecord && notify) notifyCompanion();
            if (control!=null) { control.stopped(clean); control.finishReplies(); control.close(); }
        }
    }
    private static void notifyCompanion() { notifyCompanion(token,lifetime!=null&&"Companion cleanup timeout".equals(lifetime.reason())); }
    private static void notifyPolicy(NativeRetentionState.Snapshot snapshot) throws Exception {
        if(snapshot.revision!=lifetime.snapshot().revision||!snapshot.enabled)return;
        String pkg="io.github.mazemei.dxdisplaycleanup";
        Process process=new ProcessBuilder("/system/bin/am","broadcast","--receiver-foreground","-n",pkg+"/io.github.mazemei.dxdisplaycleanup.NativeSessionReceiver",
            "-a",pkg+".NATIVE_SESSION_STATE","--es","native_token",token,"--el","policy_revision",Long.toString(snapshot.revision))
            .redirectErrorStream(true).redirectOutput(new File("/dev/null")).start();
        if(!process.waitFor(3,TimeUnit.SECONDS)){process.destroy();control.diagnostic("Companion event timeout rev="+snapshot.revision);}
        else if(process.exitValue()!=0)control.diagnostic("Companion event failed rev="+snapshot.revision+" exit="+process.exitValue());
    }
    static void notifyCompanion(String id,boolean expired) {
        try {
            String pkg="io.github.mazemei.dxdisplaycleanup";
            Process p=new ProcessBuilder("/system/bin/am","broadcast","--receiver-foreground","-n",pkg+"/io.github.mazemei.dxdisplaycleanup.NativeSessionReceiver","-a",pkg+".NATIVE_SESSION_COMPLETE","--es","native_token",NativeIdentity.validate(id),"--ez","expired",Boolean.toString(expired))
                .redirectErrorStream(true).redirectOutput(new File("/dev/null")).start();
            if(!p.waitFor(3,TimeUnit.SECONDS))p.destroy();
        }catch(Exception e){Ln.w("Companion completion notification: "+e);}
    }
    private static final class Guard {
        final String id;
        Process process;
        PrintWriter writer;
        volatile int pid;
        volatile boolean clean;
        final CountDownLatch started = new CountDownLatch(1);
        Guard(String id) { this.id = id; }
        void start() throws Exception {
            String jar = System.getenv("CLASSPATH");
            if (jar == null || !jar.startsWith("/data/local/tmp/dxm-github-native-") || !jar.endsWith(".jar"))
                throw new IllegalStateException("Unexpected helper classpath.");
            ProcessBuilder builder = new ProcessBuilder("/system/bin/app_process", "/", NativeDexGuardian.class.getName(), id);
            builder.environment().put("CLASSPATH", jar);
            process = builder.start();
            writer = new PrintWriter(new OutputStreamWriter(process.getOutputStream(), StandardCharsets.US_ASCII), true);
            Thread replies = new Thread(() -> {
                try {
                    BufferedReader in = new BufferedReader(new InputStreamReader(process.getInputStream(), StandardCharsets.US_ASCII));
                    String line;
                    while ((line = in.readLine()) != null) {
                        String prefix = "DXGUARD1|" + id + "|";
                        if (!line.startsWith(prefix)) continue;
                        String[] fields = line.substring(prefix.length()).split("\\|");
                        if (fields.length == 2 && fields[0].equals("READY")) { pid = Integer.parseInt(fields[1]); started.countDown(); }
                        if (fields.length == 2 && fields[0].equals("STOPPED")) clean = fields[1].equals("1");
                    }
                } catch (Exception e) { Ln.w("Native guard control: " + e); }
            }, "DXM-Native-Guard-Replies");
            replies.setDaemon(true); replies.start();
            Thread errors = new Thread(() -> {
                try {
                    BufferedReader in = new BufferedReader(new InputStreamReader(process.getErrorStream(), StandardCharsets.UTF_8));
                    String line; while ((line = in.readLine()) != null) Ln.w("Guard: " + line);
                } catch (Exception ignored) {}
            }, "DXM-Native-Guard-Log");
            errors.setDaemon(true); errors.start();
            if (!started.await(5, TimeUnit.SECONDS) || pid <= 0) throw new IllegalStateException("The independent native cleanup guard did not start.");
        }
        synchronized void send(String kind) {
            writer.println(kind + " " + id);
            if (writer.checkError()) throw new IllegalStateException("The native cleanup guard disconnected.");
        }
    }
}

package io.github.mazemei.dxdisplaycleanup;

import android.content.Context;
import android.content.SharedPreferences;
import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;

/** Private native backend. Legacy overlay and transfer settings keep their existing paths. */
final class NativeSessionBridge {
    private static final String PREFS="native_dex_session";
    private static final int PORT=37125;
    private static final java.util.concurrent.ExecutorService STATUS_EXECUTOR=java.util.concurrent.Executors.newSingleThreadExecutor(r->{Thread t=new Thread(r,"DXM-Native-Status");t.setDaemon(true);return t;});
    private static final NativeStatusCache<Snapshot> CACHE=new NativeStatusCache<>(STATUS_EXECUTOR,android.os.SystemClock::elapsedRealtime,1000);
    private static final java.util.Set<Runnable> LISTENERS=new java.util.concurrent.CopyOnWriteArraySet<>();
    private static final java.util.concurrent.atomic.AtomicBoolean STOP_QUEUED=new java.util.concurrent.atomic.AtomicBoolean();
    private static final java.util.concurrent.atomic.AtomicBoolean OBSERVING=new java.util.concurrent.atomic.AtomicBoolean();
    private static final java.util.concurrent.atomic.AtomicBoolean PRESENCE_QUEUED=new java.util.concurrent.atomic.AtomicBoolean();
    private static final java.util.concurrent.ThreadPoolExecutor POLICY_EXECUTOR=new java.util.concurrent.ThreadPoolExecutor(
        1,1,0L,java.util.concurrent.TimeUnit.MILLISECONDS,new java.util.concurrent.ArrayBlockingQueue<>(1),
        r->{Thread t=new Thread(r,"DXM-Companion-Retained-Policy");t.setDaemon(true);return t;},
        (job,executor)->{
            Runnable old=executor.getQueue().poll();if(old instanceof PolicyJob)((PolicyJob)old).finish();
            if(!executor.isShutdown())executor.execute(job);else if(job instanceof PolicyJob)((PolicyJob)job).finish();
        });
    static void addStatusListener(Runnable listener){LISTENERS.add(listener);}
    static void removeStatusListener(Runnable listener){LISTENERS.remove(listener);}
    private static void publish(Context context){
        Context app=context.getApplicationContext();
        new android.os.Handler(android.os.Looper.getMainLooper()).post(()->{
            for(Runnable listener:LISTENERS)listener.run();
            CleanupWidgetProvider.updateAll(app);CleanupTileService.requestRefresh(app);
        });
    }
    static final class Snapshot {
        final boolean active,confirmed; final Boolean inUse; final String detail,error;
        Snapshot(boolean active,String detail,String error){this(active,detail,error,false,null);}
        Snapshot(boolean active,String detail,String error,boolean confirmed,Boolean inUse){this.active=active;this.detail=detail;this.error=error;this.confirmed=confirmed;this.inUse=inUse;}
    }
    private static SharedPreferences preferences(Context c){return c.getSharedPreferences(PREFS,Context.MODE_PRIVATE);}
    static boolean hasSession(Context c){
        SharedPreferences p=preferences(c);
        if(p.getString("token","").isEmpty()){CACHE.clear();return false;}
        int boot=android.provider.Settings.Global.getInt(c.getContentResolver(),"boot_count",0);
        if(p.contains("boot")&&p.getInt("boot",boot)!=boot){p.edit().clear().apply();CACHE.clear();return false;}
        return !p.getString("token","").isEmpty();
    }
    static synchronized void configure(Context c,String token,boolean restore,String original) {
        configure(c,token,restore,original,false);
    }
    static synchronized void configure(Context c,String token,boolean restore,String original,boolean policy) {
        configure(c,token,restore,original,policy,false);
    }
    static synchronized void configure(Context c,String token,boolean restore,String original,boolean policy,boolean browserPolicy) {
        if(!token.matches("[0-9a-f]{32}"))throw new IllegalArgumentException("Invalid native session identity.");
        CACHE.invalidate();
        if(!token.equals(preferences(c).getString("token","")))preferences(c).edit().putBoolean("display_seen",false).putInt("display_id",-1).apply();
        observeDisplayState(c);
        preferences(c).edit().putInt("boot",android.provider.Settings.Global.getInt(c.getContentResolver(),"boot_count",0)).putString("token",token).putBoolean("restore",restore).putString("original",original==null?"":original).putBoolean("retained_policy",policy).putBoolean("browser_policy",browserPolicy&&policy).apply();
    }
    private static boolean policySession(Context c,String token){return hasSession(c)&&token.equals(preferences(c).getString("token",""))&&preferences(c).getBoolean("retained_policy",false);}
    static void onStateEvent(Context c,String token,Runnable finished) {
        if(!policySession(c,token)){finished.run();return;}
        POLICY_EXECUTOR.execute(new PolicyJob(c.getApplicationContext(),token,finished));
    }
    static void onGuardianConnectionChanged(Context c) {
        refreshPresenceAsync(c);
        String token=preferences(c).getString("token","");
        if(policySession(c,token))onStateEvent(c,token,()->{});
    }
    private static final class PolicyJob implements Runnable {
        final Context context;final String token;final Runnable finished;
        final java.util.concurrent.atomic.AtomicBoolean done=new java.util.concurrent.atomic.AtomicBoolean();
        long deadline;
        PolicyJob(Context context,String token,Runnable finished){this.context=context;this.token=token;this.finished=finished;}
        void finish(){if(done.compareAndSet(false,true))finished.run();}
        void check() throws IOException {
            if(!policySession(context,token)||android.os.SystemClock.elapsedRealtime()>deadline)throw new IOException("Retained policy session changed or timed out.");
        }
        String command(String verb,String suffix,int timeout) throws IOException {
            check();int remaining=(int)Math.max(1,Math.min(timeout,deadline-android.os.SystemClock.elapsedRealtime()));
            return requestToken(token,verb,suffix,remaining);
        }
        @Override public void run() {
            deadline=android.os.SystemClock.elapsedRealtime()+3500;
            try {
                RetainedSessionPolicy.apply(new RetainedSessionPolicy.Access(){
                    public RetainedSessionPolicy.State read() throws Exception {return RetainedSessionPolicy.parse(command("SESSION","",700));}
                    public boolean bluetoothConnected() throws Exception {
                        check();android.media.AudioManager audio=context.getSystemService(android.media.AudioManager.class);
                        if(audio==null)throw new IOException("Audio device list unavailable.");
                        android.media.AudioDeviceInfo[] outputs=audio.getDevices(android.media.AudioManager.GET_DEVICES_OUTPUTS);
                        if(outputs==null||outputs.length==0)throw new IOException("Audio device list unavailable.");
                        for(android.media.AudioDeviceInfo output:outputs)if(RetainedSessionPolicy.bluetoothType(output.getType()))return true;
                        return false;
                    }
                    public String awake(boolean enabled) throws Exception {
                        synchronized(NativeSessionBridge.class) {
                        check();StayAwakeRepository.Snapshot before=StayAwakeRepository.inspect(context);
                        if(enabled&&"7".equals(before.rawValue))return "ON";
                        if(!enabled&&before.status==StayAwakeRepository.Status.INACTIVE)return "OFF";
                        StayAwakeRepository.Snapshot result=enabled?StayAwakeRepository.restore(context,"7"):StayAwakeRepository.cleanup(context);
                        boolean ok=enabled?result.status==StayAwakeRepository.Status.ACTIVE:result.status==StayAwakeRepository.Status.INACTIVE;
                        return ok?(enabled?"ON":"OFF"):"ERROR";
                        }
                    }
                    public String route(boolean dex,long revision) throws Exception {
                        String reply=command("ROUTE",revision+" "+(dex?"DEX":"PHONE"),800);
                        if(!reply.startsWith("ROUTE|"))throw new IOException("Invalid native audio result.");
                        return reply.substring(6);
                    }
                    public String stopBrowser(long revision) throws Exception {
                        if(!preferences(context).getBoolean("browser_policy",false))return "DISABLED";
                        String reply=command("BROWSER",Long.toString(revision),1000);
                        if(!reply.startsWith("BROWSER|"))throw new IOException("Invalid browser cleanup result.");
                        return reply.substring(8);
                    }
                    public void report(long revision,String result) throws Exception {
                        android.util.Log.i("DXM-NativePolicy",result);
                        String encoded=android.util.Base64.encodeToString(result.getBytes(StandardCharsets.UTF_8),android.util.Base64.NO_WRAP);
                        command("REPORT",revision+" "+encoded,500);
                    }
                });
            }catch(Exception error){android.util.Log.w("DXM-NativePolicy","Retained policy skipped: "+error.getClass().getSimpleName());}
            finally{CACHE.expire();publish(context);finish();}
        }
    }
    static synchronized void complete(Context c,String token,boolean expired) {
        SharedPreferences p=preferences(c);
        if(!token.equals(p.getString("token","")))return;
        boolean restore=expired&&p.getBoolean("restore",false);
        String original=p.getString("original","");
        GuardianSessionStore.Session guardian=GuardianSessionStore.load(c);
        p.edit().clear().apply();CACHE.clear();
        Context densityContext=c.getApplicationContext();STATUS_EXECUTOR.execute(()->NativeDesktopDensity.finish(densityContext,token));
        publish(c);
        if(restore) {
            if(!SessionGuardianService.isPcConnected())restoreExpiredPower(c,original);
            else {
                Context app=c.getApplicationContext();
                Thread deferred=new Thread(()->{
                    for(int delay:new int[]{500,1500,8000}) {
                        try{Thread.sleep(delay);}catch(InterruptedException e){Thread.currentThread().interrupt();return;}
                        if(hasSession(app)||!guardian.matches(GuardianSessionStore.load(app)))return;
                        if(!SessionGuardianService.isPcConnected()) {
                            restoreExpiredPower(app,original);
                            SessionGuardianService.stopAfterManualCleanupIfIdle(app,CleanupCoordinator.inspect(app));return;
                        }
                    }
                },"DXM-Expired-Power-Restore");deferred.setDaemon(true);deferred.start();
            }
        }
    }
    private static void restoreExpiredPower(Context c,String original) {
        StayAwakeRepository.Snapshot current=StayAwakeRepository.inspect(c);
        if("7".equals(current.rawValue))StayAwakeRepository.restore(c,original);
    }
    static String request(Context c,String verb,String suffix,int timeout) throws IOException {
        return requestToken(preferences(c).getString("token",""),verb,suffix,timeout);
    }
    private static String requestToken(String token,String verb,String suffix,int timeout) throws IOException {
        if(android.os.Looper.myLooper()==android.os.Looper.getMainLooper())throw new IllegalStateException("Native control I/O requires a worker thread.");
        if(!token.matches("[0-9a-f]{32}"))throw new IOException("No native session is registered.");
        try(Socket socket=new Socket()) {
            socket.connect(new InetSocketAddress("127.0.0.1",PORT),500);socket.setSoTimeout(timeout);
            PrintWriter output=new PrintWriter(new OutputStreamWriter(socket.getOutputStream(),StandardCharsets.US_ASCII),true);
            output.println(verb+" "+token+(suffix.isEmpty()?"":" "+suffix));
            Reader input=new InputStreamReader(socket.getInputStream(),StandardCharsets.UTF_8);
            StringBuilder text=new StringBuilder();int ch;
            while((ch=input.read())!=-1 && ch!='\n') {if(ch!='\r')text.append((char)ch);if(text.length()>8192)throw new IOException("Native reply limit.");}
            String value=text.toString();
            if(!value.startsWith("DXND1|"+token+"|"))throw new IOException("Native session did not authenticate.");
            return value.substring(("DXND1|"+token+"|").length());
        }
    }
    static Snapshot inspect(Context c) {
        observeDisplayState(c);
        if(!hasSession(c))return new Snapshot(false,"",null);
        Context app=c.getApplicationContext();String token=preferences(c).getString("token","");
        Snapshot pending=new Snapshot(false,c.getString(R.string.native_display_checking),null);
        return CACHE.get(token,pending,key->probe(app,key),()->publish(app));
    }
    static void prepareDensity(Context c,String token,int dpi,android.content.BroadcastReceiver.PendingResult result){
        Context app=c.getApplicationContext();
        STATUS_EXECUTOR.execute(()->{
            try{
                if(!token.equals(preferences(app).getString("token","")))throw new IllegalStateException("Native ownership changed");
                NativeDesktopDensity.prepare(app,token,dpi);Thread.sleep(300);
                result.setResultCode(0);result.setResultData("DXNDPI1|"+dpi);
            }catch(Exception failure){NativeDesktopDensity.finish(app,token);result.setResultCode(1);result.setResultData("DXNDPI1|ERROR");}
            finally{result.finish();}
        });
    }
    static void observeDisplayState(Context c) {
        if(!OBSERVING.compareAndSet(false,true))return;
        Context app=c.getApplicationContext();
        STATUS_EXECUTOR.execute(()->{
            try {
                android.hardware.display.DisplayManager manager=(android.hardware.display.DisplayManager)app.getSystemService(Context.DISPLAY_SERVICE);
                if(manager==null){OBSERVING.set(false);return;}
                manager.registerDisplayListener(new android.hardware.display.DisplayManager.DisplayListener(){
                    public void onDisplayAdded(int id){refreshPresenceAsync(app);}
                    public void onDisplayRemoved(int id){refreshPresenceAsync(app);}
                    public void onDisplayChanged(int id){if(preferences(app).getInt("display_id",-1)==id)refreshPresenceAsync(app);}
                },new android.os.Handler(android.os.Looper.getMainLooper()));
                refreshPresenceAsync(app);
            }catch(RuntimeException e){OBSERVING.set(false);}
        });
    }
    static void refreshPresenceAsync(Context c) {
        if(!PRESENCE_QUEUED.compareAndSet(false,true))return;
        Context app=c.getApplicationContext();
        STATUS_EXECUTOR.execute(()->{
            try{if(hasSession(app))probe(app,preferences(app).getString("token",""));NativeDesktopDensity.reconcile(app);CACHE.expire();publish(app);}
            finally{PRESENCE_QUEUED.set(false);}
        });
    }
    private static Snapshot probe(Context c,String token) {
        if(!token.equals(preferences(c).getString("token","")))return new Snapshot(false,"",null,true,null);
        boolean matched=false,scanned=false,active=false,stopped=false;
        Boolean inUse=null;String error=null;
        try {
            android.hardware.display.DisplayManager manager=(android.hardware.display.DisplayManager)c.getSystemService(Context.DISPLAY_SERVICE);
            if(manager!=null){
                android.view.Display[] displays=manager.getDisplays();
                for(android.view.Display display:displays){
                    if(display.getDisplayId()==android.view.Display.DEFAULT_DISPLAY)scanned=true;
                    if(("DXM-Native-"+token.substring(0,12)).equals(display.getName())){
                        matched=true;
                        synchronized(NativeSessionBridge.class){if(token.equals(preferences(c).getString("token","")))preferences(c).edit().putBoolean("display_seen",true).putInt("display_id",display.getDisplayId()).apply();}
                    }
                }
            }
        }catch(RuntimeException ignored){}
        try {
            String value=requestToken(token,"STATUS","",700);
            if(!value.startsWith("STATE|"))throw new IOException("Invalid native status.");
            active=value.startsWith("STATE|ACTIVE|");stopped=value.startsWith("STATE|STOPPED|");
            if(active)NativeDesktopDensity.helperConfirmed(c,token);
            if(active)try{RetainedSessionPolicy.State state=RetainedSessionPolicy.parse(requestToken(token,"SESSION","",700));inUse=state.dex();}catch(IOException|IllegalArgumentException ignored){}
        }catch(IOException|RuntimeException e){error=e.getMessage();}
        NativeDisplayTruth.State truth=NativeDisplayTruth.resolve(active,stopped,matched,scanned,preferences(c).getBoolean("display_seen",false));
        if(truth==NativeDisplayTruth.State.ABSENT){
            complete(c,token,true);
            SessionGuardianService.stopAfterManualCleanupIfIdle(c,CleanupCoordinator.inspect(c));
            return new Snapshot(false,"",null,true,null);
        }
        if(truth==NativeDisplayTruth.State.ACTIVE)return new Snapshot(true,c.getString(R.string.native_display_active),null,true,inUse);
        return new Snapshot(false,c.getString(R.string.native_display_checking),error,false,null);
    }
    static boolean stop(Context c) {
        if(!hasSession(c))return true;
        String capturedToken=preferences(c).getString("token","");
        if(android.os.Looper.myLooper()==android.os.Looper.getMainLooper()) {
            Context app=c.getApplicationContext();
            if(STOP_QUEUED.compareAndSet(false,true))STATUS_EXECUTOR.execute(()->{
                try{stopCaptured(app,capturedToken);SessionGuardianService.stopAfterManualCleanupIfIdle(app,CleanupCoordinator.inspect(app));}
                finally{STOP_QUEUED.set(false);CACHE.invalidate();publish(app);}
            });
            return false; // Cleanup is pending; never report completion before the worker acknowledges it.
        }
        return stopCaptured(c,capturedToken);
    }
    private static boolean stopCaptured(Context c,String token) {
        if(!token.equals(preferences(c).getString("token","")))return false;
        try {
            boolean clean="STOPPED|1".equals(requestToken(token,"STOP","",17000));
            if(clean)complete(c,token,false);
            return clean;
        }catch(IOException e){probe(c,token);return !hasSession(c);}
    }
    static void updatePolicy(Context c) {
        if(!hasSession(c))return;
        for(int attempt=0;attempt<3;attempt++) {
            try {request(c,"POLICY",Integer.toString(NativeSessionRules.DISCONNECT_GRACE_SECONDS),1000);return;}
            catch(IOException ignored){if(attempt<2)try{Thread.sleep(300);}catch(InterruptedException e){Thread.currentThread().interrupt();return;}}
        }
    }
    static void updatePolicyAsync(Context c) {
        Context app=c.getApplicationContext();
        Thread worker=new Thread(()->updatePolicy(app),"DXM-Native-Policy");worker.setDaemon(true);worker.start();
    }
}

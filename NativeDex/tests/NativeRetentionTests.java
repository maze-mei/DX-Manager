import io.github.mazemei.dxmnativedex.*;
import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.*;

public final class NativeRetentionTests {
    private static int passed;
    private static final String TOKEN="0123456789abcdef0123456789abcdef";
    static void check(boolean ok,String name){if(!ok)throw new AssertionError(name);passed++;System.out.println("PASS "+name);}
    static long now(){return System.nanoTime()/1000000L;}
    public static int run() throws Exception {
        for(int legacy:new int[]{-1,0,30,60,300,600,1800}) {
            NativeRetentionState state=new NativeRetentionState(legacy,0);
            check(state.policy()==30,"Native grace stays30 regardless of Companion preference"+legacy);
            long session=state.attach(100);state.detach(session,200);
            state.policy(legacy);
            check(!state.tick(30199),"Native grace keeps the display until its deadline"+legacy);
            check(state.tick(30200),"Native grace ends at30 seconds"+legacy);
        }
        NativeRetentionState s=new NativeRetentionState(30,0);long owner=s.attach(0);s.detach(owner,1000);
        long restored=s.attach(30000);s.ping(restored,31000);
        check(!s.tick(32000),"Reconnection cancels the pending cleanup deadline");
        s.detach(owner,32001);check(s.connected(),"A stale old transport cannot detach the replacement");
        s.detach(restored,33000);s.policy(-1);
        check(!s.tick(62999),"Changing Companion to never does not restart the native grace");
        check(s.tick(63000),"Never-cleanup cannot extend the fixed native grace");
        s=new NativeRetentionState(0,0);
        check(!s.tick(44999),"Unattached bootstrap has its existing bounded allowance");
        check(s.tick(45000),"An abandoned unattached owner cannot remain indefinitely");
        s=new NativeRetentionState(30,0);owner=s.attach(0);
        check(!s.tick(5001)&&!s.connected(),"Missing heartbeat starts the native reconnect grace");
        s.ping(owner,5100);check(s.connected()&&!s.tick(5200),"A recovered heartbeat cancels expiry");
        s.stop("Windows shutdown");check(s.tick(5300),"Explicit shutdown remains immediate");
        try{NativeRetentionState.validateDelay(2);throw new AssertionError("Invalid policy accepted");}catch(IllegalArgumentException e){check(true,"Invalid native policy requests are rejected");}
        testControl();
        testPower();
        return passed;
    }
    static void testControl() throws Exception {
        NativeRetentionState state=new NativeRetentionState(300,now());
        java.util.Properties record=new java.util.Properties();record.setProperty("token",TOKEN);
        NativeAudioPolicyTests.Platform platform=new NativeAudioPolicyTests.Platform();
        try(NativeControlServer server=new NativeControlServer(TOKEN,state,(companion,restore,original)->NativePowerRecovery.configure(record,companion,restore,original),true,
            enabled->record.setProperty("feature",Boolean.toString(enabled)),new NativeAudioRouter(platform))) {
            server.ready("73|1600|900|150|1111|2222");
            String before=NativeControlServer.request(TOKEN,"STATUS",1000);
            check(before.contains("STATE|PENDING|73"),"Companion can inspect an existing pending display");
            check(!state.connected(),"A status request does not impersonate a PC attachment");
            try(Socket wrong=new Socket("127.0.0.1",NativeControlServer.PORT)) {
                wrong.setSoTimeout(1000);wrong.getOutputStream().write("STOP ffffffffffffffffffffffffffffffff\n".getBytes(StandardCharsets.US_ASCII));wrong.getOutputStream().flush();
                check(wrong.getInputStream().read()==-1&&state.reason()==null,"A foreign token cannot stop the display");
            }
            Socket first=new Socket("127.0.0.1",NativeControlServer.PORT);first.setSoTimeout(1000);
            PrintWriter output=new PrintWriter(first.getOutputStream(),true);output.println("ATTACH "+TOKEN);
            BufferedReader in=new BufferedReader(new InputStreamReader(first.getInputStream(),StandardCharsets.UTF_8));
            check(in.readLine().equals("DXND1|"+TOKEN+"|READY|73|1600|900|150|1111|2222"),"Authenticated attachment returns the original display and both owner PIDs");
            first.close();
            long until=now()+1000;while(state.connected()&&now()<until)Thread.sleep(10);
            check(!state.connected()&&!state.tick(now()),"Closing the ADB-forwarded socket retains the native engine");
            String policy;
            try(Socket c=new Socket("127.0.0.1",NativeControlServer.PORT)) {
                c.setSoTimeout(1000);new PrintWriter(c.getOutputStream(),true).println("POLICY "+TOKEN+" 60");
                policy=new BufferedReader(new InputStreamReader(c.getInputStream(),StandardCharsets.UTF_8)).readLine();
            }
            check(policy.equals("DXND1|"+TOKEN+"|POLICY|30")&&state.policy()==30,"Companion policy requests cannot change native grace");
            // request() is verb-only plus token; a POLICY value must follow the token.
            check(!state.connected(),"Companion requests leave the PC connection suspended");
            check(control("CONFIGURE "+TOKEN+" 300 companion 1 0 1").endsWith("CONFIGURED|30"),"Verified Companion feature configuration is acknowledged");
            try(Socket viewer=new Socket("127.0.0.1",NativeControlServer.PORT)) {
                viewer.setSoTimeout(1000);PrintWriter writer=new PrintWriter(viewer.getOutputStream(),true);
                BufferedReader reader=new BufferedReader(new InputStreamReader(viewer.getInputStream(),StandardCharsets.UTF_8));
                writer.println("ATTACH "+TOKEN);check(reader.readLine().contains("|READY|73|"),"A replacement PC controller attaches before publishing a viewer");
                writer.println("VIEWER "+TOKEN+" 1 1");check(reader.readLine().contains("|VIEWER|")&&state.snapshot().viewer&&state.snapshot().power,"Actual viewer/power state is authenticated on the attached PC channel");
            }
            until=now()+1000;while(state.connected()&&now()<until)Thread.sleep(10);
            long pendingRevision=state.snapshot().revision;
            check(NativeControlServer.request(TOKEN,"SESSION",1000).contains("|0|0|1|0|1|30"),"Companion reads current pending viewer state without polling the foreground app");
            check(control("ROUTE "+TOKEN+" "+(pendingRevision-1)+" DEX").endsWith("ROUTE|STALE"),"A stale routing request cannot undo a newer connection state");
            check(control("ROUTE "+TOKEN+" "+pendingRevision+" PHONE").endsWith("ROUTE|CHANGED")&&platform.current==2,"Companion can request phone routing through the native executor");
            platform.bluetooth=true;
            check(control("ROUTE "+TOKEN+" "+pendingRevision+" PHONE").endsWith("ROUTE|BLUETOOTH"),"Native execution rechecks Bluetooth after Companion's decision");
            String report=java.util.Base64.getEncoder().encodeToString("power=OFF audio=BLUETOOTH".getBytes(StandardCharsets.UTF_8));
            for(int i=0;i<35;i++){control("REPORT "+TOKEN+" "+pendingRevision+" "+report);Thread.sleep(2);}
            try(Socket replay=new Socket("127.0.0.1",NativeControlServer.PORT)) {
                replay.setSoTimeout(1000);new PrintWriter(replay.getOutputStream(),true).println("ATTACH "+TOKEN);
                BufferedReader reader=new BufferedReader(new InputStreamReader(replay.getInputStream(),StandardCharsets.UTF_8));int entries=0;String line;
                while((line=reader.readLine())!=null&&!line.contains("|READY|"))if(line.contains("|POLICY_LOG|"))entries++;
                check(entries==32,"Only the bounded recent offline policy reports replay to the PC log");
            }
            until=now()+1000;while(state.connected()&&now()<until)Thread.sleep(10);
            try(Socket c=new Socket("127.0.0.1",NativeControlServer.PORT)) {
                c.setSoTimeout(1000);new PrintWriter(c.getOutputStream(),true).println("CONFIGURE "+TOKEN+" 0 basic 1 missing");
                String reply=new BufferedReader(new InputStreamReader(c.getInputStream(),StandardCharsets.UTF_8)).readLine();
                check(reply.equals("DXND1|"+TOKEN+"|CONFIGURED|30")&&state.policy()==30,"Basic mode uses the same native grace without Companion");
                check(!NativePowerRecovery.usesCompanion(record)&&record.getProperty("power.original").equals("missing"),"Basic mode stores its own original power state without Companion");
            }
            check(control("ROUTE "+TOKEN+" "+state.snapshot().revision+" PHONE").endsWith("ROUTE|DISABLED"),"Basic-mode sessions reject Companion-only audio correction");
            try(Socket c=new Socket("127.0.0.1",NativeControlServer.PORT)) {
                c.setSoTimeout(1000);new PrintWriter(c.getOutputStream(),true).println("POLICY "+TOKEN+" -1");
                check(c.getInputStream().read()==-1&&state.policy()==30,"A stale Companion policy cannot enable retention in basic mode");
            }
            ExecutorService worker=Executors.newSingleThreadExecutor();
            try {
                Future<String> stop=worker.submit(()->NativeControlServer.request(TOKEN,"STOP",3000));
                until=now()+1000;while(state.reason()==null&&now()<until)Thread.sleep(10);
                check(state.reason()!=null,"An authenticated stop requests native engine termination");
                server.stopped(true);
                check(stop.get(2,TimeUnit.SECONDS).equals("DXND1|"+TOKEN+"|STOPPED|1"),"Stop is acknowledged only after cleanup confirms completion");
            }finally{worker.shutdownNow();}
        }
        try{NativeControlServer.readLine(new StringReader(new String(new char[300]).replace('\0','x')+"\n"),256);throw new AssertionError("Limit not enforced");}catch(IOException e){check(true,"Control framing is bounded before allocation growth");}
    }
    static String control(String command) throws Exception {
        try(Socket socket=new Socket("127.0.0.1",NativeControlServer.PORT)) {
            socket.setSoTimeout(1500);new PrintWriter(socket.getOutputStream(),true).println(command);
            return new BufferedReader(new InputStreamReader(socket.getInputStream(),StandardCharsets.UTF_8)).readLine();
        }
    }
    static void testPower() throws Exception {
        java.util.Properties record=new java.util.Properties();record.setProperty("token",TOKEN);
        class Store implements NativePowerRecovery.Settings {
            String value="7",restored;int reads;
            public String read(){reads++;return value;}
            public void restore(String original){restored=original;}
        }
        Store store=new Store();NativePowerRecovery.configure(record,false,true,"0");
        NativePowerRecovery.restore(record,TOKEN,store);
        check("0".equals(store.restored),"Offline basic cleanup restores DX Manager's captured power baseline");
        store=new Store();NativePowerRecovery.configure(record,false,true,"missing");NativePowerRecovery.restore(record,TOKEN,store);
        check("missing".equals(store.restored),"An absent original setting remains a deletion rather than zero");
        store=new Store();store.value="3";NativePowerRecovery.restore(record,TOKEN,store);
        check(store.restored==null,"Fallback cleanup preserves a later user power-setting change");
        store=new Store();NativePowerRecovery.configure(record,false,false,"0");NativePowerRecovery.restore(record,TOKEN,store);
        check(store.reads==0&&store.restored==null,"The existing disable-stay-awake-on-stop preference is respected");
        store=new Store();NativePowerRecovery.configure(record,true,true,"0");NativePowerRecovery.restore(record,TOKEN,store);
        check(store.reads==0,"Verified Companion power recovery keeps its existing path");
        store=new Store();NativePowerRecovery.configure(record,false,true,"0");NativePowerRecovery.restore(record,"ffffffffffffffffffffffffffffffff",store);
        check(store.reads==0,"A different session cannot restore this phone's saved power state");
        store=new Store();NativePowerRecovery.restore(new java.util.Properties(),TOKEN,store);
        check(store.reads==0,"A missing recovery record never changes power settings");
        try{NativePowerRecovery.configure(record,false,true,"0;reboot");throw new AssertionError("Invalid power value accepted");}catch(IllegalArgumentException e){check(true,"Recovery values cannot contain shell fragments");}
        try{NativePowerRecovery.configure(record,false,true,"2147483648");throw new AssertionError("Overflow accepted");}catch(IllegalArgumentException e){check(true,"Power setting bounds match Android integer values");}
    }
}

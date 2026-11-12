import io.github.mazemei.dxmnativedex.NativeIdentity;
import io.github.mazemei.dxmnativedex.RtspMessageReader;
import com.scrcpydex.server.DexActivator;
import com.scrcpydex.server.wrappers.DisplayManager;
import java.io.*;
import java.lang.reflect.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.atomic.AtomicBoolean;

public final class NativeDexTests {
    static int passed;
    static final String TOKEN = "0123456789abcdef0123456789abcdef";
    static void check(boolean value, String name) {
        if (!value) throw new AssertionError(name);
        passed++; System.out.println("PASS " + name);
    }
    interface Throwing { void run() throws Exception; }
    static void fails(Throwing action, String name) throws Exception {
        try { action.run(); } catch (Exception e) { check(true, name); return; }
        throw new AssertionError(name);
    }
    static BufferedReader input(String s) { return new BufferedReader(new StringReader(s)); }
    static void send(OutputStream out, String line) throws Exception { out.write(line.getBytes(StandardCharsets.US_ASCII)); out.flush(); }
    static void field(Object object, String name, Object value) throws Exception {
        Field f = object.getClass().getDeclaredField(name);f.setAccessible(true);f.set(object,value);
    }
    static Object get(Object object, String name) throws Exception {
        Field f=object.getClass().getDeclaredField(name);f.setAccessible(true);return f.get(object);
    }
    static Object invoke(Object o, String name, Class<?>[] types, Object... args) throws Exception {
        Method m=o.getClass().getDeclaredMethod(name,types);m.setAccessible(true);
        try { return m.invoke(o,args); } catch (InvocationTargetException e) { throw (Exception)e.getCause(); }
    }
    public static final class Peer {
        String address; Peer(String address){this.address=address;}
        public String getDeviceAddress(){return address;}
    }
    public static final class Status {
        int state;Peer peer;
        public int getActiveDisplayState(){return state;}
        public Peer getActiveDisplay(){return peer;}
    }
    public static final class Manager {
        Status status=new Status();int disconnects;
        public Status getWifiDisplayStatus(){return status;}
        public void disconnectWifiDisplay(){disconnects++;status.state=0;}
        public void connectWifiDisplayWithConfig(Object config,Object callback){}
    }

    public static void main(String[] args) throws Exception {
        check(NativeIdentity.name(TOKEN).equals("DXM-Native-0123456789ab"),"Unique display name");
        check(NativeIdentity.address(TOKEN).equals("02:01:23:45:67:89"),"Stable local peer identity");
        check(NativeIdentity.matches(TOKEN,"02:01:23:45:67:89"),"Peer match");
        check(!NativeIdentity.matches(TOKEN,"00:11:22:33:44:55"),"Foreign peer is rejected");
        fails(()->NativeIdentity.validate(null),"Null token rejected");
        fails(()->NativeIdentity.validate("abcd"),"Short token rejected");
        fails(()->NativeIdentity.validate("0123456789ABCDEF0123456789ABCDEF"),"Noncanonical token rejected");
        RtspMessageReader.Message message=RtspMessageReader.read(input("GET_PARAMETER rtsp://local RTSP/1.0\r\ncSeQ: 7\r\nContent-Length: 3\r\n\r\nabc"));
        check(message.cseq.equals("7")&&message.body.equals("abc"),"Case-insensitive headers and exact body");
        message=RtspMessageReader.read(input("RTSP/1.0 200 OK\r\nCSeq: 9\r\nSession: xyz;timeout=30\r\n\r\n"));
        check(message.session.equals("xyz"),"RTSP session parameters separated");
        BufferedReader multi=input("OPTIONS * RTSP/1.0\r\nCSeq: 1\r\n\r\nGET_PARAMETER * RTSP/1.0\r\nCSeq: 2\r\nContent-Length: 1\r\n\r\nx");
        check(RtspMessageReader.read(multi).firstLine.startsWith("OPTIONS"),"First concatenated RTSP frame");
        check(RtspMessageReader.read(multi).body.equals("x"),"Next concatenated RTSP frame");
        check(RtspMessageReader.read(multi)==null,"Clean RTSP EOF");
        fails(()->RtspMessageReader.read(input("OPTIONS * RTSP/1.0\r\nContent-Length: -1\r\n\r\n")),"Negative body length rejected");
        fails(()->RtspMessageReader.read(input("OPTIONS * RTSP/1.0\r\nContent-Length: 65537\r\n\r\n")),"Oversized body rejected before allocation");
        fails(()->RtspMessageReader.read(input("OPTIONS * RTSP/1.0\r\nContent-Length: 1\r\nContent-Length: 1\r\n\r\nx")),"Duplicate content length rejected");
        fails(()->RtspMessageReader.read(input("OPTIONS * RTSP/1.0\r\nContent-Length: 5\r\n\r\nx")),"Truncated body rejected");
        fails(()->RtspMessageReader.read(input("OPTIONS * RTSP/1.0\r\nCSeq: invalid\r\n\r\n")),"Invalid response sequence rejected");
        fails(()->RtspMessageReader.read(input("OPTIONS * RTSP/1.0\r\nbroken-header\r\n\r\n")),"Malformed header rejected");
        StringBuilder longLine=new StringBuilder();for(int i=0;i<5000;i++)longLine.append('x');
        fails(()->RtspMessageReader.read(input(longLine+"\r\n")),"Bounded RTSP first line");
        StringBuilder headers=new StringBuilder("OPTIONS * RTSP/1.0\r\n");
        for(int i=0;i<1000;i++)headers.append("X-Test: 0123456789012345678901234567890123456789\r\n");
        headers.append("\r\n");
        fails(()->RtspMessageReader.read(input(headers.toString())),"Bounded aggregate RTSP headers");

        Manager raw=new Manager();DisplayManager manager=new DisplayManager(raw);
        manager.requireIdle();check(raw.disconnects==0,"Idle preflight does not disconnect");
        raw.status.state=2;raw.status.peer=new Peer("aa:bb:cc:dd:ee:ff");
        fails(()->manager.requireIdle(),"An existing receiver blocks activation");
        check(raw.disconnects==0,"Existing receiver preserved during preflight");
        check(manager.disconnectOwned(NativeIdentity.address(TOKEN))&&raw.disconnects==0,"Foreign receiver preserved during cleanup");
        raw.status.peer=null;
        check(!manager.disconnectOwned(NativeIdentity.address(TOKEN))&&raw.disconnects==0,"Unknown receiver ownership fails closed");
        raw.status.peer=new Peer(NativeIdentity.address(TOKEN));
        check(manager.disconnectOwned(NativeIdentity.address(TOKEN))&&raw.disconnects==1,"Only owned receiver is disconnected");
        check(manager.disconnectOwned(NativeIdentity.address(TOKEN))&&raw.disconnects==1,"Owned cleanup is idempotent");
        testNegotiation();
        testRtpAndCancellation();
        passed+=NativeRetentionTests.run();
        passed+=NativeAudioPolicyTests.run();
        passed+=NativeSessionFallbackTests.run();
        passed+=NativeBluetoothPreserverTests.run();
        passed+=NativeBrowserCleanupTests.run();
        passed+=NativeMetricsVerifierTests.run();
        System.out.println("All native Java tests passed: "+passed);
    }

    static void testNegotiation() throws Exception {
        Manager raw=new Manager();raw.status.state=2;raw.status.peer=new Peer(NativeIdentity.address(TOKEN));
        DexActivator a=new DexActivator(new DisplayManager(raw),NativeIdentity.name(TOKEN),NativeIdentity.address(TOKEN));
        AtomicBoolean running=(AtomicBoolean)get(a,"running");running.set(true);
        field(a,"rtpVideoPort",54000);field(a,"rtpAudioPort",54001);
        try(ServerSocket server=new ServerSocket(0,1,InetAddress.getLoopbackAddress());
            Socket client=new Socket(InetAddress.getLoopbackAddress(),server.getLocalPort());
            Socket source=server.accept()){
            source.setSoTimeout(3000);
            field(a,"rtspSocket",client);
            Thread worker=new Thread(()->{try{invoke(a,"runRtspStateMachine",new Class<?>[0]);}catch(Exception e){throw new RuntimeException(e);}});
            worker.start();
            BufferedReader input=new BufferedReader(new InputStreamReader(source.getInputStream(),StandardCharsets.US_ASCII));
            OutputStream output=source.getOutputStream();
            send(output,"OPTIONS * RTSP/1.0\r\nCSeq: 1\r\n\r\n");
            check(RtspMessageReader.read(input).firstLine.equals("RTSP/1.0 200 OK"),"WFD OPTIONS response");
            RtspMessageReader.Message options=RtspMessageReader.read(input);
            check(options.firstLine.startsWith("OPTIONS"),"WFD reciprocal OPTIONS request");
            send(output,"RTSP/1.0 200 OK\r\nCSeq: "+options.cseq+"\r\n\r\n");
            String capabilities="wfd_video_formats\r\n";
            send(output,"GET_PARAMETER * RTSP/1.0\r\nCSeq: 2\r\nContent-Length: "+capabilities.length()+"\r\n\r\n"+capabilities);
            check(RtspMessageReader.read(input).body.contains("wfd_client_rtp_ports: RTP/AVP/UDP;unicast 54000"),"WFD capabilities preserve the configured RTP port");
            String body="wfd_presentation_URL: rtsp://127.0.0.1/wfd1.0/streamid=0 none\r\nwfd_trigger_method: SETUP\r\n";
            send(output,"SET_PARAMETER * RTSP/1.0\r\nCSeq: 3\r\nContent-Length: "+body.length()+"\r\n\r\n"+body);
            check(RtspMessageReader.read(input).cseq.equals("3"),"SET_PARAMETER acknowledgement");
            RtspMessageReader.Message setup=RtspMessageReader.read(input);
            check(setup.firstLine.startsWith("SETUP rtsp://127.0.0.1/"),"SETUP uses the advertised presentation URL");
            send(output,"RTSP/1.0 200 OK\r\nCSeq: "+setup.cseq+"\r\nSession: test-session;timeout=30\r\n\r\n");
            RtspMessageReader.Message play=RtspMessageReader.read(input);
            check(play.firstLine.startsWith("PLAY ")&&play.session.equals("test-session"),"PLAY uses the negotiated session");
            CountDownLatch ready=(CountDownLatch)get(a,"rtspPlayCompleted");
            send(output,"RTSP/1.0 200 OK\r\nCSeq: 999\r\n\r\n");
            Thread.sleep(100);
            check(ready.getCount()==1,"Unrelated 200 response cannot mark DeX ready");
            send(output,"RTSP/1.0 200 OK\r\nCSeq: "+play.cseq+"\r\n\r\n");
            check(ready.await(2,java.util.concurrent.TimeUnit.SECONDS),"Matching PLAY acknowledgement marks DeX ready");
            a.stop();worker.join(2000);
            check(!worker.isAlive(),"Stop closes the RTSP reader promptly");
        }
    }

    static void testRtpAndCancellation() throws Exception {
        Manager raw=new Manager();
        DexActivator a=new DexActivator(new DisplayManager(raw),NativeIdentity.name(TOKEN),NativeIdentity.address(TOKEN));
        ((AtomicBoolean)get(a,"running")).set(true);
        int port=(Integer)invoke(a,"openUdpDrain",new Class<?>[0]);
        fails(()->{try(DatagramSocket busy=new DatagramSocket(port,InetAddress.getLoopbackAddress())){}},"RTP drain owns its allocated UDP port");
        a.stop();
        try(DatagramSocket free=new DatagramSocket(port,InetAddress.getLoopbackAddress())){check(true,"Stop releases the UDP socket");}
        long started=System.nanoTime();
        Object socket=invoke(a,"connectRtspLoopback",new Class<?>[]{int.class,int.class},1,6000);
        check(socket==null&&(System.nanoTime()-started)<1000000000L,"Canceled startup does not continue retrying");
        ((AtomicBoolean)get(a,"running")).set(true);
        try(ServerSocket busy=new ServerSocket(0,1,InetAddress.getLoopbackAddress())){
            fails(()->invoke(a,"waitForPortFree",new Class<?>[]{int.class,int.class},busy.getLocalPort(),250),"Busy RTSP port refuses activation");
            check(raw.disconnects==0,"Port conflict never disconnects another receiver");
        }
        a.stop();
    }
}

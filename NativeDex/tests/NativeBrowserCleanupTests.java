import io.github.mazemei.dxmnativedex.*;
import java.util.concurrent.atomic.*;
import java.net.*;
import java.io.*;

public final class NativeBrowserCleanupTests {
 static int passed;
 static final String TOKEN="0123456789abcdef0123456789abcdef";
 static void check(boolean ok,String name){if(!ok)throw new AssertionError(name);passed++;System.out.println("PASS "+name);}
 static NativeRetentionState enabled(){NativeRetentionState s=new NativeRetentionState(300,0);s.enablePolicy(true);s.enableBrowserPolicy(true);return s;}
 public static int run() throws Exception {
  AtomicInteger calls=new AtomicInteger();AtomicBoolean owned=new AtomicBoolean(true);
  NativeBrowserCleanup browser=new NativeBrowserCleanup((pkg,user)->{check(pkg.equals("com.sec.android.app.sbrowser")&&user==-2,"Browser executor has a fixed Samsung package and current-user target");calls.incrementAndGet();},owned::get);
  NativeRetentionState s=enabled();long epoch=s.attach(0);
  check(s.stopBrowser(s.snapshot().revision,browser::stop).equals("STALE")&&calls.get()==0,"A connected PC never authorizes disconnect cleanup");
  s.detach(epoch,1);check(s.stopBrowser(s.snapshot().revision,browser::stop).equals("NOT_PENDING"),"Control-only connections cannot terminate a phone browser");
  epoch=s.attach(2);s.viewer(epoch,true,true);s.detach(epoch,3);long revision=s.snapshot().revision;
  check(s.stopBrowser(revision,browser::stop).equals("STOP_REQUESTED")&&calls.get()==1,"Actual-viewer disconnect authorizes one browser stop");
  check(s.stopBrowser(revision,browser::stop).equals("ALREADY_HANDLED")&&calls.get()==1,"Duplicate Companion events do not repeat force-stop");
  s.policy(600);check(s.stopBrowser(s.snapshot().revision,browser::stop).equals("ALREADY_HANDLED"),"Changing cleanup delay cannot replay an already handled disconnect");
  epoch=s.attach(4);s.viewer(epoch,true,true);s.detach(epoch,5);revision=s.snapshot().revision;
  long replacement=s.attach(6);check(s.stopBrowser(revision,browser::stop).equals("STALE")&&calls.get()==1,"A late disconnect request cannot kill a newly reconnected browser");
  s.detach(replacement,7);check(s.stopBrowser(s.snapshot().revision,browser::stop).equals("NOT_PENDING"),"A later control-only disconnect remains harmless");
  epoch=s.attach(8);s.viewer(epoch,true,true);s.detach(epoch,9);owned.set(false);
  check(s.stopBrowser(s.snapshot().revision,browser::stop).equals("NOT_OWNED")&&calls.get()==1,"Lost native display ownership prevents browser termination");owned.set(true);
  s=enabled();epoch=s.attach(0);s.viewer(epoch,true,true);s.policy(0);s.detach(epoch,1);
  check(s.stopBrowser(s.snapshot().revision,browser::stop).equals("STOP_REQUESTED"),"Companion immediate setting cannot disable native grace browser handling");
  s=enabled();epoch=s.attach(0);s.viewer(epoch,true,true);s.enableBrowserPolicy(false);s.detach(epoch,1);
  check(s.stopBrowser(s.snapshot().revision,browser::stop).equals("DISABLED"),"Older Companion capability does not enable browser cleanup");
  s=enabled();epoch=s.attach(0);s.viewer(epoch,true,true);s.detach(epoch,1);s.stop("Explicit stop");
  check(s.stopBrowser(s.snapshot().revision,browser::stop).equals("DISABLED"),"Normal DeX termination does not run disconnect-only browser cleanup");
  s=enabled();epoch=s.attach(0);s.viewer(epoch,true,true);s.detach(epoch,1);AtomicInteger failed=new AtomicInteger();
  try{s.stopBrowser(s.snapshot().revision,()->{failed.incrementAndGet();throw new IOException("Binder failure");});throw new AssertionError();}catch(IOException expected){}
  check(s.stopBrowser(s.snapshot().revision,()->"BAD").equals("ALREADY_HANDLED")&&failed.get()==1,"A failed stop attempt is not retried indefinitely");
  endpoint(browser,calls);return passed;
 }
 static String request(String line) throws Exception {
  try(Socket socket=new Socket("127.0.0.1",NativeControlServer.PORT)){
   socket.setSoTimeout(1000);new PrintWriter(socket.getOutputStream(),true).println(line);
   return new BufferedReader(new InputStreamReader(socket.getInputStream())).readLine();
  }
 }
 static void endpoint(NativeBrowserCleanup browser,AtomicInteger calls) throws Exception {
  NativeRetentionState state=new NativeRetentionState(300,System.nanoTime()/1000000L);
  try(NativeControlServer server=new NativeControlServer(TOKEN,state,(a,b,c)->{},true,null,null,browser)){
   server.ready("73|1600|900|150|1111|2222");int before=calls.get();
   check(request("BROWSER ffffffffffffffffffffffffffffffff 1")==null&&calls.get()==before,"Browser commands require the owning session token");
   check(request("CONFIGURE "+TOKEN+" 300 companion 1 0 1 1").endsWith("CONFIGURED|30"),"New Companion browser capability is negotiated explicitly");
   try(Socket socket=new Socket("127.0.0.1",NativeControlServer.PORT)){
    socket.setSoTimeout(1000);PrintWriter out=new PrintWriter(socket.getOutputStream(),true);BufferedReader in=new BufferedReader(new InputStreamReader(socket.getInputStream()));
    out.println("ATTACH "+TOKEN);check(in.readLine().contains("|READY|73|"),"Browser policy uses the actual attached display");
    out.println("VIEWER "+TOKEN+" 1 1");check(in.readLine().contains("|VIEWER|"),"An actual viewer is recorded before disconnect");
   }
   long end=System.nanoTime()+1000000000L;while(state.connected()&&System.nanoTime()<end)Thread.sleep(5);
   long revision=state.snapshot().revision;
   check(request("BROWSER "+TOKEN+" "+revision+" com.other.app")==null&&calls.get()==before,"The protocol cannot accept an arbitrary package name");
   check(request("BROWSER "+TOKEN+" "+revision).endsWith("BROWSER|STOP_REQUESTED")&&calls.get()==before+1,"Authenticated offline browser cleanup reaches the fixed executor");
   check(request("BROWSER "+TOKEN+" "+revision).endsWith("BROWSER|ALREADY_HANDLED"),"The real endpoint deduplicates the disconnect action");
   request("CONFIGURE "+TOKEN+" 0 basic 1 0 0 0");
   check(request("BROWSER "+TOKEN+" "+state.snapshot().revision).endsWith("BROWSER|DISABLED"),"Basic/no-Companion mode rejects browser cleanup");
  }
 }
}

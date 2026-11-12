import io.github.mazemei.dxmnativedex.*;
import java.util.*;
import java.net.*;
import java.io.*;
public final class NativeMetricsVerifierTests {
 static int passed;
 static void check(boolean ok,String s){if(!ok)throw new AssertionError(s);passed++;System.out.println("PASS "+s);}
 static final class Fake implements NativeMetricsVerifier.Platform {
  long time;boolean owned=true,noEffect,throwSet,foreign,trackEffective;int sets,sizes,reads,readCost,effective=150,defaultDpi=150,geometryAtRead,foreignAtRead,ownershipAtRead;
  final Deque<Integer> values=new ArrayDeque<>();final List<String> notes=new ArrayList<>();
  public long now(){return time;}public boolean owned(){return owned;}public void pause(long ms){time+=ms;}
  public void size(int w,int h){sizes++;}public void density(int d) throws Exception {sets++;if(throwSet){time+=1200;throw new IOException("synthetic wm rejection");}}
  public NativeMetricsVerifier.Metrics read(){reads++;time+=readCost;int d=values.isEmpty()?(noEffect?213:defaultDpi):values.remove();if(ownershipAtRead>0&&reads>=ownershipAtRead)owned=false;return new NativeMetricsVerifier.Metrics(foreign||(foreignAtRead>0&&reads>=foreignAtRead)?99:42,geometryAtRead>0&&reads>=geometryAtRead?1920:1600,900,d,noEffect?213:trackEffective?d:effective);}
  public void note(String kind,String value){notes.add(kind+" "+value);}
 }
 public static int run() throws Exception {
  check(NativeMetrics.effectiveDensity("Physical density: 213\nOverride density: 150\n")==150,"Production parser selects the forced density");
  check(NativeMetrics.effectiveDensity("Physical density: 213\n")==213,"Production parser detects an unoverridden physical density");
  check(NativeMetrics.effectiveDensity("unavailable")==-1,"Unknown wm output is not confirmed density");
  Fake f=new Fake();NativeMetricsVerifier.Metrics m=NativeMetricsVerifier.apply(f,42,1600,900,150);
  check(m.dpi==150&&f.reads==2&&f.sets==1,"Creation requires two matching logical/forced density samples");
  check(f.sizes==1,"A normal density confirmation does not repeatedly resize the display");
  f=new Fake();f.values.addAll(Arrays.asList(213,213,150,150));NativeMetricsVerifier.apply(f,42,1600,900,150);
  check(f.sets==1&&f.notes.stream().anyMatch(s->s.contains("actual=42:1600x900@213")),"Late Android initialization is observed and logged before confirming150");
  f=new Fake();for(int i=0;i<8;i++)f.values.add(213);NativeMetricsVerifier.apply(f,42,1600,900,150);
  check(f.sets==2,"A persistent initial213 is followed by one bounded density reapplication");
  f=new Fake();f.noEffect=true;
  try{NativeMetricsVerifier.apply(f,42,1600,900,150);throw new AssertionError("mismatch accepted");}catch(IllegalStateException expected){}
  check(f.sets==3&&f.time<=8000,"An uncorrectable density never retries beyond three attempts or reports success");
  check(f.notes.stream().anyMatch(s->s.startsWith("error")),"Final mismatch emits requested/actual error evidence");
  f=new Fake();f.throwSet=true;
  try{NativeMetricsVerifier.apply(f,42,1600,900,150);throw new AssertionError();}catch(IllegalStateException expected){}
  check(f.sets==3&&f.notes.stream().anyMatch(s->s.contains("IOException")),"Rejected wm commands are bounded and diagnosed");
  f=new Fake();f.owned=false;
  try{NativeMetricsVerifier.apply(f,42,1600,900,150);throw new AssertionError();}catch(IllegalStateException expected){}
  check(f.sets==0&&f.sizes==0,"Lost ownership cannot send any metrics command");
  f=new Fake();f.foreign=true;
  try{NativeMetricsVerifier.apply(f,42,1600,900,150);throw new AssertionError();}catch(IllegalStateException expected){}
  check(f.sets==1,"A changed display identity aborts rather than retrying another display");
  f=new Fake();
  try{NativeMetricsVerifier.apply(f,0,1600,900,150);throw new AssertionError();}catch(IllegalArgumentException expected){}
  check(f.sizes==0,"Display0 is never a metrics target");
  Fake initial=new Fake();initial.effective=160;initial.values.add(160);initial.values.add(160);
  NativeMetricsVerifier.verify(initial,42,1600,900,160);
  check(initial.sizes==0&&initial.sets==0,"Initial creation verification never writes size or density");
  initial=new Fake();initial.noEffect=true;
  try{NativeMetricsVerifier.verify(initial,42,1600,900,160);throw new AssertionError();}catch(IllegalStateException expected){}
  check(initial.sizes==0&&initial.sets==0,"An unconfirmed initial mode fails without a live resize fallback");
  int vesa=Integer.parseUnsignedInt(NativeInitialMode.formats(1600,900).split(" ")[5],16);
  check((vesa&(1<<20))!=0&&(vesa&(1<<21))!=0,"Initial1600 includes both30/60fps so a30-only source can select the target");
  int cea=Integer.parseUnsignedInt(NativeInitialMode.formats(1920,1080).split(" ")[4],16);
  check((cea&(1<<7))!=0&&(cea&(1<<8))!=0,"FHD capability accepts a1080p30 source as well as1080p60");
  check(NativeInitialMode.formats(1920,1080).equals(com.scrcpydex.server.DexActivator.DEFAULT_VIDEO_FORMATS),"FHD uses the known original interoperability capability set");
  try{NativeInitialMode.formats(3840,2160);throw new AssertionError();}catch(IllegalArgumentException expected){check(true,"Unvalidated initial modes are never advertised as supported");}
  initialThenLower();failedReplay();return passed;
 }
 static Fake lowDpiFake(){Fake f=new Fake();f.trackEffective=true;f.values.addAll(Arrays.asList(160,160,160,150,150));return f;}
 static void initialThenLower()throws Exception{
  check(NativeInitialMode.initialDpi(120)==160&&NativeInitialMode.initialDpi(150)==160,"Low requested DPI uses160 for initial Samsung creation only");
  check(NativeInitialMode.initialDpi(160)==160&&NativeInitialMode.initialDpi(240)==240,"DPI160 and higher keep their initial preference");
  Fake f=lowDpiFake();NativeMetricsVerifier.Metrics m=NativeMetricsVerifier.initialize(f,42,1600,900,150);
  check(m.matches(42,1600,900,150)&&f.reads==5&&f.sets==1,"Initial160 and final150 are independently confirmed before READY");
  check(f.sizes==0,"Lowering160 to150 never sends a size setter");
  check(f.notes.stream().anyMatch(s->s.contains("initial DPI 160 confirmed")),"The initial/final density transition is diagnosed explicitly");
  f=new Fake();f.trackEffective=true;f.defaultDpi=160;NativeMetricsVerifier.initialize(f,42,1600,900,160);
  check(f.sets==0&&f.sizes==0,"A160 request uses read-only initialization without any final density write");
  f=new Fake();f.noEffect=true;
  try{NativeMetricsVerifier.initialize(f,42,1600,900,150);throw new AssertionError();}catch(IllegalStateException expected){}
  check(f.sets==0&&f.sizes==0,"Initial213 cannot be accepted as160 or bypassed by a final150 write");
  f=lowDpiFake();f.geometryAtRead=3;
  try{NativeMetricsVerifier.initialize(f,42,1600,900,150);throw new AssertionError();}catch(IllegalStateException expected){}
  check(f.sets==0&&f.sizes==0,"Geometry drift before final DPI aborts without resizing or writing density");
  f=lowDpiFake();f.geometryAtRead=4;
  try{NativeMetricsVerifier.initialize(f,42,1600,900,150);throw new AssertionError();}catch(IllegalStateException expected){}
  check(f.sets==1&&f.sizes==0,"Geometry drift during final DPI verification is not repaired by a hidden resize");
  f=lowDpiFake();f.foreignAtRead=3;
  try{NativeMetricsVerifier.initialize(f,42,1600,900,150);throw new AssertionError();}catch(IllegalStateException expected){}
  check(f.sets==0&&f.sizes==0,"A replacement display cannot receive final150 after initial verification");
  f=lowDpiFake();f.ownershipAtRead=2;
  try{NativeMetricsVerifier.initialize(f,42,1600,900,150);throw new AssertionError();}catch(IllegalStateException expected){}
  check(f.sets==0&&f.sizes==0,"Lost ownership between initial and final DPI prevents all setters");
  f=new Fake();f.trackEffective=true;f.defaultDpi=160;
  try{NativeMetricsVerifier.initialize(f,42,1600,900,150);throw new AssertionError();}catch(IllegalStateException expected){}
  check(f.sets==3&&f.sizes==0&&f.time<=NativeMetricsVerifier.BUDGET_MS,"Unconfirmed final150 has bounded retries and never resizes");
  f=new Fake();f.trackEffective=true;f.defaultDpi=160;for(int i=0;i<43;i++)f.values.add(213);f.values.addAll(Arrays.asList(160,160,160));
  try{NativeMetricsVerifier.initialize(f,42,1600,900,150);throw new AssertionError();}catch(IllegalStateException expected){}
  check(f.sets<=2&&f.time<=NativeMetricsVerifier.BUDGET_MS,"Initial settling and final DPI share one finite time budget");
  f=lowDpiFake();
  try{NativeMetricsVerifier.initialize(f,0,1600,900,150);throw new AssertionError();}catch(IllegalArgumentException expected){}
  check(f.sets==0&&f.sizes==0,"The combined initial/final path cannot target phone display0");
 }
 static void failedReplay() throws Exception {
  String token="0123456789abcdef0123456789abcdef";
  try(NativeControlServer server=new NativeControlServer(token,new NativeRetentionState(300,0))){
   server.diagnostic("Metrics warning requested=150 actual=213");server.failed("Native metrics not confirmed");
   try(Socket socket=new Socket("127.0.0.1",NativeControlServer.PORT)){
    socket.setSoTimeout(1000);new PrintWriter(socket.getOutputStream(),true).println("ATTACH "+token);BufferedReader in=new BufferedReader(new InputStreamReader(socket.getInputStream()));
    check(in.readLine().contains("|POLICY_LOG|"),"Failed startup replays the metrics evidence before its error");
    check(in.readLine().contains("|ERROR|"),"Failed startup never emits READY with an unverified DPI");
   }
  }
 }
}

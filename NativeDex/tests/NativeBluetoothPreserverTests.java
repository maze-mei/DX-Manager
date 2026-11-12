import io.github.mazemei.dxmnativedex.*;
import java.util.*;
import java.util.concurrent.atomic.AtomicBoolean;

public final class NativeBluetoothPreserverTests {
 static int passed;
 static void check(boolean ok,String name){if(!ok)throw new AssertionError(name);passed++;System.out.println("PASS "+name);}
 static final NativeBluetoothPreserver.Route BT=new NativeBluetoothPreserver.Route(8,128,"test-headset");
 static final NativeBluetoothPreserver.Route OTHER=new NativeBluetoothPreserver.Route(8,128,"other-headset");
 static final NativeBluetoothPreserver.Route DEX=new NativeBluetoothPreserver.Route(25,32768,"0");
 static final NativeBluetoothPreserver.Route PHONE=new NativeBluetoothPreserver.Route(2,2,"");
 static final class Platform implements NativeBluetoothPreserver.Platform {
  NativeBluetoothPreserver.Route[] current={BT},outputs={PHONE,BT};
  Runnable callback;int reads,listens,unlistens,selects,result;
  boolean failListener,failSelect; Runnable beforeSecondRead; int triggerRead=-1;
  public NativeBluetoothPreserver.Route[] routes(){reads++;if(reads==triggerRead&&beforeSecondRead!=null)beforeSecondRead.run();return current;}
  public NativeBluetoothPreserver.Route[] connected(){return outputs;}
  public void listen(Runnable r) throws Exception {callback=r;listens++;if(failListener)throw new Exception("unavailable");}
  public void unlisten(){callback=null;unlistens++;}
  public int select(int type,String address) throws Exception {selects++;check(type==BT.internalType&&address.equals(BT.address),"Restoration addresses the exact pre-DeX output");if(failSelect)throw new Exception("reply lost");return result;}
  void change(NativeBluetoothPreserver.Route... value){current=value;if(callback!=null)callback.run();}
 }
 static NativeBluetoothPreserver make(Platform p,AtomicBoolean owned,List<String> logs){return new NativeBluetoothPreserver(p,owned::get,logs::add);}
 public static int run() {
  Platform p=new Platform();AtomicBoolean owned=new AtomicBoolean(true);List<String> logs=new ArrayList<>();
  NativeBluetoothPreserver b=make(p,owned,logs);b.capture(false);b.ready(0);b.tick(10);
  check(p.reads==0&&p.listens==0&&p.selects==0,"Option OFF does not query, listen or route audio");
  p=new Platform();p.current=new NativeBluetoothPreserver.Route[]{PHONE};b=make(p,owned,logs);b.capture(true);p.change(DEX);b.ready(0);
  check(p.selects==0&&p.unlistens==1,"An idle connected headset is not mistaken for the selected media output");
  p=new Platform();b=make(p,owned,logs);b.capture(true);p.change(DEX);b.tick(0);
  check(p.selects==0,"A callback before owned-display READY cannot switch audio");b.ready(10);
  check(p.selects==1&&p.unlistens==1,"Creation restores Bluetooth once and unregisters the listener");
  p.change(PHONE);b.tick(20);b.ready(30);check(p.selects==1,"Later manual routes and reattachment cannot reapply startup preservation");
  p=new Platform();b=make(p,owned,logs);b.capture(true);b.ready(0);int reads=p.reads;
  for(int i=1;i<40;i++)b.tick(i*100);
  check(p.reads==reads&&p.selects==0,"Unchanged startup does not poll the audio service");
  p.change(DEX);b.tick(3999);check(p.selects==1,"A delayed Samsung route event inside the startup window is handled");
  p=new Platform();b=make(p,owned,logs);b.capture(true);b.ready(0);b.tick(4000);p.change(DEX);b.tick(4001);
  check(p.selects==0&&p.unlistens==1,"Startup monitoring expires after four seconds");
  p=new Platform();b=make(p,owned,logs);b.capture(true);p.change(PHONE);b.ready(0);p.change(DEX);b.tick(10);
  check(p.selects==0,"An intervening non-DeX choice cancels the correction");
  p=new Platform();b=make(p,owned,logs);b.capture(true);p.change(OTHER);b.ready(0);
  check(p.selects==0,"A different selected Bluetooth device is left untouched");
  p=new Platform();b=make(p,owned,logs);b.capture(true);p.outputs=new NativeBluetoothPreserver.Route[]{PHONE};p.change(DEX);b.ready(0);
  check(p.selects==0,"A disconnected original headset is never selected");
  p=new Platform();p.current=new NativeBluetoothPreserver.Route[]{BT,OTHER};b=make(p,owned,logs);b.capture(true);p.change(DEX);b.ready(0);
  check(p.selects==0,"Multiple selected routes fail closed");
  p=new Platform();p.outputs=new NativeBluetoothPreserver.Route[]{BT,BT};b=make(p,owned,logs);b.capture(true);p.change(DEX);b.ready(0);
  check(p.selects==0,"Ambiguous duplicate output identities fail closed");
  p=new Platform();p.current=new NativeBluetoothPreserver.Route[]{new NativeBluetoothPreserver.Route(8,128,"")};p.outputs=p.current;b=make(p,owned,logs);b.capture(true);p.change(DEX);b.ready(0);
  check(p.selects==0,"A Bluetooth output without a stable address is never guessed");
  p=new Platform();p.current=new NativeBluetoothPreserver.Route[]{new NativeBluetoothPreserver.Route(8,0,"invalid")};p.outputs=p.current;b=make(p,owned,logs);b.capture(true);p.change(DEX);b.ready(0);
  check(p.selects==0,"An invalid internal output type cannot reach the route setter");
  p=new Platform();b=make(p,owned,logs);b.capture(true);p.change(new NativeBluetoothPreserver.Route(25,32768,"foreign"));b.ready(0);
  check(p.selects==0,"A foreign remote-submix address cannot be changed");
  p=new Platform();b=make(p,owned,logs);b.capture(true);p.change(DEX);owned.set(false);b.ready(0);owned.set(true);
  check(p.selects==0,"Lost display ownership blocks startup restoration");
  p=new Platform();b=make(p,owned,logs);b.capture(true);p.change(DEX);final Platform race=p;
  p.triggerRead=3;p.beforeSecondRead=()->race.change(PHONE);b.ready(0);
  check(p.selects==0,"A newer manual route between validation and selection is preserved");
  p=new Platform();p.failListener=true;b=make(p,owned,logs);b.capture(true);b.ready(0);
  check(p.selects==0&&p.unlistens==1,"Unsupported callbacks fail closed and release partial registration");
  p=new Platform();p.failSelect=true;b=make(p,owned,logs);b.capture(true);p.change(DEX);b.ready(0);b.tick(20);
  check(p.selects==1&&p.unlistens==1,"A lost setter reply never causes repeated switches");
  p=new Platform();p.result=-1;b=make(p,owned,logs);b.capture(true);p.change(DEX);b.ready(0);b.tick(20);
  check(p.selects==1,"A rejected restoration is not retried indefinitely");
  p=new Platform();b=make(p,owned,logs);b.capture(true);b.close();p.change(DEX);b.ready(0);
  check(p.selects==0&&p.unlistens==1,"Explicit shutdown unregisters without changing audio");
  for(int[] spec:new int[][]{{7,16},{23,134217728},{26,536870912},{27,536870913},{30,536870914},{33,536870916},{34,536870920}}) {
   p=new Platform();NativeBluetoothPreserver.Route route=new NativeBluetoothPreserver.Route(spec[0],spec[1],"test-le");
   p.current=new NativeBluetoothPreserver.Route[]{route};p.outputs=p.current;b=make(p,owned,logs);b.capture(true);b.ready(0);b.close();
   check(p.reads>=2,"Selected Bluetooth family can be captured: "+spec[0]);
  }
  check(logs.stream().noneMatch(s->s.contains(BT.address)||s.contains(OTHER.address)),"Diagnostics do not record device addresses");
  return passed;
 }
}


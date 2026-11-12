package io.github.mazemei.dxdisplaycleanup;
import org.junit.Test;
import static org.junit.Assert.*;
import java.util.*;

public final class RetainedBrowserPolicyTest {
 static final class Fake implements RetainedSessionPolicy.Access {
  RetainedSessionPolicy.State state=new RetainedSessionPolicy.State(17,false,false,true,false,true,300);
  final List<String> actions=new ArrayList<>();boolean bluetooth,browserFails;int browser;long browserRevision;
  public RetainedSessionPolicy.State read(){return state;}
  public boolean bluetoothConnected(){return bluetooth;}
  public String awake(boolean on){actions.add(on?"ON":"OFF");return "OK";}
  public String route(boolean dex,long revision){actions.add(dex?"DEX":"PHONE");return "OK";}
  public String stopBrowser(long revision) throws Exception{browser++;browserRevision=revision;actions.add("BROWSER");if(browserFails)throw new Exception();return "STOP_REQUESTED";}
  public void report(long revision,String result){actions.add("REPORT");}
 }
 @Test public void disconnectRequestsBrowserCleanupAfterPowerAndAudio() throws Exception{
  Fake f=new Fake();RetainedSessionPolicy.apply(f);assertEquals(Arrays.asList("OFF","PHONE","BROWSER","REPORT"),f.actions);assertEquals(17,f.browserRevision);
 }
 @Test public void bluetoothDoesNotSuppressBrowserOrPowerCleanup() throws Exception{
  Fake f=new Fake();f.bluetooth=true;RetainedSessionPolicy.apply(f);assertEquals(Arrays.asList("OFF","BROWSER","REPORT"),f.actions);
 }
 @Test public void controlOnlyReconnectNeverRequestsBrowserStop() throws Exception{
  Fake f=new Fake();f.state=new RetainedSessionPolicy.State(18,true,false,true,false,true,300);RetainedSessionPolicy.apply(f);assertEquals(0,f.browser);
 }
 @Test public void actualViewerNeverRequestsBrowserStop() throws Exception{
  Fake f=new Fake();f.state=new RetainedSessionPolicy.State(18,true,true,true,true,true,300);RetainedSessionPolicy.apply(f);assertEquals(0,f.browser);
 }
 @Test public void immediateCleanupDoesNotRequestBrowserStop() throws Exception{
  Fake f=new Fake();f.state=new RetainedSessionPolicy.State(17,false,false,true,false,true,0);RetainedSessionPolicy.apply(f);assertTrue(f.actions.isEmpty());
 }
 @Test public void browserFailureDoesNotUndoPowerOrBlockReporting() throws Exception{
  Fake f=new Fake();f.browserFails=true;RetainedSessionPolicy.apply(f);assertEquals(Arrays.asList("OFF","PHONE","BROWSER","REPORT"),f.actions);
 }
}

package io.github.mazemei.dxdisplaycleanup;

import org.junit.Test;
import static org.junit.Assert.*;
import java.util.*;

public class RetainedSessionPolicyTest {
    static RetainedSessionPolicy.State state(long revision,boolean connected,boolean viewer,boolean power){return new RetainedSessionPolicy.State(revision,connected,viewer,true,power,true,300);}
    static final class Fake implements RetainedSessionPolicy.Access {
        RetainedSessionPolicy.State current=state(1,false,false,false),after;
        boolean bluetooth,unknown,reportFails;int reads;
        final List<String> actions=new ArrayList<>(),reports=new ArrayList<>();
        public RetainedSessionPolicy.State read(){reads++;if(after!=null&&reads>=2)current=after;return current;}
        public boolean bluetoothConnected() throws Exception {if(unknown)throw new Exception("unavailable");return bluetooth;}
        public String awake(boolean enabled){actions.add(enabled?"ON":"OFF");return enabled?"ON":"OFF";}
        public String route(boolean dex,long revision){actions.add(dex?"DEX":"PHONE");return "CHANGED";}
        public void report(long revision,String result) throws Exception {if(reportFails)throw new Exception("report unavailable");reports.add(result);}
    }
    @Test public void disconnectRoutesPhoneAndClearsPower() throws Exception {
        Fake f=new Fake();RetainedSessionPolicy.apply(f);assertEquals(Arrays.asList("OFF","PHONE"),f.actions);
    }
    @Test public void bluetoothSkipsOnlyAudio() throws Exception {
        Fake f=new Fake();f.bluetooth=true;RetainedSessionPolicy.apply(f);assertEquals(Collections.singletonList("OFF"),f.actions);assertTrue(f.reports.get(0).contains("audio=BLUETOOTH"));
    }
    @Test public void controlOnlyReconnectDoesNotRestoreDexOrStayAwake() throws Exception {
        Fake f=new Fake();f.current=state(2,true,false,false);RetainedSessionPolicy.apply(f);assertEquals(Arrays.asList("OFF","PHONE"),f.actions);
    }
    @Test public void actualViewerRestoresDexAndConfiguredPower() throws Exception {
        Fake f=new Fake();f.current=state(2,true,true,true);RetainedSessionPolicy.apply(f);assertEquals(Arrays.asList("ON","DEX"),f.actions);
    }
    @Test public void activeViewerHonorsStayAwakeOff() throws Exception {
        Fake f=new Fake();f.current=state(2,true,true,false);RetainedSessionPolicy.apply(f);assertEquals(Collections.singletonList("DEX"),f.actions);
    }
    @Test public void reconnectChecksBluetoothAgain() throws Exception {
        Fake f=new Fake();f.current=state(2,true,true,true);f.bluetooth=true;RetainedSessionPolicy.apply(f);assertEquals(Collections.singletonList("ON"),f.actions);
    }
    @Test public void anotherManagedWindowKeepsItsPowerDemand() throws Exception {
        Fake f=new Fake();f.current=state(2,true,false,true);RetainedSessionPolicy.apply(f);assertEquals(Arrays.asList("ON","PHONE"),f.actions);
    }
    @Test public void oldDisconnectClearIsReconciledAfterFastReconnect() throws Exception {
        Fake f=new Fake();f.after=state(2,true,true,true);RetainedSessionPolicy.apply(f);assertEquals(Arrays.asList("OFF","PHONE","ON","DEX"),f.actions);
    }
    @Test public void reportFailureCannotSuppressReconciliation() throws Exception {
        Fake f=new Fake();f.after=state(2,true,true,true);f.reportFails=true;RetainedSessionPolicy.apply(f);assertEquals(Arrays.asList("OFF","PHONE","ON","DEX"),f.actions);
    }
    @Test public void unknownBluetoothStillReleasesPower() throws Exception {
        Fake f=new Fake();f.unknown=true;RetainedSessionPolicy.apply(f);assertEquals(Collections.singletonList("OFF"),f.actions);assertTrue(f.reports.get(0).contains("SKIP_UNKNOWN"));
    }
    @Test public void disabledBasicFreshAndImmediateSessionsDoNothing() throws Exception {
        for(RetainedSessionPolicy.State s:Arrays.asList(
            new RetainedSessionPolicy.State(1,false,false,true,false,false,300),
            new RetainedSessionPolicy.State(1,true,false,false,false,true,300),
            new RetainedSessionPolicy.State(1,false,false,true,false,true,0))) {
            Fake f=new Fake();f.current=s;RetainedSessionPolicy.apply(f);assertTrue(f.actions.isEmpty());
        }
    }
    @Test public void parsesOnlyCurrentBoundedState() {
        RetainedSessionPolicy.State s=RetainedSessionPolicy.parse("SESSION|4|17|1|0|1|0|1|300");assertEquals(17,s.revision);assertFalse(s.dex());
        for(String bad:Arrays.asList("SESSION|3|17|1|0|1|0|1|300","SESSION|4|-1|1|0|1|0|1|300","SESSION|4|1|2|0|1|0|1|300","SESSION|4|1|1|0|1|0|1|5"))
            try{RetainedSessionPolicy.parse(bad);fail(bad);}catch(IllegalArgumentException expected){ }
    }
    @Test public void allBluetoothAudioTypesAreProtected() {
        for(int type:new int[]{7,8,23,26,27,30})assertTrue(RetainedSessionPolicy.bluetoothType(type));
        for(int type:new int[]{1,2,3,4,11,25})assertFalse(RetainedSessionPolicy.bluetoothType(type));
    }
}

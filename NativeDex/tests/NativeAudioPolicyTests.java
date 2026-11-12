import io.github.mazemei.dxmnativedex.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.*;

public final class NativeAudioPolicyTests {
    static int passed;
    static void check(boolean value,String name){if(!value)throw new AssertionError(name);passed++;System.out.println("PASS "+name);}
    static final class Platform implements NativeAudioRouter.Platform {
        boolean bluetooth;int current=32768,calls;String address;
        public boolean bluetoothAudioConnected(){return bluetooth;}
        public int mediaDevice(){return current;}
        public int select(int device,String address){calls++;current=device;this.address=address;return 0;}
    }
    public static int run() throws Exception {
        Platform platform=new Platform();NativeAudioRouter audio=new NativeAudioRouter(platform);
        check(audio.select(false).equals("CHANGED")&&platform.current==2&&platform.address.equals(""),"Named audio executor selects phone speaker with an empty address");
        check(audio.select(true).equals("CHANGED")&&platform.current==32768&&platform.address.equals("0"),"Named audio executor selects DeX remote submix address0");
        int calls=platform.calls;platform.bluetooth=true;
        check(audio.select(false).equals("BLUETOOTH")&&platform.calls==calls,"Native executor independently protects connected Bluetooth audio");
        platform.bluetooth=false;check(audio.select(true).equals("UNCHANGED")&&platform.calls==calls,"Already-selected audio is not forced again");
        check(new NativeAudioRouter(platform,()->false).select(false).equals("NOT_OWNED")&&platform.calls==calls,"An unrelated or already-removed display cannot trigger an audio change");
        for(int type:new int[]{7,8,23,26,27,30,33,34,35})check(NativeAudioRouter.bluetoothType(type),"Bluetooth audio port type protected: "+type);
        check(!NativeAudioRouter.bluetoothType(25)&&!NativeAudioRouter.bluetoothType(2),"Remote submix and speaker are not mistaken for Bluetooth");
        NativeRetentionState state=new NativeRetentionState(300,0);long owner=state.attach(1);
        check(state.route(state.snapshot().revision,false,()->"BAD").equals("DISABLED"),"A basic or old-Companion session cannot execute retained audio policy");
        state.enablePolicy(true);
        check(state.route(state.snapshot().revision,false,()->"BAD").equals("DISABLED"),"An unused newly-created owner has no disconnect correction");
        state.viewer(owner,true,true);long old=state.snapshot().revision;state.detach(owner,10);
        NativeRetentionState.Snapshot off=state.snapshot();
        check(!off.connected&&!off.viewer&&!off.power&&off.seen,"Disconnect clears viewer/power demand while retaining display history");
        check(state.route(old,true,()->"BAD").equals("STALE"),"A delayed old viewer event cannot reroute an offline display");
        check(state.route(off.revision,false,()->"PHONE").equals("PHONE"),"Current pending state authorizes only phone audio");
        long replacement=state.attach(20);
        check(!state.snapshot().viewer&&!state.snapshot().power,"Control-only reattachment does not restore video or power demand");
        check(!state.viewer(owner,true,true),"An old transport cannot publish viewer state into a replacement epoch");
        check(state.route(state.snapshot().revision,true,()->"BAD").equals("STALE"),"Control-only reconnect does not authorize DeX output");
        state.viewer(replacement,true,true);
        check(state.route(state.snapshot().revision,true,()->"DEX").equals("DEX"),"An actual new viewer authorizes DeX audio");
        state.policy(0);check(state.route(state.snapshot().revision,true,()->"DEX").equals("DEX"),"Companion immediate cleanup preference does not disable native audio during grace");
        testNotifier();return passed;
    }
    static void testNotifier() throws Exception {
        NativeRetentionState state=new NativeRetentionState(300,0);state.enablePolicy(true);long owner=state.attach(0);state.viewer(owner,true,true);
        CountDownLatch first=new CountDownLatch(1),release=new CountDownLatch(1),last=new CountDownLatch(1);
        AtomicInteger count=new AtomicInteger();AtomicLong delivered=new AtomicLong();
        NativePolicyNotifier notifier=new NativePolicyNotifier(snapshot->{
            if(count.incrementAndGet()==1){first.countDown();release.await(2,TimeUnit.SECONDS);}
            else{delivered.set(snapshot.revision);last.countDown();}
        });
        try {
            notifier.publish(state.snapshot());check(first.await(1,TimeUnit.SECONDS),"Companion notification uses a separate worker");
            state.detach(owner,10);notifier.publish(state.snapshot());owner=state.attach(20);state.viewer(owner,true,true);long newest=state.snapshot().revision;notifier.publish(state.snapshot());
            release.countDown();check(last.await(1,TimeUnit.SECONDS)&&delivered.get()==newest,"Rapid connection events coalesce to the latest native revision");
            check(count.get()==2,"The notifier queue does not accumulate obsolete transitions");
        }finally{release.countDown();notifier.close();}
    }
}

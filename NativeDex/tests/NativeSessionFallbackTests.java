import io.github.mazemei.dxmnativedex.*;
import java.util.concurrent.atomic.*;

public final class NativeSessionFallbackTests {
    private static int passed;
    private static void check(boolean ok,String name){if(!ok)throw new AssertionError(name);passed++;System.out.println("PASS "+name);}
    public static int run() throws Exception {
        NativeRetentionState state=new NativeRetentionState(30,0);
        NativeAudioPolicyTests.Platform platform=new NativeAudioPolicyTests.Platform();
        AtomicInteger power=new AtomicInteger(),writes=new AtomicInteger();
        AtomicBoolean owned=new AtomicBoolean(true);
        NativeSessionFallback fallback=new NativeSessionFallback(state,new NativeAudioRouter(platform,owned::get),enabled->{writes.incrementAndGet();power.set(enabled?7:0);return enabled?"ON":"OFF";},message->{},owned::get);
        long owner=state.attach(0);fallback.tick();
        check(writes.get()==0&&platform.calls==0,"Basic fresh control does not change audio or power before a viewer");
        state.viewer(owner,true,true);fallback.tick();
        check(power.get()==7,"Basic viewer applies its captured stay-awake demand");
        int count=writes.get();fallback.tick();check(writes.get()==count,"Basic reconciliation runs on state changes only");
        state.detach(owner,100);fallback.tick();
        check(power.get()==0&&platform.current==2&&!state.tick(101),"Basic disconnect releases power and routes phone before30-second cleanup");
        owner=state.attach(200);fallback.tick();
        check(power.get()==0&&platform.current==2,"Control-only reconnect leaves phone audio and screen policy");
        state.viewer(owner,true,true);fallback.tick();
        check(power.get()==7&&platform.current==32768,"Actual basic viewer resume restores DeX audio and power");
        platform.bluetooth=true;int calls=platform.calls;state.detach(owner,300);fallback.tick();
        check(power.get()==0&&platform.calls==calls,"Bluetooth audio is untouched while disconnect still releases power");
        owner=state.attach(400);state.viewer(owner,true,true);state.enablePolicy(true);count=writes.get();fallback.tick();
        check(writes.get()==count,"Verified Companion delegation prevents a duplicate local policy writer");
        state.enablePolicy(false);owned.set(false);fallback.tick();check(writes.get()==count,"Lost display ownership prevents late local changes");
        owned.set(true);state.stop("Explicit stop");fallback.tick();check(writes.get()==count,"Explicit stop disables late fallback writes");
        return passed;
    }
}

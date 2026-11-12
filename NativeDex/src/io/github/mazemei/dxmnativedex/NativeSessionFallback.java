package io.github.mazemei.dxmnativedex;

import java.util.function.BooleanSupplier;
import java.util.function.Consumer;

/** Named audio/power fallback; framework I/O never holds the heartbeat/lifetime lock. */
public final class NativeSessionFallback {
    public interface Power { String awake(boolean enabled) throws Exception; }
    private final NativeRetentionState state;
    private final NativeAudioRouter audio;
    private final Power power;
    private final Consumer<String> log;
    private final BooleanSupplier owned;
    private final Runnable refreshDelegate;
    private long appliedRevision=-1;
    public NativeSessionFallback(NativeRetentionState state,NativeAudioRouter audio,Power power,
            Consumer<String> log,BooleanSupplier owned) {this(state,audio,power,log,owned,()->{});}
    public NativeSessionFallback(NativeRetentionState state,NativeAudioRouter audio,Power power,
            Consumer<String> log,BooleanSupplier owned,Runnable refreshDelegate) {
        this.state=state;this.audio=audio;this.power=power;this.log=log;this.owned=owned;this.refreshDelegate=refreshDelegate;
    }
    public void tick() {
        for(int pass=0;pass<2;pass++) {
            NativeRetentionState.Snapshot snapshot=state.snapshot();
            if(snapshot.revision==appliedRevision||!state.canFallback(snapshot.revision)||!owned.getAsBoolean())return;
            String awake="KEEP",route="SKIP_UNKNOWN";
            try {
                if(snapshot.connected&&snapshot.power)awake=power.awake(true);
                else if(!(snapshot.connected&&snapshot.viewer))awake=power.awake(false);
            }catch(Exception error){awake="ERROR";}
            if(state.canFallback(snapshot.revision)&&owned.getAsBoolean())
                try{route=audio.select(snapshot.connected&&snapshot.viewer);}catch(Exception unknown){}
            appliedRevision=snapshot.revision;
            log.accept("Native fallback rev="+snapshot.revision+" power="+awake+" audio="+route);
            NativeRetentionState.Snapshot after=state.snapshot();
            if(after.revision==snapshot.revision)return;
            if(after.enabled){refreshDelegate.run();return;}
        }
    }
}

package io.github.mazemei.dxmnativedex;

import java.util.concurrent.atomic.AtomicBoolean;
import java.util.function.BooleanSupplier;
import java.util.function.Consumer;

/** One creation only: remember the selected Bluetooth route, not every connected device. */
public final class NativeBluetoothPreserver implements AutoCloseable {
    public static final long WINDOW_MS = 4000;
    public static final class Route {
        public final int type, internalType;
        public final String address;
        public Route(int type, int internalType, String address) {
            this.type=type; this.internalType=internalType; this.address=address==null?"":address;
        }
        public boolean same(Route other) {
            return other!=null&&type==other.type&&internalType==other.internalType&&address.equals(other.address);
        }
        public boolean bluetooth() { return NativeAudioRouter.bluetoothType(type); }
        public boolean dex() { return type==25&&internalType==32768&&address.equals("0"); }
    }
    public interface Platform {
        Route[] routes() throws Exception;
        Route[] connected() throws Exception;
        void listen(Runnable changed) throws Exception;
        void unlisten() throws Exception;
        int select(int internalType, String address) throws Exception;
    }
    private final Platform platform;
    private final BooleanSupplier owned;
    private final Consumer<String> log;
    private final AtomicBoolean changed=new AtomicBoolean();
    private Route target;
    private boolean finished, listening, ready;
    private long deadline;
    public NativeBluetoothPreserver(Platform platform, BooleanSupplier owned, Consumer<String> log) {
        this.platform=platform; this.owned=owned; this.log=log;
    }
    private void note(String value) { log.accept("BluetoothStartup "+value); }
    private static Route single(Route[] routes) { return routes!=null&&routes.length==1?routes[0]:null; }
    private boolean connected(Route route) throws Exception {
        Route[] outputs=platform.connected();
        if(outputs==null) return false;
        int matches=0; for(Route output:outputs) if(route.same(output)) matches++;
        return matches==1;
    }
    public synchronized void capture(boolean enabled) {
        if(!enabled) { finished=true; return; }
        try {
            // Register first, then take a fresh snapshot. Callback threads only set a flag.
            listening=true; platform.listen(()->changed.set(true));
            target=single(platform.routes());
            if(target==null||!target.bluetooth()||target.internalType<=0||target.address.isEmpty()||!connected(target)) {
                note("skip: no unique selected connected Bluetooth media output"); close(); return;
            }
            note("captured selected Bluetooth output type="+target.type);
        } catch(Exception e) { fail(e); }
    }
    public synchronized void ready(long now) {
        if(finished||target==null) return;
        ready=true; deadline=now+WINDOW_MS; changed.set(true); tick(now);
    }
    /** Called by the existing owner loop; no periodic audio query, only a pending event. */
    public synchronized void tick(long now) {
        if(finished||!ready) return;
        if(now>=deadline) { note("startup window ended; no output forced"); close(); return; }
        if(!changed.getAndSet(false)) return;
        try {
            if(!owned.getAsBoolean()) { note("skip: owned native display ended"); close(); return; }
            Route current=single(platform.routes());
            if(target.same(current)) return;
            if(current==null||!current.dex()) { note("skip: another output was selected"); close(); return; }
            if(!connected(target)) { note("skip: original Bluetooth output is disconnected or ambiguous"); close(); return; }
            // Never apply a queued DeX event after a newer manual route or display change.
            if(!current.same(single(platform.routes()))||!owned.getAsBoolean()) {
                note("skip: route or native ownership changed"); close(); return;
            }
            finished=true; // At most one setter, including rejection or a lost Binder reply.
            int result=platform.select(target.internalType,target.address);
            note("restore requested type="+target.type+" result="+result+"; later choices are untouched");
            close();
        } catch(Exception e) { fail(e); }
    }
    private void fail(Exception e) {
        note("unavailable: "+e.getClass().getSimpleName()); close();
    }
    @Override public synchronized void close() {
        finished=true;
        if(listening) { listening=false; try { platform.unlisten(); } catch(Exception ignored) {} }
        target=null;
    }
}

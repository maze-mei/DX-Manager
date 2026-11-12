package io.github.mazemei.dxmnativedex;

/** Fixed native reconnect grace and explicit stop; the OS may end a process earlier. */
public final class NativeRetentionState {
    public static final int DISCONNECT_GRACE_SECONDS = 30;
    private int delaySeconds;
    private boolean connected, attachedOnce, viewer, viewerSeen, powerRequested, policyEnabled;
    private long epoch, lastPing, pendingSince, revision;
    private boolean browserEnabled, viewerInEpoch;
    private long pendingBrowserEpoch=-1, handledBrowserEpoch=-1;
    private String stopReason;
    public NativeRetentionState(int delay, long now) { policy(delay); pendingSince = now; }
    public static int validateDelay(int value) {
        if (value != -1 && value != 0 && value != 30 && value != 60 && value != 300 && value != 600 && value != 1800)
            throw new IllegalArgumentException("Unsupported native cleanup request.");
        // Legacy policy values may arrive from an older Companion; native grace stays fixed.
        return DISCONNECT_GRACE_SECONDS;
    }
    public synchronized void policy(int delay) { int value=validateDelay(delay);if(delaySeconds!=value){delaySeconds=value;revision++;} }
    public synchronized int policy() { return delaySeconds; }
    public synchronized long attach(long now) { connected = true; attachedOnce = true; viewer=false;viewerInEpoch=false;pendingBrowserEpoch=-1;powerRequested=false;lastPing = now;revision++;return ++epoch; }
    public synchronized void ping(long owner, long now) { if (owner == epoch && stopReason == null) { if(!connected){connected=true;revision++;}lastPing = now; } }
    public synchronized void detach(long owner, long now) {
        if (connected && owner == epoch) disconnect(now);
    }
    private void disconnect(long now){pendingBrowserEpoch=viewerInEpoch&&browserEnabled&&policyEnabled&&delaySeconds!=0?epoch:-1;connected=false;viewer=false;powerRequested=false;pendingSince=now;revision++;}
    public synchronized boolean viewer(long owner,boolean active,boolean stayAwake) {
        if(owner!=epoch||!connected||stopReason!=null)return false;
        if(viewer!=active||powerRequested!=stayAwake){viewer=active;powerRequested=stayAwake;viewerSeen|=active;viewerInEpoch|=active;revision++;}
        return true;
    }
    public synchronized void enablePolicy(boolean enabled){if(policyEnabled!=enabled){policyEnabled=enabled;revision++;}}
    public synchronized void enableBrowserPolicy(boolean enabled){if(browserEnabled!=enabled){browserEnabled=enabled;revision++;}if(!enabled)pendingBrowserEpoch=-1;}
    public synchronized String stopBrowser(long expected,java.util.concurrent.Callable<String> action) throws Exception {
        if(!policyEnabled||!browserEnabled||stopReason!=null||delaySeconds==0)return "DISABLED";
        if(expected!=revision||connected)return "STALE";
        if(pendingBrowserEpoch<0)return "NOT_PENDING";
        if(handledBrowserEpoch==pendingBrowserEpoch)return "ALREADY_HANDLED";
        handledBrowserEpoch=pendingBrowserEpoch;
        return action.call();
    }
    public static final class Snapshot {
        public final long revision;
        public final boolean connected,viewer,seen,power,enabled;
        public final int delay;
        Snapshot(long r,boolean c,boolean v,boolean s,boolean p,boolean e,int d){revision=r;connected=c;viewer=v;seen=s;power=p;enabled=e;delay=d;}
        public String frame(){return "4|"+revision+"|"+(connected?1:0)+"|"+(viewer?1:0)+"|"+(seen?1:0)+"|"+(power?1:0)+"|"+(enabled?1:0)+"|"+delay;}
    }
    public synchronized Snapshot snapshot(){return new Snapshot(revision,connected,viewer,viewerSeen,connected&&powerRequested,policyEnabled&&stopReason==null,delaySeconds);}
    public synchronized String route(long expected,boolean dex,java.util.concurrent.Callable<String> action) throws Exception {
        if(!policyEnabled||stopReason!=null||delaySeconds==0||!viewerSeen)return "DISABLED";
        if(expected!=revision||dex!=(connected&&viewer))return "STALE";
        return action.call();
    }
    public synchronized boolean canFallback(long expected) {
        return !policyEnabled&&stopReason==null&&viewerSeen&&expected==revision;
    }
    public synchronized boolean connected() { return connected; }
    public synchronized String reason() { return stopReason; }
    public synchronized void stop(String reason) { if (stopReason == null){stopReason = reason;revision++;} }
    public synchronized boolean tick(long now) {
        if (connected && now - lastPing > 5000) disconnect(now);
        if (!connected && delaySeconds >= 0 && (attachedOnce || now - pendingSince >= 45000) && now - pendingSince >= delaySeconds * 1000L)
            stop("Native reconnect grace expired");
        return stopReason != null;
    }
}

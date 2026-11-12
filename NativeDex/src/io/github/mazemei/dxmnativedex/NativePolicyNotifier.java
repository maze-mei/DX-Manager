package io.github.mazemei.dxmnativedex;

import java.util.concurrent.ArrayBlockingQueue;
import java.util.concurrent.TimeUnit;

/** Coalesced event delivery; Android engine liveness never waits for an APK broadcast. */
public final class NativePolicyNotifier implements AutoCloseable {
    public interface Sender { void send(NativeRetentionState.Snapshot snapshot) throws Exception; }
    private final ArrayBlockingQueue<NativeRetentionState.Snapshot> queue=new ArrayBlockingQueue<>(1);
    private volatile boolean closed;
    private long last=-1;
    public NativePolicyNotifier(Sender sender) {
        Thread worker=new Thread(()->{
            while(!closed)try{
                NativeRetentionState.Snapshot value=queue.poll(500,TimeUnit.MILLISECONDS);
                if(value!=null&&!closed)sender.send(value);
            }catch(InterruptedException error){Thread.currentThread().interrupt();return;}
             catch(Exception error){com.scrcpydex.server.Ln.w("Companion policy notification failed: "+error.getClass().getSimpleName());}
        },"DXM-Native-Companion-Events");worker.setDaemon(true);worker.start();
    }
    public synchronized void publish(NativeRetentionState.Snapshot value) {
        if(closed||value.revision==last||!value.enabled)return;
        last=value.revision;queue.poll();queue.offer(value);
    }
    public void close(){closed=true;queue.clear();}
}

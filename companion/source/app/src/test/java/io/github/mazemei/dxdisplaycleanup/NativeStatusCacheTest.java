package io.github.mazemei.dxdisplaycleanup;

import org.junit.Test;
import static org.junit.Assert.*;
import java.util.ArrayDeque;
import java.util.Queue;
import java.util.concurrent.*;
import java.util.concurrent.atomic.*;

public final class NativeStatusCacheTest {
    static final class Work implements java.util.concurrent.Executor {
        final Queue<Runnable> jobs=new ArrayDeque<>();
        public void execute(Runnable task){jobs.add(task);}
        void run(){jobs.remove().run();}
    }
    @Test public void firstUiReadDoesNotCallProbeInline(){
        Work work=new Work();AtomicInteger probes=new AtomicInteger();
        NativeStatusCache<String> cache=new NativeStatusCache<>(work,()->0L,1000);
        assertEquals("pending",cache.get("a","pending",token->{probes.incrementAndGet();return "ready";},()->{}));
        assertEquals(0,probes.get());assertEquals(1,work.jobs.size());
        work.run();assertEquals(1,probes.get());
        assertEquals("ready",cache.get("a","pending",token->{throw new AssertionError("Unnecessary probe");},()->{}));
    }
    @Test public void blockedSocketProbeDoesNotBlockUiRead() throws Exception {
        ExecutorService worker=Executors.newSingleThreadExecutor();
        CountDownLatch entered=new CountDownLatch(1),release=new CountDownLatch(1),done=new CountDownLatch(1);
        AtomicReference<Thread> probeThread=new AtomicReference<>();Thread caller=Thread.currentThread();
        NativeStatusCache<String> cache=new NativeStatusCache<>(worker,()->0L,1000);
        try {
            assertEquals("pending",cache.get("a","pending",token->{probeThread.set(Thread.currentThread());entered.countDown();try{release.await(2,TimeUnit.SECONDS);}catch(InterruptedException e){Thread.currentThread().interrupt();}return "ready";},done::countDown));
            assertTrue(entered.await(2,TimeUnit.SECONDS));assertNotSame(caller,probeThread.get());assertEquals(1L,done.getCount());
            release.countDown();assertTrue(done.await(2,TimeUnit.SECONDS));
        }finally{release.countDown();worker.shutdownNow();}
    }
    @Test public void repeatedRendersCoalesceOnePendingProbe(){
        Work work=new Work();NativeStatusCache<String> cache=new NativeStatusCache<>(work,()->0L,1000);
        for(int i=0;i<20;i++)assertEquals("pending",cache.get("a","pending",token->"ready",()->{}));
        assertEquals(1,work.jobs.size());
    }
    @Test public void oldSessionReplyCannotPopulateNewSession(){
        Work work=new Work();AtomicInteger changes=new AtomicInteger();NativeStatusCache<String> cache=new NativeStatusCache<>(work,()->0L,1000);
        cache.get("old","pending",token->"old-data",changes::incrementAndGet);
        assertEquals("pending",cache.get("new","pending",token->"new-data",changes::incrementAndGet));
        work.run();assertEquals(1,changes.get());
        assertEquals("pending",cache.get("new","pending",token->"new-data",changes::incrementAndGet));work.run();
        assertEquals("new-data",cache.get("new","pending",token->"unexpected",()->{}));
    }
    @Test public void expiryReturnsCachedValueWhileRefreshing(){
        Work work=new Work();AtomicLong now=new AtomicLong();NativeStatusCache<String> cache=new NativeStatusCache<>(work,now::get,1000);
        cache.get("a","pending",token->"first",()->{});work.run();now.set(1001);
        assertEquals("first",cache.get("a","pending",token->"second",()->{}));assertEquals(1,work.jobs.size());work.run();
        assertEquals("second",cache.get("a","pending",token->"unexpected",()->{}));
    }
    @Test public void completedCleanupInvalidatesInFlightStatus(){
        Work work=new Work();NativeStatusCache<String> cache=new NativeStatusCache<>(work,()->0L,1000);
        cache.get("a","pending",token->"obsolete-active",()->{});cache.clear();work.run();
        assertEquals("pending",cache.get("b","pending",token->"current",()->{}));work.run();
        assertEquals("current",cache.get("b","pending",token->"unexpected",()->{}));
    }
    @Test public void policyEventExpiresStatusWithoutFlickeringTheVisibleCard(){
        Work work=new Work();AtomicLong now=new AtomicLong(10);NativeStatusCache<String> cache=new NativeStatusCache<>(work,now::get,1000);
        cache.get("a","pending",token->"in-use",()->{});work.run();cache.expire();
        assertEquals("in-use",cache.get("a","pending",token->"retained",()->{}));assertEquals(1,work.jobs.size());work.run();
        assertEquals("retained",cache.get("a","pending",token->{throw new AssertionError("Unnecessary probe");},()->{}));
    }
    @Test public void failedProbeDoesNotWedgeTheRefreshQueue(){
        Work work=new Work();AtomicLong now=new AtomicLong();NativeStatusCache<String> cache=new NativeStatusCache<>(work,now::get,1000);
        cache.get("a","pending",token->{throw new IllegalStateException("unavailable");},()->{});work.run();now.set(1001);
        assertEquals("pending",cache.get("a","pending",token->"recovered",()->{}));work.run();
        assertEquals("recovered",cache.get("a","pending",token->"unexpected",()->{}));
    }
}

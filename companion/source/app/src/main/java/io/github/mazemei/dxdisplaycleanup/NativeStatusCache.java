package io.github.mazemei.dxdisplaycleanup;

import java.util.concurrent.Executor;
import java.util.function.LongSupplier;

/** UI reads are immediate; the supplied executor owns every potentially blocking probe. */
final class NativeStatusCache<T> {
    interface Loader<T> { T load(String token); }
    private final Executor executor;
    private final LongSupplier clock;
    private final long ttl;
    private String token="";
    private T value;
    private long updated=Long.MIN_VALUE;
    private long generation;
    private boolean running;
    NativeStatusCache(Executor executor,LongSupplier clock,long ttl){this.executor=executor;this.clock=clock;this.ttl=ttl;}
    synchronized void clear(){if(!token.isEmpty()||value!=null){token="";value=null;generation++;updated=Long.MIN_VALUE;}}
    synchronized void expire(){generation++;updated=clock.getAsLong()-ttl;}
    synchronized void invalidate(){value=null;generation++;updated=Long.MIN_VALUE;}
    synchronized T get(String requested,T pending,Loader<T> loader,Runnable changed){
        if(!requested.equals(token)){token=requested;value=null;generation++;updated=Long.MIN_VALUE;}
        if(value!=null&&clock.getAsLong()-updated<ttl)return value;
        T visible=value==null?pending:value;
        if(!running){
            running=true;final long epoch=generation;final String captured=token;
            executor.execute(()->{
                T result;
                try{result=loader.load(captured);}catch(RuntimeException failure){result=pending;}
                synchronized(NativeStatusCache.this){
                    running=false;
                    if(epoch==generation&&captured.equals(token)){value=result;updated=clock.getAsLong();}
                }
                // Also wake readers after discarding an obsolete probe so the latest token can be queried.
                changed.run();
            });
        }
        return visible;
    }
}

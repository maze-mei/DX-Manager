package io.github.mazemei.dxmnativedex;

/** Bounded creation-only verification. No input-path or retained-display correction. */
public final class NativeMetricsVerifier {
    public static final long BUDGET_MS=8000;
    public static final class Metrics {
        public final int id,width,height,dpi,effectiveDpi;
        public Metrics(int id,int width,int height,int dpi,int effectiveDpi){this.id=id;this.width=width;this.height=height;this.dpi=dpi;this.effectiveDpi=effectiveDpi;}
        public boolean matches(int target,int w,int h,int d){return id==target&&width==w&&height==h&&dpi==d&&effectiveDpi==d;}
        @Override public String toString(){return id+":"+width+"x"+height+"@"+dpi+" effective="+effectiveDpi;}
    }
    public interface Platform {
        long now(); boolean owned(); void pause(long ms) throws Exception;
        void size(int width,int height) throws Exception; void density(int dpi) throws Exception;
        Metrics read() throws Exception; void note(String kind,String text);
    }
    private static final class OwnershipChanged extends IllegalStateException { OwnershipChanged(String message){super(message);} }
    private static final class GeometryChanged extends IllegalStateException { GeometryChanged(String message){super(message);} }
    private static void requireOwned(Platform p){if(!p.owned())throw new OwnershipChanged("Owned native display ended or changed.");}
    private static void requireGeometry(Metrics actual,int id,int w,int h){
        if(actual==null||actual.id!=id)throw new OwnershipChanged("Native display identity changed before final DPI application.");
        if(actual.width!=w||actual.height!=h)throw new GeometryChanged("Initial native size changed before final DPI application: requested="+w+"x"+h+" actual="+actual);
    }
    public static Metrics initialize(Platform p,int id,int w,int h,int dpi)throws Exception{
        long deadline=p.now()+BUDGET_MS;
        int initialDpi=NativeInitialMode.initialDpi(dpi);
        Metrics initial=verify(p,id,w,h,initialDpi);
        if(initialDpi==dpi)return initial;
        p.note("response","initial DPI "+initialDpi+" confirmed; applying requested DPI "+dpi+" without size change");
        return apply(p,id,w,h,dpi,false,deadline);
    }
    public static Metrics verify(Platform p,int id,int w,int h,int dpi) throws Exception {
        if(id<=0)throw new IllegalArgumentException("Native metrics never target display0.");
        long deadline=p.now()+BUDGET_MS;Metrics actual=null;int stable=0;
        while(p.now()<deadline){
            requireOwned(p);actual=p.read();
            if(actual==null||actual.id!=id)throw new OwnershipChanged("Native display identity changed during initial verification.");
            if(actual.matches(id,w,h,dpi)){
                if(++stable>=2){p.note("response","initial verified requested="+w+"x"+h+"@"+dpi+" actual="+actual+"; no size/density writes");return actual;}
            }else{stable=0;p.note("warning","initial settling requested="+w+"x"+h+"@"+dpi+" actual="+actual);}
            p.pause(Math.min(150,Math.max(1,deadline-p.now())));
        }
        throw new IllegalStateException("Initial native metrics not confirmed: requested="+w+"x"+h+"@"+dpi+" actual="+actual+"; no resize fallback was used.");
    }
    public static Metrics apply(Platform p,int id,int w,int h,int dpi) throws Exception {
        return apply(p,id,w,h,dpi,true,p.now()+BUDGET_MS);
    }
    private static Metrics apply(Platform p,int id,int w,int h,int dpi,boolean resize,long deadline) throws Exception {
        if(id<=0)throw new IllegalArgumentException("Native metrics never target display0.");
        Metrics actual=null;
        requireOwned(p);
        if(resize){p.size(w,h);p.pause(150);}
        else {actual=p.read();requireOwned(p);requireGeometry(actual,id,w,h);}
        for(int attempt=1;attempt<=3&&p.now()<deadline;attempt++) {
            requireOwned(p);
            try {
                if(resize&&actual!=null&&(actual.width!=w||actual.height!=h))p.size(w,h);
                p.density(dpi);
                long settle=Math.min(deadline,p.now()+1200);int stable=0;
                while(p.now()<settle) {
                    requireOwned(p);actual=p.read();
                    if(actual==null||actual.id!=id)throw new OwnershipChanged("Native display identity changed during metrics verification.");
                    if(!resize)requireGeometry(actual,id,w,h);
                    if(actual.matches(id,w,h,dpi)) {
                        if(++stable>=2){p.note("response","verified requested="+w+"x"+h+"@"+dpi+" actual="+actual+" attempt="+attempt);return actual;}
                    }else {
                        stable=0;p.note("warning","DPI/size mismatch requested="+w+"x"+h+"@"+dpi+" actual="+actual+" attempt="+attempt);
                    }
                    p.pause(Math.min(150,Math.max(1,settle-p.now())));
                }
            }catch(Exception e){if(e instanceof OwnershipChanged||e instanceof GeometryChanged)throw e;requireOwned(p);p.note("warning","apply attempt="+attempt+" failed="+e.getClass().getSimpleName()+" message="+e.getMessage());}
        }
        String failure="Native metrics not confirmed: requested="+w+"x"+h+"@"+dpi+" actual="+actual;
        p.note("error",failure);throw new IllegalStateException(failure);
    }
}

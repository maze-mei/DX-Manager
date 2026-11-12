package io.github.mazemei.dxdisplaycleanup;
import android.content.Context;
import android.content.SharedPreferences;
import android.hardware.display.DisplayManager;
import android.os.SystemClock;
import android.provider.Settings;
import android.view.Display;
import java.util.Objects;

/** Reversible desktop-only initial DPI preference. Never restore against a visible display. */
final class NativeDesktopDensity {
    private static final String PREFS="native_desktop_density_recovery",KEY="screen_zoom_on_external_desktop_display";
    private static SharedPreferences prefs(Context c){return c.getSharedPreferences(PREFS,Context.MODE_PRIVATE);}
    private static Display[] displays(Context c){DisplayManager m=(DisplayManager)c.getSystemService(Context.DISPLAY_SERVICE);if(m==null)throw new IllegalStateException("Display service unavailable");return m.getDisplays();}
    private static boolean external(Display d){if(d.getDisplayId()==0)return false;try{int type=(Integer)d.getClass().getMethod("getType").invoke(d);return type==2||type==3;}catch(Exception unavailable){return true;}}
    static synchronized void prepare(Context c,String token,int dpi){
        if(!token.matches("[0-9a-f]{32}")||dpi<160||dpi>960)throw new IllegalArgumentException("Invalid initial desktop DPI");
        reconcile(c);
        SharedPreferences p=prefs(c);String previous=p.getString("token","");
        if(!previous.isEmpty()){
            if(previous.equals(token)&&p.getInt("applied",-1)==dpi&&Integer.toString(dpi).equals(Settings.Global.getString(c.getContentResolver(),KEY)))return;
            throw new IllegalStateException("Earlier desktop DPI recovery is still pending");
        }
        boolean primary=false;for(Display d:displays(c)){primary|=d.getDisplayId()==0;if(external(d))throw new IllegalStateException("An external display is already active");}
        if(!primary)throw new IllegalStateException("Display visibility is unavailable");
        String original=Settings.Global.getString(c.getContentResolver(),KEY);
        boolean saved=p.edit().putString("token",token).putString("original",original==null?"":original).putBoolean("missing",original==null)
            .putInt("applied",dpi).putLong("created",SystemClock.elapsedRealtime()).putInt("boot",Settings.Global.getInt(c.getContentResolver(),"boot_count",0))
            .putBoolean("seen",false).putBoolean("helper_seen",false).putBoolean("finish",false).commit();
        if(!saved)throw new IllegalStateException("Cannot persist desktop DPI recovery");
        if(!Settings.Global.putInt(c.getContentResolver(),KEY,dpi)||!Integer.toString(dpi).equals(Settings.Global.getString(c.getContentResolver(),KEY)))throw new IllegalStateException("Initial desktop DPI was not applied");
        new android.os.Handler(android.os.Looper.getMainLooper()).postDelayed(()->NativeSessionBridge.refreshPresenceAsync(c),61000);
    }
    static synchronized void helperConfirmed(Context c,String token){SharedPreferences p=prefs(c);if(token.equals(p.getString("token","")))p.edit().putBoolean("helper_seen",true).apply();}
    static synchronized void finish(Context c,String token){SharedPreferences p=prefs(c);if(token.equals(p.getString("token","")))p.edit().putBoolean("finish",true).commit();reconcile(c);}
    static synchronized void reconcile(Context c){
        SharedPreferences p=prefs(c);String token=p.getString("token","");if(token.isEmpty())return;
        try {
            boolean primary=false,owned=false,foreign=false;
            for(Display d:displays(c)){
                primary|=d.getDisplayId()==0;
                if(("DXM-Native-"+token.substring(0,12)).equals(d.getName()))owned=true;
                else if(external(d))foreign=true;
            }
            if(!primary)return;
            if(owned&&!p.getBoolean("seen",false))p.edit().putBoolean("seen",true).commit();
            boolean bootChanged=p.getInt("boot",0)!=Settings.Global.getInt(c.getContentResolver(),"boot_count",0);
            if(!NativeDensityRestorePolicy.mayRestore(owned,foreign,p.getBoolean("seen",false),p.getBoolean("finish",false),bootChanged,p.getBoolean("helper_seen",false),SystemClock.elapsedRealtime()-p.getLong("created",0)))return;
            String current=Settings.Global.getString(c.getContentResolver(),KEY),applied=Integer.toString(p.getInt("applied",-1));
            if(applied.equals(current)){
                String original=p.getBoolean("missing",false)?null:p.getString("original","");
                if(!Settings.Global.putString(c.getContentResolver(),KEY,original)||!Objects.equals(original,Settings.Global.getString(c.getContentResolver(),KEY)))return;
            }
            // A user/other owner changing the value wins; never overwrite it.
            p.edit().clear().commit();
        }catch(RuntimeException failure){android.util.Log.w("DXNativeDensity","Desktop DPI recovery deferred: "+failure.getClass().getSimpleName());}
    }
}

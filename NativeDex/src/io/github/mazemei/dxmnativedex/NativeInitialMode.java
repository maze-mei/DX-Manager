package io.github.mazemei.dxmnativedex;
import java.util.Locale;
import com.scrcpydex.server.DexActivator;

/** Initial capability candidates; Samsung actual metrics are always verified. */
public final class NativeInitialMode {
    public static int initialDpi(int requested){
        if(requested<48||requested>960)throw new IllegalArgumentException("Unsupported requested native DPI.");
        return Math.max(160,requested);
    }
    public static boolean supported(int width,int height){return width==1600&&height==900||width==1920&&height==1080;}
    public static String formats(int width,int height){
        int nativeMode,cea,vesa;
        if(width==1600&&height==900){nativeMode=0xa1;cea=1;vesa=(1<<20)|(1<<21);}
        // Retain30fps source compatibility; capture FPS is controlled separately by scrcpy.
        else if(width==1920&&height==1080)return DexActivator.DEFAULT_VIDEO_FORMATS;
        else throw new IllegalArgumentException("Initial native mode is not validated for "+width+"x"+height);
        String common=String.format(Locale.ROOT,"%08x %08x 00000000 10 0000 001f 11 %04x %04x",cea,vesa,width,height);
        return String.format(Locale.ROOT,"%02x 00 01 10 ",nativeMode)+common+", 02 10 "+common;
    }
}

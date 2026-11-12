package io.github.mazemei.dxmnativedex;

import java.util.function.BooleanSupplier;

/** Fixed target only; no arbitrary package or shell executor is exposed. */
public final class NativeBrowserCleanup {
    public static final String PACKAGE = "com.sec.android.app.sbrowser";
    public interface Platform { void forceStop(String packageName, int userId) throws Exception; }
    private final Platform platform;
    private final BooleanSupplier owned;
    public NativeBrowserCleanup(Platform platform, BooleanSupplier owned) { this.platform=platform; this.owned=owned; }
    public String stop() throws Exception {
        if(!owned.getAsBoolean())return "NOT_OWNED";
        platform.forceStop(PACKAGE,-2);
        return "STOP_REQUESTED";
    }
    public static NativeBrowserCleanup samsung(BooleanSupplier owned) {
        return new NativeBrowserCleanup((pkg,user)->{
            Object service=Class.forName("android.app.ActivityManager").getDeclaredMethod("getService").invoke(null);
            Class.forName("android.app.IActivityManager").getMethod("forceStopPackage",String.class,int.class).invoke(service,pkg,user);
        },owned);
    }
}
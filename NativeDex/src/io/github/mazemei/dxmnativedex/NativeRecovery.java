package io.github.mazemei.dxmnativedex;

import android.os.Handler;
import android.os.HandlerThread;
import com.scrcpydex.server.DisplayWatch;
import com.scrcpydex.server.Ln;
import com.scrcpydex.server.wrappers.DisplayManager;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.nio.charset.StandardCharsets;
import java.util.Properties;
import java.util.concurrent.TimeUnit;

/** Durable, token-scoped recovery marker; no Companion APK or global overlay changes. */
public final class NativeRecovery {
    private static final File FILE = new File("/data/local/tmp/dxm-github-native-dex.lease");
    public static Properties read() throws Exception {
        if (!FILE.exists()) return null;
        Properties p = new Properties();
        try (FileInputStream input = new FileInputStream(FILE)) { p.load(input); }
        NativeIdentity.validate(p.getProperty("token"));
        if (!"1".equals(p.getProperty("version"))) throw new IllegalStateException("Unknown native recovery record.");
        return p;
    }
    public static synchronized void save(Properties p) throws Exception {
        File temp = new File(FILE.getPath() + ".new");
        try (FileOutputStream out = new FileOutputStream(temp)) { p.store(out, "DX Manager private native DeX recovery"); out.getFD().sync(); }
        temp.setReadable(false, false); temp.setReadable(true, true);
        temp.setWritable(false, false); temp.setWritable(true, true);
        if (!temp.renameTo(FILE)) throw new IllegalStateException("Could not save native recovery record.");
    }
    public static synchronized void clear(String token) throws Exception {
        Properties p = read();
        if (p != null && token.equals(p.getProperty("token")) && !FILE.delete()) throw new IllegalStateException("Could not clear native recovery record.");
    }
    public static boolean helperAlive(Properties p) {
        try {
            String pid = p.getProperty("pid", "");
            if (!pid.matches("[1-9][0-9]{0,8}")) return true;
            File cmdline = new File("/proc/" + pid + "/cmdline");
            if (!cmdline.exists()) return false;
            try (FileInputStream in = new FileInputStream(cmdline)) {
                byte[] b = new byte[4096]; int n = in.read(b);
                String command = n > 0 ? new String(b, 0, n, StandardCharsets.UTF_8) : "";
                return command.contains("NativeDexServer") && command.contains(p.getProperty("token"));
            }
        } catch (Exception e) { return true; }
    }
    public static boolean cleanup(DisplayManager manager, String token, int settleMs) {
        return cleanup(manager, token, settleMs, true);
    }
    public static synchronized void configurePower(String token, boolean companion, boolean restore, String original) throws Exception {
        Properties record = read();
        if (record == null || !token.equals(record.getProperty("token"))) throw new IllegalStateException("Native power ownership changed.");
        NativePowerRecovery.configure(record, companion, restore, original);
        save(record);
    }
    public static synchronized void configureFeature(String token,boolean enabled) throws Exception {
        Properties record=read();
        if(record==null||!token.equals(record.getProperty("token")))throw new IllegalStateException("Native policy ownership changed.");
        record.setProperty("retained.policy",Boolean.toString(enabled));save(record);
    }
    public static boolean usesCompanion(String token) {
        try { Properties record=read(); return record!=null&&token.equals(record.getProperty("token"))&&NativePowerRecovery.usesCompanion(record); }
        catch(Exception e) { Ln.w("Native Companion ownership status: "+e); return false; }
    }
    private static String settings(String... args) throws Exception {
        java.util.List<String> command = new java.util.ArrayList<>(); command.add("/system/bin/settings");
        java.util.Collections.addAll(command, args);
        Process process = new ProcessBuilder(command).redirectErrorStream(true).start();
        if (!process.waitFor(3,TimeUnit.SECONDS)) { process.destroy(); throw new IllegalStateException("Power recovery timed out."); }
        java.io.ByteArrayOutputStream output = new java.io.ByteArrayOutputStream();
        byte[] buffer = new byte[128]; int count;
        while ((count = process.getInputStream().read(buffer)) > 0) {
            if (output.size()+count>1024) throw new IllegalStateException("Power recovery output limit.");
            output.write(buffer,0,count);
        }
        if (process.exitValue()!=0) throw new IllegalStateException("Power recovery command failed.");
        return output.toString(StandardCharsets.UTF_8.name()).trim();
    }
    public static String setManagedStayAwake(boolean enabled) throws Exception {
        String expected=enabled?"7":"0";
        if(!expected.equals(settings("get","global","stay_on_while_plugged_in")))
            settings("put","global","stay_on_while_plugged_in",expected);
        return expected.equals(settings("get","global","stay_on_while_plugged_in"))?(enabled?"ON":"OFF"):"ERROR";
    }
    private static synchronized void restorePower(String token) throws Exception {
        NativePowerRecovery.restore(read(), token, new NativePowerRecovery.Settings() {
            public String read() throws Exception { return settings("get","global","stay_on_while_plugged_in"); }
            public void restore(String original) throws Exception {
                if (original.equals("missing")) settings("delete","global","stay_on_while_plugged_in");
                else settings("put","global","stay_on_while_plugged_in",original);
                String actual=read();
                if (!(original.equals("missing") ? actual.equals("null") || actual.isEmpty() : original.equals(actual)))
                    throw new IllegalStateException("Power recovery was not confirmed.");
            }
        });
    }
    public static boolean cleanup(DisplayManager manager, String token, int settleMs, boolean restoreOfflinePower) {
        HandlerThread thread = new HandlerThread("DXM-Native-Recovery-Display");
        thread.start();
        DisplayWatch watch = new DisplayWatch(NativeIdentity.name(token));
        try {
            watch.startListening(new Handler(thread.getLooper()));
            long deadline = System.nanoTime() + java.util.concurrent.TimeUnit.MILLISECONDS.toNanos(settleMs);
            int quiet = 0;
            Properties record = read();
            boolean starting = record != null && token.equals(record.getProperty("token")) && !"active".equals(record.getProperty("phase"));
            long minimumQuietTime = starting ? deadline - java.util.concurrent.TimeUnit.SECONDS.toNanos(1) : 0;
            while (System.nanoTime() < deadline) {
                // Unknown status fails closed; a different active receiver is never disconnected.
                if (!manager.disconnectOwned(NativeIdentity.address(token))) { quiet = 0; }
                else if (!watch.isDisplayPresent()) {
                    if (++quiet >= 5 && System.nanoTime() >= minimumQuietTime) {
                        if (restoreOfflinePower) restorePower(token);
                        clear(token); return true;
                    }
                } else quiet = 0;
                Thread.sleep(200);
            }
        } catch (Exception e) { Ln.w("Native recovery remains pending: " + e); }
        finally { watch.close(); thread.quitSafely(); }
        return false;
    }
}

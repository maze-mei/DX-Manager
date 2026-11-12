package io.github.mazemei.dxmnativedex;

import android.os.Looper;
import com.scrcpydex.server.Workarounds;
import com.scrcpydex.server.wrappers.DisplayManager;
import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicLong;

/** Independent shell-UID guard: parent crash/EOF does not rely on its shutdown hook. */
public final class NativeDexGuardian {
    public static void main(String[] args) throws Exception {
        String token = NativeIdentity.validate(args.length == 1 ? args[0] : null);
        Looper.prepareMainLooper();
        Workarounds.apply();
        DisplayManager manager = new DisplayManager();
        AtomicBoolean armed = new AtomicBoolean(false), done = new AtomicBoolean(false);
        AtomicBoolean disarmed = new AtomicBoolean(false);
        AtomicLong lastPing = new AtomicLong(System.nanoTime());
        Thread reader = new Thread(() -> {
            try {
                BufferedReader input = new BufferedReader(new InputStreamReader(System.in, StandardCharsets.US_ASCII));
                String line;
                while ((line = input.readLine()) != null && line.length() <= 128) {
                    if (line.equals("PING " + token)) lastPing.set(System.nanoTime());
                    else if (line.equals("ARM " + token)) { armed.set(true); lastPing.set(System.nanoTime()); }
                    else if (line.equals("DISARM " + token)) { disarmed.set(true); break; }
                }
            } catch (Exception ignored) {} finally { done.set(true); }
        }, "DXM-Native-Guard-Control");
        reader.setDaemon(true);
        reader.start();
        System.out.println("DXGUARD1|" + token + "|READY|" + android.os.Process.myPid());
        System.out.flush();
        while (!done.get() && System.nanoTime() - lastPing.get() < 6000000000L) Thread.sleep(100);
        boolean clean = disarmed.get() || !armed.get();
        boolean notify = false;
        if (!clean) {
            notify = NativeRecovery.usesCompanion(token);
            clean = NativeRecovery.cleanup(manager, token, 6000);
        }
        if(clean && armed.get() && !disarmed.get() && notify)NativeDexServer.notifyCompanion(token,true);
        System.out.println("DXGUARD1|" + token + "|STOPPED|" + (clean ? "1" : "0"));
        System.out.flush();
        System.exit(clean ? 0 : 2);
    }
}

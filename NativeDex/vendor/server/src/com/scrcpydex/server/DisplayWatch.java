// DX Manager modifications (2026-10-26): session ownership, cancellation and bounded resource cleanup.
// Derived from ScrcpyDeX v1.1, Apache-2.0; see NativeDex/upstream.json and vendor/LICENSE.
package com.scrcpydex.server;

import android.os.Handler;
import android.os.Looper;

import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.lang.reflect.Proxy;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * Event-Driven Samsung DeX Display Monitor.
 * 
 * Replaces slow and unstable polling via "dumpsys display | grep ScrcpyDeX".
 * Uses DisplayManagerGlobal directly via native in-memory IPC,
 * listening for display creation events to capture the DeX display ID
 * at the exact moment it appears in the system.
 */
public class DisplayWatch {
    private final String targetDisplayName;
    private Object registeredListener;

    private final Object displayManagerGlobal;
    private final CountDownLatch displayLatch = new CountDownLatch(1);
    private final AtomicInteger detectedDisplayId = new AtomicInteger(-1);

    private volatile int width = 1920;
    private volatile int height = 1080;
    private volatile int dpi = 160;

    public DisplayWatch(String targetDisplayName) {
        this.targetDisplayName = targetDisplayName;
        try {
            Class<?> dmgClass = Class.forName("android.hardware.display.DisplayManagerGlobal");
            Method getInstance = dmgClass.getDeclaredMethod("getInstance");
            this.displayManagerGlobal = getInstance.invoke(null);
        } catch (Exception e) {
            throw new RuntimeException("Failed to obtain DisplayManagerGlobal", e);
        }
    }

    /**
     * Starts listening for display creation and checks if the display already exists.
     */
    public void startListening(Handler handler) {
        // 1. Check if the display is already active prior to listener registration
        if (checkExistingDisplays()) {
            return;
        }

        // 2. Register native display listener via dynamic Proxy
        try {
            Class<?> listenerClass = Class.forName("android.hardware.display.DisplayManager$DisplayListener");
            Object listenerProxy = Proxy.newProxyInstance(
                listenerClass.getClassLoader(),
                new Class<?>[]{listenerClass},
                (proxy, method, args) -> {
                    String name = method.getName();
                    if ("onDisplayAdded".equals(name) || "onDisplayChanged".equals(name)) {
                        int displayId = (int) args[0];
                        inspectDisplay(displayId);
                    }
                    return null;
                }
            );

            registeredListener = listenerProxy;
            // Try to register with handler
            try {
                displayManagerGlobal.getClass()
                    .getMethod("registerDisplayListener", listenerClass, Handler.class, long.class, String.class)
                    .invoke(displayManagerGlobal, listenerProxy, handler, 7L, FakeContext.PACKAGE_NAME);
            } catch (NoSuchMethodException e1) {
                try {
                    displayManagerGlobal.getClass()
                        .getMethod("registerDisplayListener", listenerClass, Handler.class, long.class)
                        .invoke(displayManagerGlobal, listenerProxy, handler, 7L);
                } catch (NoSuchMethodException e2) {
                    displayManagerGlobal.getClass()
                        .getMethod("registerDisplayListener", listenerClass, Handler.class)
                        .invoke(displayManagerGlobal, listenerProxy, handler);
                }
            }
            Ln.d("DisplayListener registered in DisplayManagerGlobal.");
        } catch (Exception e) {
            Ln.w("Failed to register DisplayListener via Proxy: " + e.getMessage() + ". Using active polling fallback.");
        }
    }

    /**
     * Waits until the DeX display is detected, up to a timeout.
     * 
     * @param timeoutSeconds Timeout in seconds
     * @return DeX display ID, or -1 if timed out
     */
    public int waitForDisplay(int timeoutSeconds) throws InterruptedException {
        long deadline = System.currentTimeMillis() + (timeoutSeconds * 1000L);
        while (System.currentTimeMillis() < deadline) {
            if (detectedDisplayId.get() != -1) {
                return detectedDisplayId.get();
            }
            if (checkExistingDisplays()) {
                return detectedDisplayId.get();
            }
            if (displayLatch.await(200, TimeUnit.MILLISECONDS)) {
                return detectedDisplayId.get();
            }
        }
        return -1;
    }

    private boolean checkExistingDisplays() {
        try {
            Method getDisplayIds = displayManagerGlobal.getClass().getMethod("getDisplayIds");
            int[] ids = (int[]) getDisplayIds.invoke(displayManagerGlobal);
            if (ids != null) {
                for (int id : ids) {
                    if (inspectDisplay(id)) {
                        return true;
                    }
                }
            }
        } catch (Exception e) {
            Ln.w("Error querying getDisplayIds: " + e.getMessage());
        }
        return false;
    }

    private boolean inspectDisplay(int displayId) {
        try {
            Method getDisplayInfo = displayManagerGlobal.getClass().getMethod("getDisplayInfo", int.class);
            Object displayInfo = getDisplayInfo.invoke(displayManagerGlobal, displayId);
            if (displayInfo == null) return false;

            Class<?> cls = displayInfo.getClass();
            Field nameField = cls.getDeclaredField("name");
            nameField.setAccessible(true);
            String name = (String) nameField.get(displayInfo);

            if (targetDisplayName.equals(name)) {
                int oldId = detectedDisplayId.get(), oldWidth = width, oldHeight = height, oldDpi = dpi;
                try {
                    Field wField = cls.getDeclaredField("logicalWidth");
                    Field hField = cls.getDeclaredField("logicalHeight");
                    Field dpiField = cls.getDeclaredField("logicalDensityDpi");
                    wField.setAccessible(true);
                    hField.setAccessible(true);
                    dpiField.setAccessible(true);

                    this.width = wField.getInt(displayInfo);
                    this.height = hField.getInt(displayInfo);
                    this.dpi = dpiField.getInt(displayInfo);
                } catch (Throwable ignored) {}

                if (oldId != displayId || oldWidth != width || oldHeight != height || oldDpi != dpi)
                    Ln.i("Samsung DeX display detected: ID=" + displayId +
                         " (" + width + "x" + height + " @ " + dpi + "dpi)");
                detectedDisplayId.set(displayId);
                displayLatch.countDown();
                return true;
            }
        } catch (Exception ignored) {}
        return false;
    }

    public boolean isDisplayPresent() { return checkExistingDisplays(); }

    public void close() {
        if (registeredListener == null) return;
        try {
            Class<?> listenerClass = Class.forName("android.hardware.display.DisplayManager$DisplayListener");
            displayManagerGlobal.getClass().getMethod("unregisterDisplayListener", listenerClass)
                .invoke(displayManagerGlobal, registeredListener);
        } catch (Exception ex) { Ln.d("Display listener cleanup: " + ex); }
        registeredListener = null;
    }

    public int getDetectedDisplayId() {
        return detectedDisplayId.get();
    }

    public int getWidth() {
        return width;
    }

    public int getHeight() {
        return height;
    }

    public int getDpi() {
        return dpi;
    }
}

/*
 * SPDX-License-Identifier: Apache-2.0
 * Samsung loopback configuration method follows ScrcpyDeX, Copyright 2026
 * Aureliano Peixoto and ScrcpyDeX Contributors. Modified for DX Manager:
 * reflection-only build, fail-closed preflight, unique session identity,
 * before/after display selection and ownership-checked disconnection.
 */
package com.dxmanager.loopback;

import java.lang.reflect.Method;
import java.util.ArrayList;
import java.util.List;

final class SamsungDisplay {
    private final Object manager;
    private final Method connect;
    private final Method disconnect;
    private final Method status;
    private final Method ids;
    private final Method info;
    private final Class<?> binderClass;
    private final Class<?> serviceClass;
    private final String name;
    private final String address;

    SamsungDisplay(String token) throws Exception {
        String manufacturer = (String) Class.forName("android.os.Build").getField("MANUFACTURER").get(null);
        int sdk = Class.forName("android.os.Build$VERSION").getField("SDK_INT").getInt(null);
        if (!"samsung".equalsIgnoreCase(manufacturer) || sdk < 31) throw new UnsupportedOperationException("unsupported device");
        name = "DXM-" + token;
        address = "02:" + token.substring(0, 2) + ":" + token.substring(2, 4) + ":"
                + token.substring(4, 6) + ":" + token.substring(6, 8) + ":" + token.substring(8, 10);
        binderClass = Class.forName("android.os.IBinder");
        serviceClass = Class.forName("android.os.ServiceManager");
        Object binder = serviceClass.getMethod("getService", String.class).invoke(null, "display");
        Class<?> stub = Class.forName("android.hardware.display.IDisplayManager$Stub");
        manager = stub.getMethod("asInterface", binderClass).invoke(null, binder);
        if (manager == null) throw new UnsupportedOperationException("no display service");
        Class<?> api = Class.forName("android.hardware.display.IDisplayManager");
        disconnect = api.getMethod("disconnectWifiDisplay");
        status = api.getMethod("getWifiDisplayStatus");
        info = api.getMethod("getDisplayInfo", int.class);
        Method foundIds;
        try { foundIds = api.getMethod("getDisplayIds", boolean.class); }
        catch (NoSuchMethodException e) { foundIds = api.getMethod("getDisplayIds"); }
        ids = foundIds;
        connect = api.getMethod("connectWifiDisplayWithConfig",
                Class.forName("android.hardware.display.SemWifiDisplayConfig"),
                Class.forName("android.hardware.display.IWifiDisplayConnectionCallback"));
        // Verify builder API exists before any device state is changed.
        config();
        wifiStatus();
    }

    void ensureIdle() throws Exception {
        Object current = wifiStatus();
        int state = ((Integer) current.getClass().getMethod("getActiveDisplayState").invoke(current)).intValue();
        Object active = current.getClass().getMethod("getActiveDisplay").invoke(current);
        if (state != 0 || active != null || SessionPolicy.hasExternal(displays())) {
            throw new IllegalStateException("display already active");
        }
        // Samsung standalone/transitioning desktop mode may exist without an external display yet.
        Object binder = serviceClass.getMethod("getService", String.class).invoke(null, "desktopmode");
        if (binder != null) {
            Class<?> desktopApi = Class.forName("com.samsung.android.desktopmode.IDesktopMode");
            Object desktop = Class.forName("com.samsung.android.desktopmode.IDesktopMode$Stub")
                    .getMethod("asInterface", binderClass).invoke(null, binder);
            Object desktopState = desktopApi.getMethod("getDesktopModeState").invoke(desktop);
            if (desktopState == null) throw new UnsupportedOperationException("desktop state unavailable");
            int enabled = ((Integer) desktopState.getClass().getMethod("getEnabled").invoke(desktopState)).intValue();
            if (enabled != 2) throw new IllegalStateException("desktop mode not idle");
        }
    }

    List<SessionPolicy.Display> displays() throws Exception {
        int[] values = (int[]) (ids.getParameterTypes().length == 0
                ? ids.invoke(manager) : ids.invoke(manager, Boolean.FALSE));
        if (values == null) throw new UnsupportedOperationException("display IDs unavailable");
        List<SessionPolicy.Display> result = new ArrayList<SessionPolicy.Display>();
        for (int value : values) {
            Object display = info.invoke(manager, Integer.valueOf(value));
            if (display != null) result.add(new SessionPolicy.Display(value, display.getClass().getField("type").getInt(display)));
        }
        return result;
    }

    void connect() throws Exception {
        // A null optional callback avoids Binder callback stubs; readiness is verified independently.
        connect.invoke(manager, config(), null);
    }

    boolean ownsActive() throws Exception {
        Object current = wifiStatus();
        Object active = current.getClass().getMethod("getActiveDisplay").invoke(current);
        if (active == null) return false;
        String actualAddress = (String) active.getClass().getMethod("getDeviceAddress").invoke(active);
        String actualName = (String) active.getClass().getMethod("getDeviceName").invoke(active);
        return SessionPolicy.owns(name, address, actualName, actualAddress);
    }

    boolean hasForeignActive() throws Exception {
        Object current = wifiStatus();
        Object active = current.getClass().getMethod("getActiveDisplay").invoke(current);
        return active != null && !ownsActive();
    }

    boolean disconnectOwned() throws Exception {
        // Never disconnect an unrelated/new session even if ours disappeared or was replaced.
        if (!ownsActive()) return false;
        disconnect.invoke(manager);
        return true;
    }

    boolean isWifiDisconnected() throws Exception {
        Object current = wifiStatus();
        int state = ((Integer) current.getClass().getMethod("getActiveDisplayState").invoke(current)).intValue();
        if (state != 0 || current.getClass().getMethod("getActiveDisplay").invoke(current) != null) return false;
        for (SessionPolicy.Display item : displays()) if (item.type == 3) return false;
        return true;
    }

    private Object wifiStatus() throws Exception {
        Object current = status.invoke(manager);
        if (current == null) throw new UnsupportedOperationException("wifi status unavailable");
        return current;
    }

    private Object config() throws Exception {
        Class<?> builderClass = Class.forName("android.hardware.display.SemWifiDisplayConfig$Builder");
        Object builder = builderClass.getDeclaredConstructor().newInstance();
        builderClass.getMethod("setApConnection", String.class, String.class, String.class, String.class)
                .invoke(builder, "127.0.0.1", "7236", name, address);
        builderClass.getMethod("setMode", int.class).invoke(builder, Integer.valueOf(2));
        return builderClass.getMethod("build").invoke(builder);
    }
}

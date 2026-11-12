package com.scrcpydex.server.wrappers;

import java.lang.reflect.Method;
import android.os.IBinder;

/**
 * Reflection wrapper for Android's hidden android.os.ServiceManager class.
 * 
 * Allows obtaining Binder references for core operating system services
 * (such as "display", "input", "window") from a process executed via app_process.
 */
public final class ServiceManager {
    private static final Method GET_SERVICE_METHOD;

    static {
        try {
            Class<?> serviceManagerClass = Class.forName("android.os.ServiceManager");
            GET_SERVICE_METHOD = serviceManagerClass.getMethod("getService", String.class);
        } catch (Exception e) {
            throw new AssertionError("Critical failure loading android.os.ServiceManager via reflection", e);
        }
    }

    private ServiceManager() {
        // Utility class
    }

    /**
     * Returns the IBinder interface for a system service by its public name.
     * 
     * @param serviceName Service name (e.g. "display", "input", "package")
     * @return IBinder for IPC communication
     */
    public static IBinder getService(String serviceName) {
        try {
            IBinder binder = (IBinder) GET_SERVICE_METHOD.invoke(null, serviceName);
            if (binder == null) {
                throw new IllegalStateException("System service not available: " + serviceName);
            }
            return binder;
        } catch (Exception e) {
            throw new RuntimeException("Error querying service " + serviceName + " in ServiceManager", e);
        }
    }
}

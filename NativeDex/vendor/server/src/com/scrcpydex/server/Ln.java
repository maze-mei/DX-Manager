// DX Manager modifications (2026-10-26): session ownership, cancellation and bounded resource cleanup.
// Derived from ScrcpyDeX v1.1, Apache-2.0; see NativeDex/upstream.json and vendor/LICENSE.
package com.scrcpydex.server;

import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

/**
 * Logging utility for ScrcpyDex Server.
 * 
 * Writes directly to stdout/stderr with clean formatting and timestamps.
 * Because the server runs via app_process (native command-line process
 * on Android), logs are directed to the standard process output, which can be
 * read directly via ADB or redirected to a log file.
 */
public final class Ln {
    private static final String TAG = "[ScrcpyDeX]";
    private static final SimpleDateFormat DATE_FORMAT = 
            new SimpleDateFormat("HH:mm:ss.SSS", Locale.US);

    private Ln() {
        // Static utility class
    }

    private static synchronized String getTimestamp() {
        return DATE_FORMAT.format(new Date());
    }

    public static void i(String message) {
        System.err.println(getTimestamp() + " " + TAG + " [INFO] " + message);
    }

    public static void w(String message) {
        System.err.println(getTimestamp() + " " + TAG + " [WARN] " + message);
    }

    public static void e(String message) {
        System.err.println(getTimestamp() + " " + TAG + " [ERROR] " + message);
    }

    public static void e(String message, Throwable throwable) {
        System.err.println(getTimestamp() + " " + TAG + " [ERROR] " + message);
        if (throwable != null) {
            throwable.printStackTrace(System.err);
        }
    }

    public static void d(String message) {
        // Enabled for debugging during the interoperability analysis phase
        System.err.println(getTimestamp() + " " + TAG + " [DEBUG] " + message);
    }
}

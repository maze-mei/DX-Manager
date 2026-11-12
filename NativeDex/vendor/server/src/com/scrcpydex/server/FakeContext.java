package com.scrcpydex.server;

import android.content.ContextWrapper;

/**
 * Simulated Context ("FakeContext") for processes executed via app_process.
 * 
 * Because app_process runs as a command-line terminal process (UID shell),
 * it does not possess a default Android Application. FakeContext provides
 * the package name "com.android.shell" for system APIs that perform caller validation.
 */
public final class FakeContext extends ContextWrapper {
    public static final String PACKAGE_NAME = "com.android.shell";

    private static final FakeContext INSTANCE = new FakeContext();

    public static FakeContext get() {
        return INSTANCE;
    }

    private FakeContext() {
        super(null);
    }

    @Override
    public String getPackageName() {
        return PACKAGE_NAME;
    }

    @Override
    public String getOpPackageName() {
        return PACKAGE_NAME;
    }
}

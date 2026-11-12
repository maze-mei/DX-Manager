package io.github.mazemei.dxdisplaycleanup;

import android.Manifest;
import android.content.ContentResolver;
import android.content.Context;
import android.content.pm.PackageManager;
import android.os.Bundle;
import android.provider.Settings;

final class OverlayDisplayRepository {
    static final String SETTING_NAME = "overlay_display_devices";
    static final String REQUIRED_PERMISSION = Manifest.permission.WRITE_SECURE_SETTINGS;
    private static final String DELETE_GLOBAL_METHOD = "DELETE_global";

    enum Status {
        ACTIVE,
        INACTIVE,
        UNKNOWN,
        PERMISSION_REQUIRED,
        ERROR
    }

    static final class Snapshot {
        final Status status;
        final boolean overlayActive;
        final String rawValue;
        final String error;

        Snapshot(Status status, boolean overlayActive, String rawValue, String error) {
            this.status = status;
            this.overlayActive = overlayActive;
            this.rawValue = rawValue;
            this.error = error;
        }
    }

    private OverlayDisplayRepository() {
    }

    static Snapshot inspect(Context context) {
        try {
            String raw = Settings.Global.getString(
                    context.getContentResolver(), SETTING_NAME);
            boolean active = hasOverlayValue(raw);
            if (!hasWritePermission(context)) {
                return new Snapshot(Status.PERMISSION_REQUIRED, active, raw, null);
            }
            return new Snapshot(active ? Status.ACTIVE : Status.INACTIVE,
                    active, raw, null);
        } catch (RuntimeException exception) {
            return new Snapshot(Status.ERROR, false, null,
                    exception.getClass().getSimpleName() + ": " + exception.getMessage());
        }
    }

    static Snapshot cleanup(Context context) {
        if (!hasWritePermission(context)) {
            return inspect(context);
        }

        ContentResolver resolver = context.getContentResolver();
        try {
            try {
                Bundle result = resolver.call(
                        Settings.Global.CONTENT_URI,
                        DELETE_GLOBAL_METHOD,
                        SETTING_NAME,
                        new Bundle());
                // Some providers return null even when the delete succeeds.
                if (result != null) {
                    result.size();
                }
            } catch (IllegalArgumentException | UnsupportedOperationException unsupportedCall) {
                // Public Settings APIs route a null value through the provider's
                // delete path on supported Android versions.
                if (!Settings.Global.putString(resolver, SETTING_NAME, null)) {
                    return new Snapshot(Status.ERROR, true, null,
                            "The Settings provider rejected the delete request.");
                }
            }
        } catch (SecurityException denied) {
            return new Snapshot(Status.PERMISSION_REQUIRED, true, null,
                    denied.getMessage());
        } catch (RuntimeException exception) {
            return new Snapshot(Status.ERROR, true, null,
                    exception.getClass().getSimpleName() + ": " + exception.getMessage());
        }

        Snapshot verified = inspect(context);
        if (verified.status == Status.INACTIVE) {
            return verified;
        }
        if (verified.status == Status.ACTIVE) {
            return new Snapshot(Status.ERROR, true, verified.rawValue,
                    "The setting remained after the delete request.");
        }
        return verified;
    }

    static boolean hasWritePermission(Context context) {
        return context.checkSelfPermission(REQUIRED_PERMISSION)
                == PackageManager.PERMISSION_GRANTED;
    }

    static boolean hasOverlayValue(String value) {
        if (value == null || value.isEmpty()) {
            return false;
        }
        String normalized = value.trim();
        return !normalized.isEmpty()
                && !"none".equalsIgnoreCase(normalized)
                && !"null".equalsIgnoreCase(normalized);
    }
}

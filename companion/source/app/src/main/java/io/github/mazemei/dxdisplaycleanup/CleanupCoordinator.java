package io.github.mazemei.dxdisplaycleanup;

import android.content.Context;

final class CleanupCoordinator {
    static final class Snapshot {
        final OverlayDisplayRepository.Snapshot overlay;
        final StayAwakeRepository.Snapshot stayAwake;

        Snapshot(OverlayDisplayRepository.Snapshot overlay,
                 StayAwakeRepository.Snapshot stayAwake) {
            this.overlay = overlay;
            this.stayAwake = stayAwake;
        }

        boolean permissionRequired() {
            return overlay.status == OverlayDisplayRepository.Status.PERMISSION_REQUIRED
                    || stayAwake.status == StayAwakeRepository.Status.PERMISSION_REQUIRED;
        }

        boolean hasError() {
            return overlay.status == OverlayDisplayRepository.Status.ERROR
                    || stayAwake.status == StayAwakeRepository.Status.ERROR;
        }

        boolean anyActive() {
            return overlay.overlayActive || stayAwake.stayAwakeActive;
        }

        boolean selectedActive(CleanupPreferences.Targets targets) {
            return (targets.overlay && overlay.overlayActive)
                    || (targets.stayAwake && stayAwake.stayAwakeActive);
        }
    }

    private CleanupCoordinator() {
    }

    static Snapshot inspect(Context context) {
        OverlayDisplayRepository.Snapshot overlay=OverlayDisplayRepository.inspect(context);
        NativeSessionBridge.Snapshot nativeDisplay=NativeSessionBridge.inspect(context);
        if(nativeDisplay.active) overlay=new OverlayDisplayRepository.Snapshot(
                OverlayDisplayRepository.Status.ACTIVE,true,"Native DeX: "+nativeDisplay.detail,null);
        else if(NativeSessionBridge.hasSession(context)&&!nativeDisplay.confirmed&&!overlay.overlayActive)
            overlay=new OverlayDisplayRepository.Snapshot(OverlayDisplayRepository.Status.UNKNOWN,false,nativeDisplay.detail,null);
        return new Snapshot(overlay,StayAwakeRepository.inspect(context));
    }

    static Snapshot cleanup(Context context, boolean overlay, boolean stayAwake) {
        if (overlay) {
            if(NativeSessionBridge.hasSession(context))NativeSessionBridge.stop(context);
            else OverlayDisplayRepository.cleanup(context);
        }
        if (stayAwake) {
            StayAwakeRepository.cleanup(context);
        }
        return inspect(context);
    }

    static Snapshot cleanupSelected(Context context) {
        CleanupPreferences.Targets targets = CleanupPreferences.load(context);
        return cleanup(context, targets.overlay, targets.stayAwake);
    }

    static boolean isCleanupComplete(
            Snapshot snapshot, boolean overlay, boolean stayAwake) {
        if (snapshot == null) {
            return false;
        }
        if (overlay && (snapshot.overlay.status
                != OverlayDisplayRepository.Status.INACTIVE
                || snapshot.overlay.overlayActive)) {
            return false;
        }
        return !stayAwake || (snapshot.stayAwake.status
                == StayAwakeRepository.Status.INACTIVE
                && !snapshot.stayAwake.stayAwakeActive);
    }

    static boolean isWindowsShutdownCleanupComplete(
            Snapshot snapshot,
            boolean removeOverlay,
            boolean restoreStayAwake,
            String originalStayAwake) {
        if (snapshot == null) {
            return false;
        }
        if (removeOverlay && (snapshot.overlay.status
                != OverlayDisplayRepository.Status.INACTIVE
                || snapshot.overlay.overlayActive)) {
            return false;
        }
        if (!restoreStayAwake) {
            return true;
        }
        if (snapshot.stayAwake.status
                == StayAwakeRepository.Status.ERROR
                || snapshot.stayAwake.status
                == StayAwakeRepository.Status.PERMISSION_REQUIRED) {
            return false;
        }
        return originalStayAwakeMatches(
                snapshot.stayAwake.rawValue, originalStayAwake);
    }

    private static boolean originalStayAwakeMatches(
            String actual, String expected) {
        String normalizedActual = normalizeStoredValue(actual);
        String normalizedExpected = normalizeStoredValue(expected);
        if (normalizedActual == null || normalizedExpected == null) {
            return normalizedActual == null && normalizedExpected == null;
        }
        try {
            return Long.parseLong(normalizedActual)
                    == Long.parseLong(normalizedExpected);
        } catch (NumberFormatException ignored) {
            return normalizedActual.equals(normalizedExpected);
        }
    }

    private static String normalizeStoredValue(String value) {
        if (value == null) {
            return null;
        }
        String normalized = value.trim();
        return normalized.isEmpty() || "null".equalsIgnoreCase(normalized)
                ? null : normalized;
    }
}

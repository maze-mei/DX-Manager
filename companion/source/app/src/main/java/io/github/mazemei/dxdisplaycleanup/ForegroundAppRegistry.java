package io.github.mazemei.dxdisplaycleanup;

import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Objects;

/** No Android dependencies: change-only state and initial reconnect snapshots. */
final class ForegroundAppRegistry {
    interface Listener {
        void availability(long sequence, boolean available);
        void app(long sequence, int displayId, String packageName, long eventTime);
        void window(ForegroundWindowDetails details);
    }

    private boolean available;
    private long sequence;
    private final Map<Integer, String> packages = new LinkedHashMap<>();
    private final Map<Integer, ForegroundWindowDetails> windows = new LinkedHashMap<>();
    private Listener listener;

    synchronized boolean isAvailable() { return available; }

    synchronized void attach(Listener newListener) {
        listener = newListener;
        listener.availability(++sequence, available);
        if (available) {
            for (Map.Entry<Integer, String> entry : packages.entrySet()) {
                // A reconnect snapshot has no old event age.
                listener.app(++sequence, entry.getKey(), entry.getValue(), 0);
            }
            for (ForegroundWindowDetails details : windows.values()) listener.window(details);
        }
    }

    synchronized void detach(Listener expected) {
        if (listener == expected) listener = null;
    }

    synchronized void setAvailable(boolean value) {
        if (available == value) return;
        available = value;
        packages.clear();
        windows.clear();
        if (listener != null) listener.availability(++sequence, value);
    }

    synchronized void update(Map<Integer, String> current, long eventTime) {
        if (!available) return;
        Map<Integer, String> next = new LinkedHashMap<>();
        for (Map.Entry<Integer, String> entry : current.entrySet()) {
            if (entry.getKey() >= 0 && isPackageName(entry.getValue()))
                next.put(entry.getKey(), entry.getValue());
        }
        for (Integer displayId : packages.keySet()) {
            if (!next.containsKey(displayId) && listener != null)
                listener.app(++sequence, displayId, "", eventTime);
        }
        for (Map.Entry<Integer, String> entry : next.entrySet()) {
            if (!Objects.equals(packages.get(entry.getKey()), entry.getValue())
                    && listener != null)
                listener.app(++sequence, entry.getKey(), entry.getValue(), eventTime);
        }
        packages.clear();
        packages.putAll(next);
    }

    synchronized void updateWindows(Map<Integer, ForegroundWindowDetails> current,
            java.util.Set<Integer> stateChangedDisplays) {
        if (!available) return;
        Map<Integer, ForegroundWindowDetails> next = new LinkedHashMap<>();
        for (ForegroundWindowDetails details : current.values()) {
            if (details == null || details.displayId < 0 || details.windowId < 0
                    || !isPackageName(details.packageName)
                    || !ForegroundWindowDetails.isBrowserHost(details.packageName)) continue;
            next.put(details.displayId, details);
            boolean stateEvent = stateChangedDisplays.contains(-1)
                    || stateChangedDisplays.contains(details.displayId);
            if (listener != null && (stateEvent || !details.sameWindow(windows.get(details.displayId))))
                listener.window(details);
        }
        windows.clear(); windows.putAll(next);
    }

    static boolean isPackageName(String value) {
        return value != null && value.length() <= 255
                && value.matches("[a-zA-Z_][a-zA-Z0-9_]*(\\.[a-zA-Z_][a-zA-Z0-9_]*)+");
    }
}

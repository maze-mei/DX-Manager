package io.github.mazemei.dxdisplaycleanup;

import android.accessibilityservice.AccessibilityService;
import android.content.Intent;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.util.Log;
import android.util.SparseArray;
import android.view.accessibility.AccessibilityEvent;
import android.view.accessibility.AccessibilityNodeInfo;
import android.view.accessibility.AccessibilityWindowInfo;

import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.HashSet;
import java.util.Set;
import java.util.Map;

/** Tracks keyboard-focused application windows, never notification/text content. */
public final class ForegroundAppAccessibilityService extends AccessibilityService {
    private static final String TAG = "DXForeground";
    static final ForegroundAppRegistry REGISTRY = new ForegroundAppRegistry();
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final Map<Integer, String> windowPackages = new HashMap<>();
    private final Map<Integer, String> stateClasses = new HashMap<>();
    private final Map<Integer, String> rootClasses = new HashMap<>();
    private final Set<Integer> stateChangedDisplays = new HashSet<>();
    private ForegroundWindowDetails.EventSource pendingSource = ForegroundWindowDetails.EventSource.empty();
    private boolean refreshQueued;
    private long pendingEventTime;
    private boolean active;

    @Override
    protected void onServiceConnected() {
        super.onServiceConnected();
        active = true;
        REGISTRY.setAvailable(true);
        Log.i(TAG, "Foreground app accessibility service connected.");
        scheduleRefresh(SystemClock.uptimeMillis());
    }

    @Override
    public void onAccessibilityEvent(AccessibilityEvent event) {
        if (!active || event == null) return;
        int type = event.getEventType();
        if (type != AccessibilityEvent.TYPE_WINDOW_STATE_CHANGED
                && type != AccessibilityEvent.TYPE_WINDOWS_CHANGED
                && type != AccessibilityEvent.TYPE_VIEW_FOCUSED) return;

        CharSequence packageName = event.getPackageName();
        if (event.getWindowId() >= 0 && packageName != null
                && ForegroundAppRegistry.isPackageName(packageName.toString())) {
            windowPackages.put(event.getWindowId(), packageName.toString());
        }
        String sourcePackage = packageName == null ? "" : packageName.toString();
        String sourceClass = event.getClassName() == null ? "" : event.getClassName().toString();
        pendingSource = new ForegroundWindowDetails.EventSource(
                type, event.getDisplayId(), event.getWindowId(), sourcePackage, sourceClass);
        if (type == AccessibilityEvent.TYPE_WINDOW_STATE_CHANGED) {
            stateChangedDisplays.add(event.getDisplayId());
            if (event.getWindowId() >= 0)
                stateClasses.put(event.getWindowId(), ForegroundWindowDetails.plain(sourceClass));
        }
        scheduleRefresh(event.getEventTime());
    }

    private void scheduleRefresh(long eventTime) {
        pendingEventTime = eventTime;
        if (refreshQueued) return;
        refreshQueued = true;
        // Coalesce callbacks already in the main queue; no timer and no polling.
        handler.post(() -> {
            refreshQueued = false;
            Set<Integer> changedDisplays = new HashSet<>(stateChangedDisplays);
            stateChangedDisplays.clear();
            if (active) refreshFocusedApps(pendingEventTime, pendingSource, changedDisplays);
        });
    }

    private void refreshFocusedApps(long eventTime, ForegroundWindowDetails.EventSource source,
            Set<Integer> changedDisplays) {
        Map<Integer, String> focused = new LinkedHashMap<>();
        Map<Integer, String> visiblePackages = new HashMap<>();
        Map<Integer, ForegroundWindowDetails> details = new LinkedHashMap<>();
        Set<Integer> visibleWindows = new HashSet<>();
        try {
            SparseArray<List<AccessibilityWindowInfo>> displays = getWindowsOnAllDisplays();
            for (int i = 0; i < displays.size(); i++) {
                List<AccessibilityWindowInfo> windows = displays.valueAt(i);
                if (windows == null) continue;
                for (AccessibilityWindowInfo window : windows) {
                    if (window == null) continue;
                    visibleWindows.add(window.getId());
                    String known = windowPackages.get(window.getId());
                    if (known != null) visiblePackages.put(window.getId(), known);
                    if (!window.isFocused()
                            || window.getType() != AccessibilityWindowInfo.TYPE_APPLICATION)
                        continue;
                    String packageName = known;
                    if (packageName == null || ForegroundWindowDetails.isBrowserHost(packageName)
                            && !rootClasses.containsKey(window.getId())) {
                        // Once per visible browser window; no node traversal or text access.
                        AccessibilityNodeInfo root = window.getRoot(0);
                        if (root != null && root.getPackageName() != null && packageName == null)
                            packageName = root.getPackageName().toString();
                        rootClasses.put(window.getId(), root == null || root.getClassName() == null
                                ? "" : ForegroundWindowDetails.plain(root.getClassName().toString()));
                    }
                    if (ForegroundAppRegistry.isPackageName(packageName)) {
                        focused.put(window.getDisplayId(), packageName);
                        visiblePackages.put(window.getId(), packageName);
                        if (ForegroundWindowDetails.isBrowserHost(packageName)) {
                            CharSequence title = window.getTitle();
                            details.put(window.getDisplayId(), new ForegroundWindowDetails(
                                    window.getDisplayId(), window.getId(), window.getType(),
                                    window.getLayer(), window.isFocused(), window.isActive(),
                                    packageName, title == null ? "" : title.toString(),
                                    stateClasses.get(window.getId()), rootClasses.get(window.getId()), source));
                        }
                    }
                }
            }
            windowPackages.clear();
            windowPackages.putAll(visiblePackages);
            stateClasses.keySet().retainAll(visibleWindows);
            rootClasses.keySet().retainAll(visibleWindows);
            REGISTRY.update(focused, eventTime);
            REGISTRY.updateWindows(details, changedDisplays);
        } catch (RuntimeException exception) {
            // Clear old app rules immediately if Android cannot report focus.
            REGISTRY.update(new LinkedHashMap<>(), eventTime);
            REGISTRY.updateWindows(new LinkedHashMap<>(), java.util.Collections.emptySet());
            Log.w(TAG, "Could not resolve focused apps; default Enter remains safe.", exception);
        }
    }

    private void disconnectTracker() {
        active = false;
        handler.removeCallbacksAndMessages(null);
        refreshQueued = false;
        windowPackages.clear();
        stateClasses.clear(); rootClasses.clear(); stateChangedDisplays.clear();
        pendingSource = ForegroundWindowDetails.EventSource.empty();
        REGISTRY.setAvailable(false);
    }

    @Override public void onInterrupt() {
        // A feedback interruption is not a service-disable event.
        REGISTRY.update(new LinkedHashMap<>(), SystemClock.uptimeMillis());
        if (active) scheduleRefresh(SystemClock.uptimeMillis());
    }
    @Override public boolean onUnbind(Intent intent) {
        disconnectTracker();
        return super.onUnbind(intent);
    }
    @Override public void onDestroy() {
        disconnectTracker();
        super.onDestroy();
    }
}

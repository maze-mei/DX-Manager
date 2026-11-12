package io.github.mazemei.dxdisplaycleanup;



/** Window metadata only; no event text, edit text, URLs or node traversal. */
final class ForegroundWindowDetails {
    static final int MAX_DETAIL_CHARS = 512;

    static final class EventSource {
        final int type, displayId, windowId;
        final String packageName, className;
        EventSource(int type, int displayId, int windowId, String packageName, String className) {
            this.type = type; this.displayId = displayId; this.windowId = windowId;
            this.packageName = plain(packageName); this.className = plain(className);
        }
        static EventSource empty() { return new EventSource(0, -1, -1, "", ""); }
    }

    final int displayId, windowId, windowType, layer;
    final boolean focused, active;
    final String packageName, title, stateClass, rootClass;
    final EventSource event;

    ForegroundWindowDetails(int displayId, int windowId, int windowType, int layer,
            boolean focused, boolean active, String packageName, String title,
            String stateClass, String rootClass, EventSource event) {
        this.displayId = displayId; this.windowId = windowId; this.windowType = windowType;
        this.layer = layer; this.focused = focused; this.active = active;
        this.packageName = plain(packageName); this.title = plain(title);
        this.stateClass = plain(stateClass); this.rootClass = plain(rootClass);
        this.event = event == null ? EventSource.empty() : event;
    }

    boolean sameWindow(ForegroundWindowDetails other) {
        return other != null && displayId == other.displayId && windowId == other.windowId
                && windowType == other.windowType && layer == other.layer
                && focused == other.focused && active == other.active
                && packageName.equals(other.packageName) && title.equals(other.title)
                && stateClass.equals(other.stateClass) && rootClass.equals(other.rootClass);
    }

    static boolean isBrowserHost(String packageName) {
        return "com.android.chrome".equals(packageName)
                || "com.chrome.beta".equals(packageName) || "com.chrome.dev".equals(packageName)
                || "com.chrome.canary".equals(packageName)
                || "com.sec.android.app.sbrowser".equals(packageName)
                || packageName != null && packageName.startsWith("org.chromium.webapk.");
    }

    static String plain(String value) {
        if (value == null) return "";
        StringBuilder result = new StringBuilder(Math.min(value.length(), MAX_DETAIL_CHARS));
        for (int i = 0; i < value.length() && result.length() < MAX_DETAIL_CHARS; i++) {
            char c = value.charAt(i);
            if (c < 32 || c == 127 || c == '\u2028' || c == '\u2029') c = ' ';
            result.append(c);
        }
        return result.toString();
    }
}

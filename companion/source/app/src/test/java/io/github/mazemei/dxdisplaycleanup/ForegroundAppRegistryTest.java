package io.github.mazemei.dxdisplaycleanup;

import org.junit.Test;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import static org.junit.Assert.*;

public final class ForegroundAppRegistryTest {
    private static final class Recorder implements ForegroundAppRegistry.Listener {
        final List<String> events = new ArrayList<>();
        long last;
        public void window(ForegroundWindowDetails details) { }
        public void availability(long sequence, boolean value) {
            assertTrue(sequence > last); last = sequence; events.add("available=" + value);
        }
        public void app(long sequence, int display, String name, long eventTime) {
            assertTrue(sequence > last); last = sequence; events.add(display + "=" + name);
        }
    }
    private static Map<Integer, String> apps(int display, String packageName) {
        Map<Integer, String> apps = new LinkedHashMap<>(); apps.put(display, packageName); return apps;
    }
    @Test public void publishesOnlyPackageChanges() {
        ForegroundAppRegistry registry = new ForegroundAppRegistry(); Recorder recorder = new Recorder();
        registry.setAvailable(true); registry.attach(recorder); recorder.events.clear();
        registry.update(apps(2, "com.openai.chatgpt"), 10);
        registry.update(apps(2, "com.openai.chatgpt"), 20);
        assertEquals(1, recorder.events.size());
        registry.update(apps(2, "com.android.chrome"), 30);
        assertEquals("2=com.android.chrome", recorder.events.get(1));
    }
    @Test public void reconnectSendsCurrentAppsWithoutWaitingForAnotherSwitch() {
        ForegroundAppRegistry registry = new ForegroundAppRegistry();
        registry.setAvailable(true); registry.update(apps(2, "ai.x.grok"), 1);
        Recorder recorder = new Recorder(); registry.attach(recorder);
        assertEquals("available=true", recorder.events.get(0));
        assertEquals("2=ai.x.grok", recorder.events.get(1));
    }
    @Test public void displayRemovalInvalidatesOnlyThatDisplay() {
        ForegroundAppRegistry registry = new ForegroundAppRegistry(); Recorder recorder = new Recorder();
        registry.setAvailable(true); registry.attach(recorder);
        Map<Integer, String> current = apps(2, "com.openai.chatgpt");
        current.put(3, "com.android.chrome"); registry.update(current, 1); recorder.events.clear();
        current.remove(2); registry.update(current, 2);
        assertEquals(1, recorder.events.size()); assertEquals("2=", recorder.events.get(0));
    }
    @Test public void disablingClearsCachedAppsAndIgnoresFurtherUpdates() {
        ForegroundAppRegistry registry = new ForegroundAppRegistry(); Recorder recorder = new Recorder();
        registry.setAvailable(true); registry.update(apps(2, "com.openai.chatgpt"), 1);
        registry.setAvailable(false); registry.update(apps(2, "ai.x.grok"), 2);
        registry.attach(recorder); assertEquals(1, recorder.events.size());
        assertEquals("available=false", recorder.events.get(0));
        registry.setAvailable(true); recorder.events.clear();
        registry.attach(recorder); assertEquals(1, recorder.events.size());
    }
    @Test public void oldListenerCannotDetachReplacement() {
        ForegroundAppRegistry registry = new ForegroundAppRegistry(); Recorder old = new Recorder();
        Recorder current = new Recorder(); registry.setAvailable(true); registry.attach(old);
        registry.attach(current); registry.detach(old); current.events.clear();
        registry.update(apps(2, "com.openai.chatgpt"), 1);
        assertEquals("2=com.openai.chatgpt", current.events.get(0));
    }
    @Test public void rejectsInvalidPackagesAndDisplayIds() {
        ForegroundAppRegistry registry = new ForegroundAppRegistry(); Recorder recorder = new Recorder();
        registry.setAvailable(true); registry.attach(recorder); recorder.events.clear();
        registry.update(apps(2, "../chatgpt"), 1); registry.update(apps(-1, "com.openai.chatgpt"), 2);
        assertTrue(recorder.events.isEmpty());
        assertFalse(ForegroundAppRegistry.isPackageName("com..openai"));
    }
}

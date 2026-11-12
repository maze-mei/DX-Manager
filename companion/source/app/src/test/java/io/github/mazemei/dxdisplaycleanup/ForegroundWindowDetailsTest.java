package io.github.mazemei.dxdisplaycleanup;

import org.junit.Test;
import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import static org.junit.Assert.*;

public final class ForegroundWindowDetailsTest {
    static final class Recorder implements ForegroundAppRegistry.Listener {
        final List<String> packages = new ArrayList<>();
        final List<ForegroundWindowDetails> windows = new ArrayList<>();
        public void availability(long sequence, boolean available) { }
        public void app(long sequence, int display, String name, long time) { packages.add(name); }
        public void window(ForegroundWindowDetails details) { windows.add(details); }
    }
    static ForegroundWindowDetails details(int windowId, String title, String stateClass) {
        return new ForegroundWindowDetails(77,windowId,1,10,true,true,"com.android.chrome",
                title,stateClass,"android.widget.FrameLayout",
                new ForegroundWindowDetails.EventSource(32,77,windowId,
                        "com.android.chrome",stateClass));
    }
    static Map<Integer,ForegroundWindowDetails> snapshot(ForegroundWindowDetails info) {
        Map<Integer,ForegroundWindowDetails> result=new LinkedHashMap<>(); result.put(77,info); return result;
    }
    @Test public void detectsWebWindowChangeWithoutChangingChromePackage() {
        ForegroundAppRegistry registry=new ForegroundAppRegistry(); Recorder recorder=new Recorder();
        registry.setAvailable(true); registry.attach(recorder);
        Map<Integer,String> apps=new LinkedHashMap<>(); apps.put(77,"com.android.chrome");
        registry.update(apps,1); registry.updateWindows(snapshot(details(1,"Chrome","ChromeActivity")),Collections.emptySet());
        registry.update(apps,2); registry.updateWindows(snapshot(details(2,"Google Gemini","WebappActivity")),Collections.emptySet());
        assertEquals(1,recorder.packages.size()); assertEquals(2,recorder.windows.size());
        assertEquals("Google Gemini",recorder.windows.get(1).title);
    }
    @Test public void suppressesRepeatedViewFocusButReportsWindowStateEvents() {
        ForegroundAppRegistry registry=new ForegroundAppRegistry(); Recorder recorder=new Recorder();
        registry.setAvailable(true); registry.attach(recorder);
        ForegroundWindowDetails info=details(2,"Google Gemini","WebappActivity");
        registry.updateWindows(snapshot(info),Collections.emptySet());
        registry.updateWindows(snapshot(info),Collections.emptySet());
        assertEquals(1,recorder.windows.size());
        registry.updateWindows(snapshot(info),Collections.singleton(77));
        assertEquals(2,recorder.windows.size());
    }
    @Test public void changedTitleOnSameWindowIsReported() {
        ForegroundAppRegistry registry=new ForegroundAppRegistry(); Recorder recorder=new Recorder();
        registry.setAvailable(true); registry.attach(recorder);
        registry.updateWindows(snapshot(details(2,"Chrome","WebappActivity")),Collections.emptySet());
        registry.updateWindows(snapshot(details(2,"Google Gemini","WebappActivity")),Collections.emptySet());
        assertEquals(2,recorder.windows.size());
    }
    @Test public void reconnectResendsMetadataAndDisablingClearsIt() {
        ForegroundAppRegistry registry=new ForegroundAppRegistry(); registry.setAvailable(true);
        registry.updateWindows(snapshot(details(2,"Google Gemini","WebappActivity")),Collections.emptySet());
        Recorder first=new Recorder(); registry.attach(first); assertEquals(1,first.windows.size());
        registry.setAvailable(false); Recorder second=new Recorder(); registry.attach(second);
        assertTrue(second.windows.isEmpty());
    }
    @Test public void metadataIsBoundedAndCannotBreakLogLines() {
        String longTitle=String.join("",Collections.nCopies(600,"A"));
        assertEquals(512,details(2,longTitle,"WebappActivity").title.length());
        assertEquals("Google Gemini label",details(2,"Google\nGemini\u2028label","WebappActivity").title);
        assertFalse(ForegroundWindowDetails.isBrowserHost("com.openai.chatgpt"));
        assertTrue(ForegroundWindowDetails.isBrowserHost("org.chromium.webapk.ae64724a399d5d5cd_v2"));
    }
}

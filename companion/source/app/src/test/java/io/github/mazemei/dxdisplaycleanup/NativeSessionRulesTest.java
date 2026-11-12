package io.github.mazemei.dxdisplaycleanup;

import static org.junit.Assert.*;
import org.junit.Test;

public final class NativeSessionRulesTest {
    @Test public void nativeGraceIsSeparateFromLegacyChoices() {
        assertEquals(30,NativeSessionRules.DISCONNECT_GRACE_SECONDS);
        for(int value:GuardianPreferences.DELAY_VALUES) {
            assertEquals(value,GuardianPreferences.normalizeDelaySeconds(value));
            assertEquals(30,NativeSessionRules.DISCONNECT_GRACE_SECONDS);
        }
        assertEquals(0,GuardianPreferences.DEFAULT_DELAY_SECONDS);
    }
    @Test public void fixedGraceStateStillSupportsImmediateAudioAndPowerPolicy() {
        RetainedSessionPolicy.State state=RetainedSessionPolicy.parse("SESSION|4|10|0|0|1|0|1|30");
        assertTrue(state.applicable());assertFalse(state.dex());assertFalse(state.power);
        assertEquals(30,state.delay);
    }
}

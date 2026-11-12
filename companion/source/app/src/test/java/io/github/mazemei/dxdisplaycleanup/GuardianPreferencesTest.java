package io.github.mazemei.dxdisplaycleanup;

import static org.junit.Assert.assertArrayEquals;
import static org.junit.Assert.assertEquals;

import org.junit.Test;

public final class GuardianPreferencesTest {
    @Test
    public void delayChoicesMatchTheDisplayedOrder() {
        assertArrayEquals(
                new int[] { 0, 60, 300, 600, 1800, -1 },
                GuardianPreferences.DELAY_VALUES);
        assertEquals(0, GuardianPreferences.DEFAULT_DELAY_SECONDS);
    }

    @Test
    public void removedLegacyChoicesMigrateToImmediateCleanup() {
        assertEquals(0, GuardianPreferences.normalizeDelaySeconds(30));
        assertEquals(0, GuardianPreferences.normalizeDelaySeconds(180));
        assertEquals(300, GuardianPreferences.normalizeDelaySeconds(300));
        assertEquals(600, GuardianPreferences.normalizeDelaySeconds(600));
        assertEquals(-1, GuardianPreferences.normalizeDelaySeconds(-1));
    }
}

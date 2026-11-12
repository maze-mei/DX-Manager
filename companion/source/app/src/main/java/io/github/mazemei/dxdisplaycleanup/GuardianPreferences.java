package io.github.mazemei.dxdisplaycleanup;

import android.content.Context;
import android.content.SharedPreferences;

final class GuardianPreferences {
    static final int DISABLED = -1;
    static final int DEFAULT_DELAY_SECONDS = 0;
    static final int[] DELAY_VALUES = {
            0, 60, 300, 600, 1800, DISABLED
    };

    private static final String FILE_NAME = "guardian_preferences";
    private static final String KEY_DELAY_SECONDS =
            "disconnect_cleanup_delay_seconds";

    private GuardianPreferences() {
    }

    static int loadDelaySeconds(Context context) {
        SharedPreferences preferences = context.getSharedPreferences(
                FILE_NAME, Context.MODE_PRIVATE);
        int value = preferences.getInt(
                KEY_DELAY_SECONDS, DEFAULT_DELAY_SECONDS);
        return normalizeDelaySeconds(value);
    }

    static int normalizeDelaySeconds(int value) {
        for (int candidate : DELAY_VALUES) {
            if (candidate == value) {
                return value;
            }
        }
        return DEFAULT_DELAY_SECONDS;
    }

    static void saveDelaySeconds(Context context, int seconds) {
        if (normalizeDelaySeconds(seconds) != seconds) {
            throw new IllegalArgumentException(
                    "Unsupported guardian cleanup delay.");
        }
        context.getSharedPreferences(FILE_NAME, Context.MODE_PRIVATE)
                .edit()
                .putInt(KEY_DELAY_SECONDS, seconds)
                .apply();
        // Native DeX has a fixed grace; this preference controls legacy cleanup only.
    }
}

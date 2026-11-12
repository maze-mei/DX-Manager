package io.github.mazemei.dxmnativedex;

import java.util.Properties;

/** Basic-mode fallback only; a verified Companion keeps its original power recovery policy. */
public final class NativePowerRecovery {
    public interface Settings {
        String read() throws Exception;
        void restore(String original) throws Exception;
    }
    public static String original(String value) {
        if ("missing".equals(value)) return value;
        if (value == null || !value.matches("[0-9]{1,10}")) throw new IllegalArgumentException("Invalid original power setting.");
        int number = Integer.parseInt(value);
        if (number < 0) throw new IllegalArgumentException("Invalid original power setting.");
        return Integer.toString(number);
    }
    public static void configure(Properties record, boolean companion, boolean restore, String value) {
        String normalized = original(value);
        record.setProperty("power.companion", Boolean.toString(companion));
        record.setProperty("power.restore", Boolean.toString(restore));
        record.setProperty("power.original", normalized);
    }
    public static boolean usesCompanion(Properties record) {
        return record != null && "true".equals(record.getProperty("power.companion"));
    }
    public static void restore(Properties record, String token, Settings settings) throws Exception {
        NativeIdentity.validate(token);
        if (record == null || !token.equals(record.getProperty("token")) || usesCompanion(record) ||
            !"false".equals(record.getProperty("power.companion")) || !"true".equals(record.getProperty("power.restore"))) return;
        String value = original(record.getProperty("power.original"));
        // Preserve a user/app change made after DX Manager's own override.
        if ("7".equals(settings.read()) && !"7".equals(value)) settings.restore(value);
    }
}

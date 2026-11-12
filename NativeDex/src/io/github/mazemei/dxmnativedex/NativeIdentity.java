package io.github.mazemei.dxmnativedex;

/** The private helper accepts only typed session identifiers, never shell fragments. */
public final class NativeIdentity {
    public static String validate(String token) {
        if (token == null || !token.matches("[0-9a-f]{32}")) throw new IllegalArgumentException("Invalid session token.");
        return token;
    }
    public static String name(String token) { return "DXM-Native-" + validate(token).substring(0, 12); }
    public static String address(String token) {
        validate(token);
        StringBuilder result = new StringBuilder("02");
        for (int i = 0; i < 10; i += 2) result.append(':').append(token.substring(i, i + 2));
        return result.toString();
    }
    public static boolean matches(String token, String address) {
        return address(token).equalsIgnoreCase(address == null ? "" : address);
    }
}

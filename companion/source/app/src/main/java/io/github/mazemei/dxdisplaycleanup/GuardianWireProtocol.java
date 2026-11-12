package io.github.mazemei.dxdisplaycleanup;

import java.io.DataInput;
import java.io.IOException;

/** Session-scoped framing shared by old official and new foreground-aware clients. */
final class GuardianWireProtocol {
    private GuardianWireProtocol() { }
    static boolean supported(int version) { return version == 1 || version == 3; }
    static Long readPingProbe(DataInput input, int version) throws IOException {
        if (version == 1) return null; // The next byte belongs to the next command.
        if (version == 3) return input.readLong();
        throw new IOException("Unsupported Guardian protocol");
    }
}

package io.github.mazemei.dxdisplaycleanup;

import org.junit.Test;
import static org.junit.Assert.*;
import java.io.*;

public final class GuardianWireProtocolTest {
    @Test public void oldPingDoesNotConsumeShutdown() throws Exception {
        DataInputStream in = new DataInputStream(new ByteArrayInputStream(new byte[]{2, 1, 1}));
        assertNull(GuardianWireProtocol.readPingProbe(in, 1));
        assertEquals(2, in.readUnsignedByte());
    }
    @Test public void newPingConsumesOnlyItsProbe() throws Exception {
        ByteArrayOutputStream bytes = new ByteArrayOutputStream();
        DataOutputStream out = new DataOutputStream(bytes);
        out.writeLong(123456789L); out.writeByte(3);
        DataInputStream in = new DataInputStream(new ByteArrayInputStream(bytes.toByteArray()));
        assertEquals(Long.valueOf(123456789L), GuardianWireProtocol.readPingProbe(in, 3));
        assertEquals(3, in.readUnsignedByte());
    }
    @Test public void protocolIsPartOfThePersistedSessionIdentity() {
        GuardianSessionStore.Session old = new GuardianSessionStore.Session(true, 37124, "t", "usb", 1L);
        GuardianSessionStore.Session fresh = new GuardianSessionStore.Session(true, 37124, "t", "usb", 1L, 3);
        assertEquals(1, old.protocolVersion);
        assertTrue(old.isReady()); assertTrue(fresh.isReady());
        assertFalse(old.matches(fresh));
        assertFalse(new GuardianSessionStore.Session(true, 37124, "t", "usb", 1L, 2).isReady());
    }
}

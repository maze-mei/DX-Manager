package io.github.mazemei.dxmnativedex;

import java.io.BufferedReader;
import java.io.EOFException;
import java.io.IOException;
import java.util.Locale;

/** Bounded framing for the Apache-2.0 ScrcpyDeX RTSP negotiator. */
public final class RtspMessageReader {
    public static final int MAX_LINE = 4096;
    public static final int MAX_HEADERS = 32768;
    public static final int MAX_BODY = 65536;
    public static final class Message {
        public String firstLine, cseq = "1", session, body = "";
    }
    private static String line(BufferedReader in) throws IOException {
        StringBuilder s = new StringBuilder();
        int c;
        while ((c = in.read()) != -1) {
            if (c == '\n') return s.toString();
            if (c != '\r') s.append((char)c);
            if (s.length() > MAX_LINE) throw new IOException("RTSP line limit exceeded.");
        }
        if (s.length() == 0) return null;
        throw new EOFException("Incomplete RTSP line.");
    }
    public static Message read(BufferedReader in) throws IOException {
        Message m = new Message();
        do { m.firstLine = line(in); } while ("".equals(m.firstLine));
        if (m.firstLine == null) return null;
        int total = m.firstLine.length(), length = 0;
        boolean lengthSeen = false;
        for (;;) {
            String header = line(in);
            if (header == null) throw new EOFException("Incomplete RTSP headers.");
            total += header.length() + 2;
            if (total > MAX_HEADERS) throw new IOException("RTSP header limit exceeded.");
            if (header.isEmpty()) break;
            int colon = header.indexOf(':');
            if (colon < 1) throw new IOException("Invalid RTSP header.");
            String key = header.substring(0, colon).trim().toLowerCase(Locale.ROOT);
            String value = header.substring(colon + 1).trim();
            if (key.equals("cseq")) {
                if (!value.matches("[0-9]{1,9}")) throw new IOException("Invalid RTSP CSeq.");
                m.cseq = value;
            } else if (key.equals("session")) {
                m.session = value.split(";", 2)[0].trim();
            } else if (key.equals("content-length")) {
                if (lengthSeen || !value.matches("[0-9]{1,8}")) throw new IOException("Invalid RTSP Content-Length.");
                lengthSeen = true;
                try { length = Integer.parseInt(value); } catch (NumberFormatException e) { throw new IOException(e); }
                if (length > MAX_BODY) throw new IOException("RTSP body limit exceeded.");
            }
        }
        char[] body = new char[length];
        int offset = 0;
        while (offset < length) {
            int count = in.read(body, offset, length - offset);
            if (count < 0) throw new EOFException("Incomplete RTSP body.");
            offset += count;
        }
        m.body = new String(body);
        return m;
    }
}

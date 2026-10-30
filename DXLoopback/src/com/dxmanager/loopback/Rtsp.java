/*
 * SPDX-License-Identifier: Apache-2.0
 * Wireless DeX negotiation adapted from ScrcpyDeX's DexActivator.java.
 * Copyright 2026 Aureliano Peixoto and ScrcpyDeX Contributors.
 * Modified for DX Manager: bounded byte framing, response correlation, loopback-only
 * URL validation, and no video/audio/input capture. See DXLoopback/NOTICE.
 */
package com.dxmanager.loopback;

import java.io.ByteArrayOutputStream;
import java.io.EOFException;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.util.LinkedHashMap;
import java.util.Locale;
import java.util.Map;

final class Rtsp {
    private Rtsp() { }
    static final class Message {
        final String first;
        final Map<String, String> headers;
        final String body;
        Message(String first, Map<String, String> headers, String body) {
            this.first = first;
            this.headers = headers;
            this.body = body;
        }
        String header(String name) { return headers.get(name.toLowerCase(Locale.ROOT)); }
    }
    static String line(InputStream in, int max) throws IOException {
        ByteArrayOutputStream bytes = new ByteArrayOutputStream();
        for (;;) {
            int b = in.read();
            if (b == -1) {
                if (bytes.size() == 0) return null;
                throw new EOFException("truncated line");
            }
            if (b == '\n') {
                byte[] data = bytes.toByteArray();
                int length = data.length;
                if (length > 0 && data[length - 1] == '\r') length--;
                return new String(data, 0, length, StandardCharsets.US_ASCII);
            }
            if (b == 0 || b > 127 || bytes.size() >= max) throw new IOException("invalid line");
            bytes.write(b);
        }
    }
    static Message read(InputStream in) throws IOException {
        String first = line(in, 4096);
        if (first == null) return null;
        if (first.length() == 0) throw new IOException("empty start line");
        Map<String, String> headers = new LinkedHashMap<String, String>();
        int total = 0;
        for (;;) {
            String header = line(in, 4096);
            if (header == null) throw new EOFException("truncated headers");
            total += header.length();
            if (total > 16384) throw new IOException("large headers");
            if (header.length() == 0) break;
            int colon = header.indexOf(':');
            if (colon < 1) throw new IOException("invalid header");
            String key = header.substring(0, colon).trim().toLowerCase(Locale.ROOT);
            if (headers.put(key, header.substring(colon + 1).trim()) != null) throw new IOException("duplicate header");
        }
        int length = 0;
        String lengthText = headers.get("content-length");
        if (lengthText != null) {
            if (!lengthText.matches("[0-9]{1,5}")) throw new IOException("invalid body length");
            length = Integer.parseInt(lengthText);
            if (length > 65536) throw new IOException("large body");
        }
        byte[] body = new byte[length];
        int offset = 0;
        while (offset < length) {
            int count = in.read(body, offset, length - offset);
            if (count < 0) throw new EOFException("truncated body");
            offset += count;
        }
        return new Message(first, headers, new String(body, StandardCharsets.US_ASCII));
    }
    static final class Handshake {
        private final int videoPort;
        private final int audioPort;
        private int sequence = 1;
        private int optionsSequence = -1;
        private int setupSequence = -1;
        private int playSequence = -1;
        private String url = "rtsp://127.0.0.1/wfd1.0/streamid=0";
        private String session;
        private boolean ready;
        Handshake(int videoPort, int audioPort) { this.videoPort = videoPort; this.audioPort = audioPort; }
        boolean isReady() { return ready; }
        void accept(Message message, OutputStream out) throws IOException {
            String cseq = message.header("cseq");
            if (cseq == null || !cseq.matches("[0-9]{1,9}")) throw new IOException("missing cseq");
            if (message.first.startsWith("RTSP/1.0 ")) {
                int responseSequence = Integer.parseInt(cseq);
                if (!message.first.startsWith("RTSP/1.0 200 ")) throw new IOException("rtsp refused");
                if (responseSequence == setupSequence && session == null) {
                    String rawSession = message.header("session");
                    if (rawSession == null) throw new IOException("missing session");
                    session = rawSession.split(";", 2)[0].trim();
                    if (!session.matches("[A-Za-z0-9_.-]{1,128}")) throw new IOException("invalid session");
                    playSequence = sequence++;
                    send(out, "PLAY " + url + " RTSP/1.0\r\nCSeq: " + playSequence
                            + "\r\nSession: " + session + "\r\n\r\n");
                } else if (responseSequence == playSequence && session != null) ready = true;
                else if (responseSequence != optionsSequence) throw new IOException("unexpected response");
                return;
            }
            if (message.first.startsWith("OPTIONS ")) {
                reply(out, cseq, "Public: org.wfa.wfd1.0, GET_PARAMETER, SET_PARAMETER\r\n", "");
                if (optionsSequence == -1) {
                    optionsSequence = sequence++;
                    send(out, "OPTIONS * RTSP/1.0\r\nCSeq: " + optionsSequence
                            + "\r\nRequire: org.wfa.wfd1.0\r\n\r\n");
                }
            } else if (message.first.startsWith("GET_PARAMETER ")) {
                String capabilities = "";
                if (message.body.contains("wfd_video_formats")) {
                    capabilities = "wfd_client_rtp_ports: RTP/AVP/UDP;unicast " + videoPort + " 0 mode=play\r\n"
                            + "wfd_audio_codecs: LPCM 00000003 00, AAC 0000000f 00\r\n"
                            + "wfd_video_formats: 40 00 01 10 0001bdeb 1fffffff 00003fff 10 0000 001f 11 0780 0438, 02 10 0001bdeb 1fffffff 00000fff 10 0000 001f 11 0780 0438\r\n"
                            + "wfd_content_protection: none\r\n"
                            + "wfd_uibc_capability: none\r\n";
                }
                reply(out, cseq, "", capabilities);
            } else if (message.first.startsWith("SET_PARAMETER ")) {
                boolean setup = false;
                for (String entry : message.body.split("\r?\n")) {
                    if (entry.startsWith("wfd_presentation_URL:")) {
                        String candidate = entry.substring(entry.indexOf(':') + 1).trim().split("\\s+")[0];
                        try {
                            URI parsed = new URI(candidate);
                            if (!"rtsp".equals(parsed.getScheme()) || !"127.0.0.1".equals(parsed.getHost())
                                    || parsed.getUserInfo() != null || candidate.indexOf('\r') >= 0
                                    || candidate.indexOf('\n') >= 0) throw new IOException("non-loopback URL");
                            url = candidate;
                        } catch (java.net.URISyntaxException e) { throw new IOException("invalid URL", e); }
                    }
                    if ("wfd_trigger_method: SETUP".equals(entry.trim())) setup = true;
                    if ("wfd_trigger_method: TEARDOWN".equals(entry.trim())) throw new IOException("remote teardown");
                }
                reply(out, cseq, "", "");
                if (setup && setupSequence == -1) {
                    setupSequence = sequence++;
                    send(out, "SETUP " + url + " RTSP/1.0\r\nCSeq: " + setupSequence
                            + "\r\nTransport: RTP/AVP/UDP;unicast;client_port=" + videoPort
                            + "-" + audioPort + "\r\n\r\n");
                }
            } else throw new IOException("unsupported RTSP method");
        }
        private static void reply(OutputStream out, String cseq, String extra, String body) throws IOException {
            String headers = "RTSP/1.0 200 OK\r\nCSeq: " + cseq + "\r\n" + extra;
            if (body.length() > 0) headers += "Content-Type: text/parameters\r\nContent-Length: "
                    + body.getBytes(StandardCharsets.US_ASCII).length + "\r\n";
            send(out, headers + "\r\n" + body);
        }
        private static void send(OutputStream out, String text) throws IOException {
            out.write(text.getBytes(StandardCharsets.US_ASCII));
            out.flush();
        }
    }
}

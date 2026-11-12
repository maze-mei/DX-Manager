package io.github.mazemei.dxdisplaycleanup;

import android.os.SystemClock;
import android.util.Log;
import java.io.DataOutputStream;
import java.io.IOException;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.ArrayBlockingQueue;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;

/** Independent writer: the Guardian reader/heartbeat never delays app-change delivery. */
final class ForegroundAppChannel implements ForegroundAppRegistry.Listener, AutoCloseable {
    private static final String TAG = "DXForeground";
    static final int MESSAGE_APP = 4, MESSAGE_AVAILABILITY = 5, MESSAGE_PONG = 6, MESSAGE_WINDOW = 7;
    private final Socket socket;
    private final DataOutputStream output;
    private final ThreadPoolExecutor writer = new ThreadPoolExecutor(
            1, 1, 0L, TimeUnit.MILLISECONDS, new ArrayBlockingQueue<>(64));
    private volatile boolean closed;

    ForegroundAppChannel(Socket socket, DataOutputStream output) {
        this.socket = socket;
        this.output = output;
        ForegroundAppAccessibilityService.REGISTRY.attach(this);
    }

    @FunctionalInterface private interface Frame { void write() throws IOException; }

    private void enqueue(Frame frame) {
        if (closed) return;
        try {
            writer.execute(() -> {
                if (closed) return;
                try {
                    frame.write();
                    output.flush();
                } catch (IOException | RuntimeException exception) {
                    Log.d(TAG, "Foreground channel disconnected.", exception);
                    closeSocket();
                }
            });
        } catch (RuntimeException exception) {
            // A bounded backlog fails closed instead of delivering stale app rules.
            closeSocket();
        }
    }

    @Override public void availability(long sequence, boolean available) {
        enqueue(() -> {
            output.writeByte(MESSAGE_AVAILABILITY);
            output.writeLong(sequence);
            output.writeBoolean(available);
            Log.i(TAG, "Accessibility available=" + available);
        });
    }

    @Override public void app(long sequence, int displayId, String packageName,
            long eventTime) {
        enqueue(() -> {
            byte[] bytes = packageName.getBytes(StandardCharsets.UTF_8);
            long age = eventTime > 0 ? Math.max(0, SystemClock.uptimeMillis() - eventTime) : 0;
            output.writeByte(MESSAGE_APP);
            output.writeLong(sequence);
            output.writeInt(displayId);
            output.writeInt((int) Math.min(age, Integer.MAX_VALUE));
            output.writeInt(bytes.length);
            output.write(bytes);
            Log.i(TAG, "display=" + displayId + " package="
                    + (packageName.isEmpty() ? "<unknown>" : packageName)
                    + " eventAgeMs=" + age);
        });
    }

    @Override public void window(ForegroundWindowDetails details) {
        enqueue(() -> {
            output.writeByte(MESSAGE_WINDOW);
            output.writeInt(details.displayId); output.writeInt(details.windowId);
            output.writeInt(details.windowType); output.writeInt(details.layer);
            output.writeBoolean(details.focused); output.writeBoolean(details.active);
            output.writeInt(details.event.type); output.writeInt(details.event.displayId);
            output.writeInt(details.event.windowId);
            writeDetail(details.packageName); writeDetail(details.event.packageName);
            writeDetail(details.stateClass); writeDetail(details.event.className);
            writeDetail(details.rootClass); writeDetail(details.title);
            Log.i(TAG, "web window display=" + details.displayId + " window=" + details.windowId
                    + " package=" + details.packageName + " stateClass=" + details.stateClass
                    + " title=" + details.title);
        });
    }

    private void writeDetail(String value) throws IOException {
        byte[] bytes = value.getBytes(StandardCharsets.UTF_8);
        output.writeInt(bytes.length); output.write(bytes);
    }

    void pong(long probe) {
        enqueue(() -> {
            output.writeByte(MESSAGE_PONG);
            output.writeLong(probe);
        });
    }

    private void closeSocket() {
        closed = true;
        try { socket.close(); } catch (IOException ignored) { }
    }

    @Override public void close() {
        closed = true;
        ForegroundAppAccessibilityService.REGISTRY.detach(this);
        writer.shutdownNow();
    }
}

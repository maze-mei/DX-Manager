package io.github.mazemei.dxdisplaycleanup;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Intent;
import android.content.IntentFilter;
import android.net.ConnectivityManager;
import android.net.Network;
import android.net.NetworkCapabilities;
import android.os.Build;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;
import android.os.SystemClock;
import android.util.Log;

import java.io.BufferedInputStream;
import java.io.BufferedOutputStream;
import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.EOFException;
import java.io.IOException;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicInteger;

public final class SessionGuardianService extends Service {
    static final String ACTION_START =
            "io.github.mazemei.dxdisplaycleanup.START_SESSION_GUARDIAN";

    private static final String TAG = "DXSessionGuardian";
    private static final String CHANNEL_ID = "dx_manager_session_guardian";
    private static final int NOTIFICATION_ID = 1401;
    private static final int MAGIC = 0x44584744;
    private static final int VERSION = 3;
    private static final int COMMAND_PING = 1;
    private static final int COMMAND_WINDOWS_SHUTDOWN = 2;
    private static final int COMMAND_STOP_MONITORING = 3;
    private static final long RECONNECT_DELAY_MS = 2000L;
    private static final long FAILED_CLEANUP_RETRY_DELAY_MS = 5000L;
    private static final int MAX_WINDOWS_SHUTDOWN_CLEANUP_RETRIES = 3;
    private static final int MAX_STRING_BYTES = 64 * 1024;
    private static final String ACTION_USB_STATE =
            "android.hardware.usb.action.USB_STATE";
    private static final String EXTRA_USB_CONNECTED = "connected";
    private static volatile SessionGuardianService activeInstance;
    static boolean isPcConnected(){SessionGuardianService current=activeInstance;return current!=null&&current.connected&&!current.stopRequested;}

    private final ExecutorService executor =
            Executors.newSingleThreadExecutor();
    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private final AtomicInteger generation = new AtomicInteger();
    private volatile Socket currentSocket;
    private volatile boolean connected;
    private volatile boolean stopRequested;
    private volatile Runnable pendingCleanup;
    private volatile Runnable pendingWindowsShutdownCleanup;
    private Runnable pendingIdleStop;
    private boolean hadConnection;
    private long connectionStartedAtMs;

    @Override
    public void onCreate() {
        super.onCreate();
        activeInstance = this;
        NativeSessionBridge.observeDisplayState(this);
        createNotificationChannel();
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent != null && ACTION_START.equals(intent.getAction())) {
            int port = intent.getIntExtra(
                    "port", GuardianSessionStore.DEFAULT_PORT);
            String token = intent.getStringExtra("token");
            String transport = intent.getStringExtra("transport");
            int protocolVersion = intent.getIntExtra("protocol_version", 1);
            if (!GuardianSessionStore.isSupportedProtocol(protocolVersion)) {
                stopSelf(startId); return START_NOT_STICKY;
            }
            if (port > 0 && port <= 65535 && token != null
                    && !token.isEmpty()) {
                GuardianSessionStore.save(
                        this, port, token, transport, protocolVersion);
            }
        }

        GuardianSessionStore.Session session =
                GuardianSessionStore.load(this);
        if (!session.isReady()) {
            stopSelf(startId);
            return START_NOT_STICKY;
        }

        stopRequested = false;
        startForeground(NOTIFICATION_ID, buildNotification(false));
        restartConnectionLoop(session);
        return START_STICKY;
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public void onDestroy() {
        stopRequested = true;
        generation.incrementAndGet();
        cancelPendingCleanup();
        closeCurrentSocket();
        executor.shutdownNow();
        stopForeground(true);
        if (activeInstance == this) {
            activeInstance = null;
        }
        super.onDestroy();
    }

    static void stopAfterManualCleanupIfIdle(
            android.content.Context context,
            CleanupCoordinator.Snapshot snapshot) {
        if (context == null || snapshot == null || snapshot.anyActive()) {
            return;
        }
        SessionGuardianService instance = activeInstance;
        if (instance != null && instance.connected) {
            return;
        }
        GuardianSessionStore.clear(context);
        context.stopService(new Intent(
                context, SessionGuardianService.class));
    }

    private void restartConnectionLoop(
            GuardianSessionStore.Session session) {
        int currentGeneration = generation.incrementAndGet();
        hadConnection = false;
        connectionStartedAtMs = SystemClock.elapsedRealtime();
        cancelPendingCleanup();
        cancelPendingCleanup();
        closeCurrentSocket();
        executor.execute(() -> connectionLoop(
                session, currentGeneration));
    }

    private void connectionLoop(
            GuardianSessionStore.Session session,
            int currentGeneration) {
        while (!stopRequested
                && generation.get() == currentGeneration
                && session.matches(GuardianSessionStore.load(this))) {
            try (Socket socket = new Socket()) {
                currentSocket = socket;
                socket.connect(new InetSocketAddress(
                        InetAddress.getLoopbackAddress(), session.port), 2000);
                socket.setTcpNoDelay(true);
                socket.setSoTimeout(10000);
                try (DataOutputStream output = new DataOutputStream(
                        new BufferedOutputStream(socket.getOutputStream()));
                     DataInputStream input = new DataInputStream(
                             new BufferedInputStream(socket.getInputStream()))) {
                    output.writeInt(MAGIC);
                    output.writeInt(session.protocolVersion);
                    writeString(output, session.token);
                    output.flush();
                    if (input.read() != 1) {
                        throw new IOException(
                                "DX Manager rejected the guardian session.");
                    }

                    try (ForegroundAppChannel foregroundChannel =
                            session.protocolVersion == 3 ? new ForegroundAppChannel(socket, output) : null) {
                    markConnected(true, session, currentGeneration);
                    while (!stopRequested
                            && generation.get() == currentGeneration) {
                        int command = input.read();
                        if (command < 0) throw new EOFException();
                        if (command == COMMAND_PING) {
                            Long probe = GuardianWireProtocol.readPingProbe(input, session.protocolVersion);
                            if (probe != null && foregroundChannel != null) foregroundChannel.pong(probe);
                            continue;
                        }
                        if (command == COMMAND_WINDOWS_SHUTDOWN) {
                            boolean removeOverlay = input.read() == 1;
                            boolean restoreStayAwake = input.read() == 1;
                            String originalStayAwake = readString(input);
                            postForSession(session, currentGeneration, () -> handleWindowsShutdown(
                                    removeOverlay, restoreStayAwake, originalStayAwake));
                            return;
                        }
                        if (command == COMMAND_STOP_MONITORING) {
                            postForSession(session, currentGeneration, this::stopMonitoring);
                            return;
                        }
                        throw new IOException(
                                "Unknown guardian command: " + command);
                    }
                    }
                }
            } catch (IOException | RuntimeException exception) {
                if (!stopRequested
                        && generation.get() == currentGeneration) {
                    Log.d(TAG, "DX Manager guardian connection lost.",
                            exception);
                }
            } finally {
                currentSocket = null;
                if (!stopRequested
                        && generation.get() == currentGeneration) {
                    markConnected(false, session, currentGeneration);
                }
            }

            if (stopRequested || generation.get() != currentGeneration) {
                return;
            }
            try {
                Thread.sleep(RECONNECT_DELAY_MS);
            } catch (InterruptedException interrupted) {
                Thread.currentThread().interrupt();
                return;
            }
        }
    }

    private void postForSession(GuardianSessionStore.Session session,
                                int currentGeneration, Runnable action) {
        mainHandler.post(() -> {
            if (stopRequested || generation.get() != currentGeneration
                    || !session.matches(GuardianSessionStore.load(this))) return;
            action.run();
        });
    }

    private void markConnected(boolean value, GuardianSessionStore.Session session,
                               int currentGeneration) {
        postForSession(session, currentGeneration, () -> {
            boolean changed = connected != value;
            connected = value;
            if (changed) NativeSessionBridge.onGuardianConnectionChanged(this);
            if (value) {
                hadConnection = true;
                cancelPendingCleanup();
            } else if (NativeSessionBridge.hasSession(this)) {
                // Native owner applies its independent fixed thirty-second grace.
            } else if (stopIdleDisconnectedSession(session, currentGeneration)) {
                return;
            } else if (pendingCleanup == null && !isConfiguredTransportConnected(session)) {
                scheduleDisconnectCleanup(session);
            }
            getSystemService(NotificationManager.class).notify(
                    NOTIFICATION_ID, buildNotification(value));
        });
    }

    private boolean stopIdleDisconnectedSession(
            GuardianSessionStore.Session session, int currentGeneration) {
        if (NativeSessionBridge.hasSession(this)) return false;
        CleanupCoordinator.Snapshot snapshot = CleanupCoordinator.inspect(this);
        if (!CleanupCoordinator.isCleanupComplete(snapshot, true, true)) return false;
        long elapsed = SystemClock.elapsedRealtime() - connectionStartedAtMs;
        if (GuardianIdleStopPolicy.shouldStop(connected, hadConnection, elapsed, snapshot)) {
            completeAndStop(); return true;
        }
        if (!connected && pendingIdleStop == null) {
            pendingIdleStop = () -> {
                pendingIdleStop = null;
                postForSession(session, currentGeneration, () -> {
                    if (!connected) stopIdleDisconnectedSession(session, currentGeneration);
                });
            };
            mainHandler.postDelayed(pendingIdleStop,
                    GuardianIdleStopPolicy.startupDelayRemaining(hadConnection, elapsed));
        }
        return true;
    }

    private void scheduleDisconnectCleanup(
            GuardianSessionStore.Session scheduledSession) {
        cancelPendingCleanup();
        int delaySeconds = GuardianPreferences.loadDelaySeconds(this);
        if (delaySeconds == GuardianPreferences.DISABLED) {
            return;
        }
        if (!scheduledSession.isReady()) {
            return;
        }
        pendingCleanup = () -> {
            pendingCleanup = null;
            if (connected || stopRequested
                    || isConfiguredTransportConnected(scheduledSession)
                    || !scheduledSession.matches(
                    GuardianSessionStore.load(this))) {
                return;
            }
            if(NativeSessionBridge.hasSession(this)){NativeSessionBridge.updatePolicyAsync(this);return;}
            CleanupCoordinator.Snapshot snapshot =
                    CleanupCoordinator.cleanup(this, true, true);
            CleanupWidgetProvider.updateAll(this);
            CleanupTileService.requestRefresh(this);
            if (CleanupCoordinator.isCleanupComplete(
                    snapshot, true, true)) {
                completeAndStop();
            } else {
                Log.w(TAG, "Guardian disconnect cleanup failed; retrying.");
                scheduleDisconnectCleanupRetry(scheduledSession);
            }
        };
        mainHandler.postDelayed(
                pendingCleanup, delaySeconds * 1000L);
    }

    private boolean isConfiguredTransportConnected(
            GuardianSessionStore.Session session) {
        if (session == null) {
            return true;
        }
        if (GuardianSessionStore.TRANSPORT_USB.equals(session.transport)) {
            Intent state = registerReceiver(
                    null, new IntentFilter(ACTION_USB_STATE));
            return state != null
                    && state.getBooleanExtra(EXTRA_USB_CONNECTED, false);
        }
        if (GuardianSessionStore.TRANSPORT_WIRELESS.equals(
                session.transport)) {
            ConnectivityManager manager =
                    (ConnectivityManager) getSystemService(
                            CONNECTIVITY_SERVICE);
            if (manager == null) {
                return true;
            }
            for (Network network : manager.getAllNetworks()) {
                NetworkCapabilities capabilities =
                        manager.getNetworkCapabilities(network);
                if (capabilities != null && capabilities.hasTransport(
                        NetworkCapabilities.TRANSPORT_WIFI)) {
                    return true;
                }
            }
            return false;
        }
        // Unknown transport is kept conservatively. A manual cleanup or an
        // authenticated Windows-shutdown command can still remove it.
        return true;
    }

    private void handleWindowsShutdown(
            boolean removeOverlay,
            boolean restoreStayAwake,
            String originalStayAwake) {
        cancelPendingCleanup();
        attemptWindowsShutdownCleanup(
                removeOverlay,
                restoreStayAwake,
                originalStayAwake,
                0);
    }

    private void attemptWindowsShutdownCleanup(
            boolean removeOverlay,
            boolean restoreStayAwake,
            String originalStayAwake,
            int attempt) {
        if (stopRequested) {
            return;
        }
        boolean nativeClean=NativeSessionBridge.stop(this);
        if (removeOverlay) {
            OverlayDisplayRepository.cleanup(this);
        }
        if (restoreStayAwake) {
            StayAwakeRepository.restore(this, originalStayAwake);
        }
        CleanupCoordinator.Snapshot snapshot = CleanupCoordinator.inspect(this);
        CleanupWidgetProvider.updateAll(this);
        CleanupTileService.requestRefresh(this);
        if (nativeClean && CleanupCoordinator.isWindowsShutdownCleanupComplete(
                snapshot,
                removeOverlay,
                restoreStayAwake,
                originalStayAwake)) {
            completeAndStop();
            return;
        }

        if (attempt < MAX_WINDOWS_SHUTDOWN_CLEANUP_RETRIES) {
            scheduleWindowsShutdownCleanupRetry(
                    removeOverlay,
                    restoreStayAwake,
                    originalStayAwake,
                    attempt + 1);
            return;
        }

        Log.w(TAG, "Guardian Windows shutdown cleanup could not be verified; retaining the session.");
        GuardianSessionStore.Session session = GuardianSessionStore.load(this);
        if (session.isReady()) {
            restartConnectionLoop(session);
        }
    }

    private void stopMonitoring() {
        stopRequested = true;
        generation.incrementAndGet();
        cancelPendingCleanup();
        GuardianSessionStore.clear(this);
        stopForeground(true);
        stopSelf();
    }

    private void cancelPendingCleanup() {
        Runnable idle = pendingIdleStop;
        pendingIdleStop = null;
        if (idle != null) mainHandler.removeCallbacks(idle);
        Runnable cleanup = pendingCleanup;
        pendingCleanup = null;
        if (cleanup != null) {
            mainHandler.removeCallbacks(cleanup);
        }
        Runnable windowsCleanup = pendingWindowsShutdownCleanup;
        pendingWindowsShutdownCleanup = null;
        if (windowsCleanup != null) {
            mainHandler.removeCallbacks(windowsCleanup);
        }
    }

    private void scheduleDisconnectCleanupRetry(
            GuardianSessionStore.Session session) {
        pendingCleanup = () -> {
            pendingCleanup = null;
            if (connected || stopRequested
                    || isConfiguredTransportConnected(session)
                    || !session.matches(GuardianSessionStore.load(this))) {
                return;
            }
            if(NativeSessionBridge.hasSession(this)){NativeSessionBridge.updatePolicyAsync(this);return;}
            CleanupCoordinator.Snapshot snapshot =
                    CleanupCoordinator.cleanup(this, true, true);
            CleanupWidgetProvider.updateAll(this);
            CleanupTileService.requestRefresh(this);
            if (CleanupCoordinator.isCleanupComplete(
                    snapshot, true, true)) {
                completeAndStop();
            } else {
                Log.w(TAG, "Guardian disconnect cleanup retry failed; keeping the session.");
            }
        };
        mainHandler.postDelayed(
                pendingCleanup, FAILED_CLEANUP_RETRY_DELAY_MS);
    }

    private void scheduleWindowsShutdownCleanupRetry(
            boolean removeOverlay,
            boolean restoreStayAwake,
            String originalStayAwake,
            int attempt) {
        pendingWindowsShutdownCleanup = () -> {
            pendingWindowsShutdownCleanup = null;
            attemptWindowsShutdownCleanup(
                    removeOverlay,
                    restoreStayAwake,
                    originalStayAwake,
                    attempt);
        };
        mainHandler.postDelayed(
                pendingWindowsShutdownCleanup,
                FAILED_CLEANUP_RETRY_DELAY_MS);
    }

    private void completeAndStop() {
        stopRequested = true;
        generation.incrementAndGet();
        cancelPendingCleanup();
        GuardianSessionStore.clear(this);
        closeCurrentSocket();
        stopForeground(true);
        stopSelf();
    }

    private void closeCurrentSocket() {
        Socket socket = currentSocket;
        currentSocket = null;
        if (socket != null) {
            try {
                socket.close();
            } catch (IOException ignored) {
            }
        }
    }

    private void createNotificationChannel() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) {
            return;
        }
        NotificationChannel channel = new NotificationChannel(
                CHANNEL_ID,
                getString(R.string.guardian_channel_name),
                NotificationManager.IMPORTANCE_LOW);
        channel.setDescription(getString(
                R.string.guardian_channel_description));
        getSystemService(NotificationManager.class)
                .createNotificationChannel(channel);
    }

    private Notification buildNotification(boolean ready) {
        Intent launchIntent = new Intent(this, MainActivity.class);
        PendingIntent launch = PendingIntent.getActivity(
                this,
                1401,
                launchIntent,
                PendingIntent.FLAG_UPDATE_CURRENT |
                        PendingIntent.FLAG_IMMUTABLE);
        Notification.Builder builder = Build.VERSION.SDK_INT >=
                Build.VERSION_CODES.O
                ? new Notification.Builder(this, CHANNEL_ID)
                : new Notification.Builder(this);
        return builder
                .setSmallIcon(R.drawable.ic_tile_cleanup)
                .setContentTitle(getString(
                        R.string.guardian_notification_title))
                .setContentText(getString(ready
                        ? R.string.guardian_notification_connected
                        : R.string.guardian_notification_waiting))
                .setContentIntent(launch)
                .setOngoing(true)
                .setOnlyAlertOnce(true)
                .build();
    }

    private static void writeString(DataOutputStream output, String value)
            throws IOException {
        byte[] bytes = (value == null ? "" : value)
                .getBytes(StandardCharsets.UTF_8);
        output.writeInt(bytes.length);
        output.write(bytes);
    }

    private static String readString(DataInputStream input)
            throws IOException {
        int length = input.readInt();
        if (length < 0 || length > MAX_STRING_BYTES) {
            throw new IOException("Invalid guardian string length.");
        }
        byte[] bytes = new byte[length];
        input.readFully(bytes);
        return new String(bytes, StandardCharsets.UTF_8);
    }
}

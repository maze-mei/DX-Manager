package io.github.mazemei.dxdisplaycleanup;

import android.Manifest;
import android.app.Activity;
import android.app.Dialog;
import android.content.res.ColorStateList;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.database.ContentObserver;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.ColorDrawable;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.text.format.DateUtils;
import android.view.MotionEvent;
import android.view.View;
import android.view.WindowInsets;
import android.view.Window;
import android.view.WindowManager;
import android.widget.Button;
import android.widget.CheckBox;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.AdapterView;
import android.widget.PopupMenu;
import android.widget.Spinner;
import android.widget.TextView;
import android.widget.Toast;

import java.util.List;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public final class MainActivity extends Activity {
    private static final int REQUEST_FILES = 101;
    private static final int REQUEST_FOLDER = 102;
    private static final int REQUEST_NOTIFICATIONS = 103;
    private static final int TRANSFER_PROBE_TIMEOUT_MS = 1000;
    private static final long TRANSFER_PROBE_INTERVAL_MS = 2000L;

    private ImageView statusIcon;
    private TextView statusTitle;
    private TextView statusDetail;
    private View homeConnectionCard,homeNativeDexCard;
    private TextView homeNativeDexTitle,homeNativeDexDetail;
    private TextView homeDeviceName;
    private TextView homeTransport;
    private TextView homeCleanupSummary;
    private TextView homeActivityTitle;
    private TextView homeActivityElapsed;
    private Button homeCleanupButton;
    private Button homeTransferButton;
    private View cleanupOverlayCard,cleanupStayAwakeCard;
    private TextView overlayStatus;
    private TextView overlayValue;
    private TextView stayAwakeStatus;
    private TextView stayAwakeValue;
    private Button cleanupOverlayButton;
    private Button cleanupStayAwakeButton;
    private Button cleanupBothButton;
    private CheckBox tileWidgetOverlay;
    private CheckBox tileWidgetStayAwake;
    private Spinner guardianCleanupDelay;
    private TextView pcTransferStatus;
    private TextView transferTransportDetail;
    private View transferStatusCard;
    private View pcToPhoneHistoryEmpty;
    private View phoneToPcHistoryEmpty;
    private LinearLayout pcToPhoneHistoryList;
    private LinearLayout phoneToPcHistoryList;
    private Button clearPcToPhoneHistory;
    private Button clearPhoneToPcHistory;
    private View settingsPermissionCard;
    private ImageView settingsPermissionIcon;
    private TextView settingsPermissionTitle;
    private TextView settingsPermissionDetail;
    private Button sendFilesButton;
    private Button sendFolderButton;
    private View pageHome;
    private View pageQuickSettings;
    private View pageFileTransfer;
    private View pageSettings;
    private View navHome;
    private View navQuickSettings;
    private View navFileTransfer;
    private View navSettings;
    private ImageView navHomeIcon;
    private ImageView navQuickSettingsIcon;
    private ImageView navFileTransferIcon;
    private ImageView navSettingsIcon;
    private TextView navHomeLabel;
    private TextView navQuickSettingsLabel;
    private TextView navFileTransferLabel;
    private TextView navSettingsLabel;
    private View pageContainer;
    private ContentObserver settingObserver;
    private boolean updatingPreferences;
    private Page currentPage = Page.HOME;
    private float swipeStartX;
    private float swipeStartY;
    private boolean pageDragging;
    private Page dragTargetPage;
    private View dragTargetView;
    private final Handler transferStatusHandler =
            new Handler(Looper.getMainLooper());
    private final ExecutorService transferStatusExecutor =
            Executors.newSingleThreadExecutor();
    private boolean transferStatusMonitoring;
    private boolean transferProbeRunning;
    private int transferProbeGeneration;
    private boolean transferSessionReady;
    private long connectionStartedAtMillis;
    private long lastHistoryRevision = Long.MIN_VALUE;
    private final Runnable transferStatusPoll = new Runnable() {
        @Override
        public void run() {
            if (!transferStatusMonitoring) {
                return;
            }
            requestTransferSessionProbe();
            transferStatusHandler.postDelayed(
                    this, TRANSFER_PROBE_INTERVAL_MS);
        }
    };
    private final Runnable connectionElapsedTick = new Runnable() {
        @Override
        public void run() {
            if (!transferStatusMonitoring) {
                return;
            }
            updateConnectionElapsed();
            transferStatusHandler.postDelayed(this, 1000L);
        }
    };

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        View rootLayout = findViewById(R.id.root_layout);
        rootLayout.setOnApplyWindowInsetsListener((view, insets) -> {
            int statusBarInset;
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                statusBarInset = insets.getInsets(
                        WindowInsets.Type.statusBars()).top;
            } else {
                statusBarInset = insets.getSystemWindowInsetTop();
            }
            view.setPadding(
                    view.getPaddingLeft(),
                    statusBarInset,
                    view.getPaddingRight(),
                    view.getPaddingBottom());
            return insets;
        });
        rootLayout.requestApplyInsets();

        statusIcon = findViewById(R.id.status_icon);
        statusTitle = findViewById(R.id.status_title);
        statusDetail = findViewById(R.id.status_detail);
        cleanupOverlayCard = findViewById(R.id.cleanup_overlay_card);
        cleanupStayAwakeCard = findViewById(R.id.cleanup_stay_awake_card);
        homeConnectionCard = findViewById(R.id.home_connection_card);
        homeNativeDexCard = findViewById(R.id.home_native_dex_card);
        homeNativeDexTitle = findViewById(R.id.home_native_dex_title);
        homeNativeDexDetail = findViewById(R.id.home_native_dex_detail);
        homeDeviceName = findViewById(R.id.home_device_name);
        homeTransport = findViewById(R.id.home_transport);
        homeCleanupSummary = findViewById(R.id.home_cleanup_summary);
        homeActivityTitle = findViewById(R.id.home_activity_title);
        homeActivityElapsed = findViewById(R.id.home_activity_elapsed);
        overlayStatus = findViewById(R.id.overlay_status);
        overlayValue = findViewById(R.id.overlay_value);
        stayAwakeStatus = findViewById(R.id.stay_awake_status);
        stayAwakeValue = findViewById(R.id.stay_awake_value);
        cleanupOverlayButton = findViewById(R.id.cleanup_overlay_button);
        cleanupStayAwakeButton = findViewById(R.id.cleanup_stay_awake_button);
        cleanupBothButton = findViewById(R.id.cleanup_both_button);
        tileWidgetOverlay = findViewById(R.id.tile_widget_overlay);
        tileWidgetStayAwake = findViewById(R.id.tile_widget_stay_awake);
        guardianCleanupDelay = findViewById(R.id.guardian_cleanup_delay);
        pcTransferStatus = findViewById(R.id.pc_transfer_status);
        transferTransportDetail = findViewById(R.id.transfer_transport_detail);
        transferStatusCard = findViewById(R.id.transfer_status_card);
        pcToPhoneHistoryEmpty = findViewById(
                R.id.pc_to_phone_history_empty);
        phoneToPcHistoryEmpty = findViewById(
                R.id.phone_to_pc_history_empty);
        pcToPhoneHistoryList = findViewById(
                R.id.pc_to_phone_history_list);
        phoneToPcHistoryList = findViewById(
                R.id.phone_to_pc_history_list);
        clearPcToPhoneHistory = findViewById(
                R.id.clear_pc_to_phone_history);
        clearPhoneToPcHistory = findViewById(
                R.id.clear_phone_to_pc_history);
        settingsPermissionCard = findViewById(R.id.settings_permission_card);
        settingsPermissionIcon = findViewById(R.id.settings_permission_icon);
        settingsPermissionTitle = findViewById(R.id.settings_permission_title);
        settingsPermissionDetail = findViewById(R.id.settings_permission_detail);
        sendFilesButton = findViewById(R.id.send_files_button);
        sendFolderButton = findViewById(R.id.send_folder_button);
        pageHome = findViewById(R.id.page_home);
        pageQuickSettings = findViewById(R.id.page_quick_settings);
        pageFileTransfer = findViewById(R.id.page_file_transfer);
        pageSettings = findViewById(R.id.page_settings);
        navHome = findViewById(R.id.nav_home);
        navQuickSettings = findViewById(R.id.nav_quick_settings);
        navFileTransfer = findViewById(R.id.nav_file_transfer);
        navSettings = findViewById(R.id.nav_settings);
        navHomeIcon = findViewById(R.id.nav_home_icon);
        navQuickSettingsIcon = findViewById(R.id.nav_quick_settings_icon);
        navFileTransferIcon = findViewById(R.id.nav_file_transfer_icon);
        navSettingsIcon = findViewById(R.id.nav_settings_icon);
        navHomeLabel = findViewById(R.id.nav_home_label);
        navQuickSettingsLabel = findViewById(R.id.nav_quick_settings_label);
        navFileTransferLabel = findViewById(R.id.nav_file_transfer_label);
        navSettingsLabel = findViewById(R.id.nav_settings_label);
        pageContainer = findViewById(R.id.page_container);
        Button accessibilityButton = findViewById(R.id.foreground_accessibility_button);
        accessibilityButton.setOnClickListener(view ->
                startActivity(new Intent(Settings.ACTION_ACCESSIBILITY_SETTINGS)));
        Button refreshButton = findViewById(R.id.refresh_button);
        homeCleanupButton = findViewById(R.id.home_cleanup_button);
        homeTransferButton = findViewById(R.id.home_transfer_button);
        View homeMoreButton = findViewById(R.id.home_more_button);

        cleanupOverlayButton.setOnClickListener(view -> cleanup(true, false));
        cleanupStayAwakeButton.setOnClickListener(view -> cleanup(false, true));
        cleanupBothButton.setOnClickListener(view -> cleanup(true, true));
        refreshButton.setOnClickListener(view -> refresh());
        sendFilesButton.setOnClickListener(view -> chooseFiles());
        sendFolderButton.setOnClickListener(view -> chooseFolder());
        clearPcToPhoneHistory.setOnClickListener(view ->
                clearTransferHistory(
                        TransferHistoryStore.DIRECTION_PC_TO_PHONE));
        clearPhoneToPcHistory.setOnClickListener(view ->
                clearTransferHistory(
                        TransferHistoryStore.DIRECTION_PHONE_TO_PC));
        homeCleanupButton.setOnClickListener(view -> cleanup(true, true));
        homeTransferButton.setOnClickListener(view -> chooseFiles());
        homeMoreButton.setOnClickListener(view -> showHomeMenu(homeMoreButton));
        navHome.setOnClickListener(view -> selectPage(Page.HOME));
        navQuickSettings.setOnClickListener(view -> selectPage(Page.QUICK_SETTINGS));
        navFileTransfer.setOnClickListener(view -> selectPage(Page.FILE_TRANSFER));
        navSettings.setOnClickListener(view -> selectPage(Page.SETTINGS));
        selectPage(Page.HOME);

        CleanupPreferences.Targets targets = CleanupPreferences.load(this);
        setPreferenceChecks(targets);
        tileWidgetOverlay.setOnCheckedChangeListener((button, checked) ->
                savePreferenceChecks(button.getId()));
        tileWidgetStayAwake.setOnCheckedChangeListener((button, checked) ->
                savePreferenceChecks(button.getId()));
        configureGuardianCleanupDelay();

        settingObserver = new ContentObserver(new Handler(Looper.getMainLooper())) {
            @Override
            public void onChange(boolean selfChange) {
                refresh();
            }

            @Override
            public void onChange(boolean selfChange, Uri uri) {
                refresh();
            }
        };
        requestNotificationPermissionIfNeeded();
        refresh();
    }

    @Override
    protected void onStart() {
        super.onStart();
        NativeSessionBridge.addStatusListener(nativeStatusUpdated);
        transferStatusMonitoring = true;
        setTransferSessionReady(false);
        getContentResolver().registerContentObserver(
                Settings.Global.getUriFor(OverlayDisplayRepository.SETTING_NAME),
                false,
                settingObserver);
        getContentResolver().registerContentObserver(
                Settings.Global.getUriFor(StayAwakeRepository.SETTING_NAME),
                false,
                settingObserver);
        refresh();
        transferStatusHandler.removeCallbacks(transferStatusPoll);
        transferStatusHandler.post(transferStatusPoll);
        transferStatusHandler.removeCallbacks(connectionElapsedTick);
        transferStatusHandler.post(connectionElapsedTick);
    }

    private void configureGuardianCleanupDelay() {
        int saved = GuardianPreferences.loadDelaySeconds(this);
        int selected = 0;
        for (int index = 0;
             index < GuardianPreferences.DELAY_VALUES.length;
             index++) {
            if (GuardianPreferences.DELAY_VALUES[index] == saved) {
                selected = index;
                break;
            }
        }
        guardianCleanupDelay.setSelection(selected, false);
        guardianCleanupDelay.setOnItemSelectedListener(
                new AdapterView.OnItemSelectedListener() {
                    @Override
                    public void onItemSelected(AdapterView<?> parent,
                                               View view,
                                               int position,
                                               long id) {
                        if (position >= 0 && position <
                                GuardianPreferences.DELAY_VALUES.length) {
                            GuardianPreferences.saveDelaySeconds(
                                    MainActivity.this,
                                    GuardianPreferences.DELAY_VALUES[position]);
                        }
                    }

                    @Override
                    public void onNothingSelected(AdapterView<?> parent) {
                    }
                });
    }

    @Override
    protected void onStop() {
        NativeSessionBridge.removeStatusListener(nativeStatusUpdated);
        transferStatusMonitoring = false;
        transferProbeGeneration++;
        transferStatusHandler.removeCallbacks(transferStatusPoll);
        transferStatusHandler.removeCallbacks(connectionElapsedTick);
        getContentResolver().unregisterContentObserver(settingObserver);
        super.onStop();
    }

    @Override
    protected void onDestroy() {
        transferStatusExecutor.shutdownNow();
        super.onDestroy();
    }

    private boolean cleanupInProgress;
    private void cleanup(boolean overlay, boolean stayAwake) {
        if(cleanupInProgress)return;
        cleanupInProgress=true;cleanupBothButton.setEnabled(false);homeCleanupButton.setEnabled(false);
        new Thread(()->{
            CleanupCoordinator.Snapshot result=CleanupCoordinator.cleanup(this,overlay,stayAwake);
            runOnUiThread(()->{cleanupInProgress=false;if(!isFinishing()&&!isDestroyed())showCleanupResult(result,overlay,stayAwake);});
        },"DXM-Manual-Cleanup").start();
    }
    private void showCleanupResult(CleanupCoordinator.Snapshot snapshot, boolean overlay, boolean stayAwake) {
        /* Cleanup has already run off the UI thread. */
        /* snapshot is verified by the selected backend. */
        SessionGuardianService.stopAfterManualCleanupIfIdle(this, snapshot);
        render(snapshot);
        notifySurfaces();

        if (snapshot.permissionRequired()) {
            Toast.makeText(this, R.string.permission_required_short,
                    Toast.LENGTH_LONG).show();
        } else if (snapshot.hasError()
                || (overlay && snapshot.overlay.overlayActive)
                || (stayAwake && snapshot.stayAwake.stayAwakeActive)) {
            Toast.makeText(this, R.string.cleanup_failed, Toast.LENGTH_LONG).show();
        } else {
            int message = overlay && stayAwake
                    ? R.string.cleanup_both_success
                    : overlay
                    ? R.string.cleanup_overlay_success
                    : R.string.cleanup_stay_awake_success;
            Toast.makeText(this, message, Toast.LENGTH_SHORT).show();
        }
    }

    private final Runnable nativeStatusUpdated=()->{
        if(!isFinishing()&&!isDestroyed())render(CleanupCoordinator.inspect(this));
    };

    @Override
    protected void onResume() {
        super.onResume();
        refreshForegroundStatus();
    }

    private void refreshForegroundStatus() {
        TextView status = findViewById(R.id.foreground_accessibility_status);
        if (status != null) status.setText(
                ForegroundAppAccessibilityService.REGISTRY.isAvailable()
                ? R.string.foreground_accessibility_enabled
                : R.string.foreground_accessibility_disabled);
    }

    private void refresh() {
        refreshForegroundStatus();
        render(CleanupCoordinator.inspect(this));
        renderTransferSession();
        notifySurfaces();
    }

    private void renderTransferSession() {
        if (!TransferSessionStore.load(this).isReady()) {
            setTransferSessionReady(false);
        }
        requestTransferSessionProbe();
    }

    private void requestTransferSessionProbe() {
        if (!transferStatusMonitoring || transferProbeRunning) {
            return;
        }
        TransferSessionStore.Session session =
                TransferSessionStore.load(this);
        if (!session.isReady()) {
            setTransferSessionReady(false);
            return;
        }

        transferProbeRunning = true;
        int generation = ++transferProbeGeneration;
        transferStatusExecutor.execute(() -> {
            boolean ready = TransferSessionProbe.isReceiverReady(
                    session, TRANSFER_PROBE_TIMEOUT_MS);
            transferStatusHandler.post(() -> {
                transferProbeRunning = false;
                if (!transferStatusMonitoring
                        || generation != transferProbeGeneration) {
                    return;
                }
                TransferSessionStore.Session current =
                        TransferSessionStore.load(this);
                setTransferSessionReady(ready && session.matches(current));
            });
        });
    }

    private void setTransferSessionReady(boolean ready) {
        transferSessionReady = ready;
        pcTransferStatus.setText(ready
                ? R.string.transfer_pc_ready
                : R.string.transfer_pc_not_ready_short);
        transferStatusCard.setBackgroundResource(ready
                ? R.drawable.bg_status_card
                : R.drawable.bg_connection_pending);
        sendFilesButton.setEnabled(ready);
        sendFolderButton.setEnabled(ready);
        homeTransferButton.setEnabled(ready);
        homeConnectionCard.setBackgroundResource(ready
                ? R.drawable.bg_status_card
                : R.drawable.bg_connection_pending);
        statusIcon.setImageResource(ready
                ? R.drawable.dx_manager_icon
                : R.drawable.dx_manager_icon_mono);
        statusIcon.clearColorFilter();
        statusTitle.setText(ready
                ? R.string.home_connection_title_ready
                : R.string.home_connection_title_waiting);
        statusTitle.setTextColor(getColor(ready
                ? R.color.success : R.color.text_secondary));
        statusDetail.setText(ready
                ? R.string.home_connection_detail_ready
                : R.string.home_connection_detail_waiting);
        String deviceName = Settings.Global.getString(
                getContentResolver(), "device_name");
        if (deviceName == null || deviceName.trim().isEmpty()) {
            deviceName = Build.MODEL;
        }
        homeDeviceName.setText(deviceName);

        GuardianSessionStore.Session guardian = GuardianSessionStore.load(this);
        int transportLabel = GuardianSessionStore.TRANSPORT_WIRELESS.equals(
                guardian.transport)
                ? R.string.transport_wifi
                : GuardianSessionStore.TRANSPORT_USB.equals(guardian.transport)
                ? R.string.transport_usb
                : R.string.transport_unknown;
        String transport = getString(transportLabel);
        homeTransport.setText(ready
                ? getString(R.string.home_transport_connected, transport)
                : getString(R.string.home_transport_waiting));
        transferTransportDetail.setText(ready
                ? getString(R.string.transfer_transport_connected, transport)
                : getString(R.string.transfer_transport_waiting));
        homeActivityTitle.setText(ready
                ? getString(R.string.home_activity_connected_transport,
                transport)
                : getString(R.string.home_activity_disconnected));
        connectionStartedAtMillis = ready
                ? guardian.connectedAtMillis
                : 0L;
        updateConnectionElapsed();
        renderTransferHistory();
    }

    private void updateConnectionElapsed() {
        if (!transferSessionReady || connectionStartedAtMillis <= 0L) {
            homeActivityElapsed.setText("");
            return;
        }

        long elapsedSeconds = Math.max(0L,
                (System.currentTimeMillis() - connectionStartedAtMillis)
                        / 1000L);
        long hours = elapsedSeconds / 3600L;
        long minutes = (elapsedSeconds % 3600L) / 60L;
        long seconds = elapsedSeconds % 60L;
        homeActivityElapsed.setText(hours > 0L
                ? getString(R.string.home_activity_elapsed_hours,
                hours, minutes, seconds)
                : getString(R.string.home_activity_elapsed_minutes,
                minutes, seconds));
    }

    private void selectPage(Page selectedPage) {
        currentPage = selectedPage;
        resetPageTransforms();
        pageHome.setVisibility(selectedPage == Page.HOME ? View.VISIBLE : View.GONE);
        pageQuickSettings.setVisibility(
                selectedPage == Page.QUICK_SETTINGS ? View.VISIBLE : View.GONE);
        pageFileTransfer.setVisibility(
                selectedPage == Page.FILE_TRANSFER ? View.VISIBLE : View.GONE);
        pageSettings.setVisibility(
                selectedPage == Page.SETTINGS ? View.VISIBLE : View.GONE);

        updateNavigationItem(navHome, navHomeIcon, navHomeLabel,
                selectedPage == Page.HOME);
        updateNavigationItem(navQuickSettings, navQuickSettingsIcon,
                navQuickSettingsLabel,
                selectedPage == Page.QUICK_SETTINGS);
        updateNavigationItem(navFileTransfer, navFileTransferIcon,
                navFileTransferLabel,
                selectedPage == Page.FILE_TRANSFER);
        updateNavigationItem(navSettings, navSettingsIcon, navSettingsLabel,
                selectedPage == Page.SETTINGS);
        if (selectedPage == Page.FILE_TRANSFER) {
            renderTransferHistory();
        }
    }

    private void updateNavigationItem(View container, ImageView icon,
            TextView label, boolean selected) {
        container.setBackgroundResource(selected
                ? R.drawable.bg_nav_selected
                : android.R.color.transparent);
        int color = getColor(selected ? R.color.accent : R.color.text_secondary);
        icon.setImageTintList(ColorStateList.valueOf(color));
        label.setTextColor(color);
        label.setTypeface(null, selected ? Typeface.BOLD : Typeface.NORMAL);
        container.setSelected(selected);
    }

    @Override
    public boolean dispatchTouchEvent(MotionEvent event) {
        int action = event.getActionMasked();
        if (action == MotionEvent.ACTION_DOWN) {
            swipeStartX = event.getX();
            swipeStartY = event.getY();
            pageDragging = false;
            dragTargetPage = null;
            dragTargetView = null;
            return super.dispatchTouchEvent(event);
        }

        float deltaX = event.getX() - swipeStartX;
        float deltaY = event.getY() - swipeStartY;
        if (action == MotionEvent.ACTION_MOVE) {
            float startThreshold = 10f * getResources().getDisplayMetrics().density;
            if (!pageDragging
                    && Math.abs(deltaX) >= startThreshold
                    && Math.abs(deltaX) > Math.abs(deltaY) * 1.35f) {
                beginPageDrag(deltaX);
                MotionEvent cancel = MotionEvent.obtain(event);
                cancel.setAction(MotionEvent.ACTION_CANCEL);
                super.dispatchTouchEvent(cancel);
                cancel.recycle();
            }
            if (pageDragging) {
                updatePageDrag(deltaX);
                return true;
            }
            return super.dispatchTouchEvent(event);
        }

        if (action == MotionEvent.ACTION_UP && pageDragging) {
            finishPageDrag(deltaX, false);
            return true;
        }

        if (action == MotionEvent.ACTION_CANCEL && pageDragging) {
            finishPageDrag(deltaX, true);
            return true;
        }

        return super.dispatchTouchEvent(event);
    }

    private void beginPageDrag(float deltaX) {
        pageDragging = true;
        configureDragTarget(deltaX);
    }

    private void updatePageDrag(float deltaX) {
        Page desiredTarget = deltaX < 0f ? nextPage(currentPage) : previousPage(currentPage);
        if (desiredTarget != dragTargetPage) {
            if (dragTargetView != null) {
                dragTargetView.setVisibility(View.GONE);
                dragTargetView.setTranslationX(0f);
            }
            configureDragTarget(deltaX);
        }

        View currentView = pageView(currentPage);
        int width = Math.max(1, pageContainer.getWidth());
        if (dragTargetView == null) {
            currentView.setTranslationX(deltaX * 0.18f);
            return;
        }

        currentView.setTranslationX(deltaX);
        float targetStart = deltaX < 0f ? width : -width;
        dragTargetView.setTranslationX(targetStart + deltaX);
    }

    private void configureDragTarget(float deltaX) {
        dragTargetPage = deltaX < 0f ? nextPage(currentPage) : previousPage(currentPage);
        dragTargetView = pageView(dragTargetPage);
        if (dragTargetView == null) {
            return;
        }

        int width = Math.max(1, pageContainer.getWidth());
        dragTargetView.setVisibility(View.VISIBLE);
        dragTargetView.bringToFront();
        pageView(currentPage).bringToFront();
        dragTargetView.setTranslationX(deltaX < 0f ? width : -width);
    }

    private void finishPageDrag(float deltaX, boolean cancelled) {
        final View currentView = pageView(currentPage);
        final View targetView = dragTargetView;
        final Page targetPage = dragTargetPage;
        int width = Math.max(1, pageContainer.getWidth());
        float completionThreshold = Math.min(
                width * 0.22f,
                96f * getResources().getDisplayMetrics().density);
        boolean complete = !cancelled
                && targetView != null
                && Math.abs(deltaX) >= completionThreshold;

        if (complete) {
            float exitX = deltaX < 0f ? -width : width;
            currentView.animate()
                    .translationX(exitX)
                    .setDuration(180L)
                    .start();
            targetView.animate()
                    .translationX(0f)
                    .setDuration(180L)
                    .withEndAction(() -> selectPage(targetPage))
                    .start();
        } else {
            float targetRest = deltaX < 0f ? width : -width;
            currentView.animate()
                    .translationX(0f)
                    .setDuration(160L)
                    .start();
            if (targetView != null) {
                targetView.animate()
                        .translationX(targetRest)
                        .setDuration(160L)
                        .withEndAction(() -> {
                            targetView.setVisibility(View.GONE);
                            targetView.setTranslationX(0f);
                        })
                        .start();
            }
        }

        pageDragging = false;
        dragTargetPage = null;
        dragTargetView = null;
    }

    private Page nextPage(Page page) {
        if (page == Page.HOME) {
            return Page.QUICK_SETTINGS;
        }
        if (page == Page.QUICK_SETTINGS) {
            return Page.FILE_TRANSFER;
        }
        if (page == Page.FILE_TRANSFER) {
            return Page.SETTINGS;
        }
        return null;
    }

    private Page previousPage(Page page) {
        if (page == Page.SETTINGS) {
            return Page.FILE_TRANSFER;
        }
        if (page == Page.FILE_TRANSFER) {
            return Page.QUICK_SETTINGS;
        }
        if (page == Page.QUICK_SETTINGS) {
            return Page.HOME;
        }
        return null;
    }

    private View pageView(Page page) {
        if (page == null) {
            return null;
        }
        if (page == Page.HOME) {
            return pageHome;
        }
        if (page == Page.QUICK_SETTINGS) {
            return pageQuickSettings;
        }
        if (page == Page.FILE_TRANSFER) {
            return pageFileTransfer;
        }
        return pageSettings;
    }

    private void resetPageTransforms() {
        View[] pages = {pageHome, pageQuickSettings, pageFileTransfer, pageSettings};
        for (View page : pages) {
            page.animate().cancel();
            page.setTranslationX(0f);
        }
    }

    private void chooseFiles() {
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT)
                .addCategory(Intent.CATEGORY_OPENABLE)
                .setType("*/*")
                .putExtra(Intent.EXTRA_ALLOW_MULTIPLE, true)
                .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION
                        | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
        startActivityForResult(intent, REQUEST_FILES);
    }

    private void requestNotificationPermissionIfNeeded() {
        if (android.os.Build.VERSION.SDK_INT >= 33
                && checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)
                != PackageManager.PERMISSION_GRANTED) {
            requestPermissions(
                    new String[]{Manifest.permission.POST_NOTIFICATIONS},
                    REQUEST_NOTIFICATIONS);
        }
    }

    private void chooseFolder() {
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE)
                .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION
                        | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION
                        | Intent.FLAG_GRANT_PREFIX_URI_PERMISSION);
        startActivityForResult(intent, REQUEST_FOLDER);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode,
            Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if ((requestCode != REQUEST_FILES && requestCode != REQUEST_FOLDER)
                || resultCode != RESULT_OK || data == null) {
            return;
        }
        java.util.ArrayList<Uri> uris =
                ShareToPcActivity.collectUris(data);
        if (data.getData() != null && !uris.contains(data.getData())) {
            uris.add(data.getData());
        }
        if (uris.isEmpty()) {
            Toast.makeText(this, R.string.transfer_no_items,
                    Toast.LENGTH_LONG).show();
            return;
        }
        for (Uri uri : uris) {
            try {
                getContentResolver().takePersistableUriPermission(
                        uri, Intent.FLAG_GRANT_READ_URI_PERMISSION);
            } catch (SecurityException ignored) {
            }
        }
        if (PhoneTransferService.start(this, uris)) {
            lastHistoryRevision = Long.MIN_VALUE;
            renderTransferHistory();
            Toast.makeText(this, R.string.transfer_queued,
                    Toast.LENGTH_SHORT).show();
        } else {
            PhoneTransferService.releasePersistedUriPermissions(this, uris);
            Toast.makeText(this, R.string.transfer_queue_failed,
                    Toast.LENGTH_LONG).show();
        }
    }

    private void notifySurfaces() {
        CleanupWidgetProvider.updateAll(this);
        CleanupTileService.requestRefresh(this);
    }

    private void renderNativeDisplayCard() {
        NativeSessionBridge.Snapshot snapshot=NativeSessionBridge.inspect(this);
        NativeDisplayCard.Mode mode=NativeDisplayCard.mode(snapshot.active&&snapshot.confirmed,snapshot.inUse);
        homeNativeDexCard.setVisibility(mode==NativeDisplayCard.Mode.HIDDEN?View.GONE:View.VISIBLE);
        homeNativeDexTitle.setText(mode==NativeDisplayCard.Mode.RETAINED?R.string.home_native_dex_retained:R.string.home_native_dex_running);
        homeNativeDexDetail.setText(mode==NativeDisplayCard.Mode.IN_USE?R.string.home_native_dex_in_use_detail:
                mode==NativeDisplayCard.Mode.RETAINED?R.string.home_native_dex_retained_detail:R.string.home_native_dex_running_detail);
    }

    private void render(CleanupCoordinator.Snapshot snapshot) {
        renderNativeDisplayCard();
        overlayValue.setText(snapshot.overlay.rawValue == null
                ? getString(R.string.setting_not_present)
                : snapshot.overlay.rawValue);
        stayAwakeValue.setText(snapshot.stayAwake.rawValue == null
                ? "0"
                : snapshot.stayAwake.rawValue);

        overlayStatus.setText(snapshot.overlay.status==OverlayDisplayRepository.Status.UNKNOWN?R.string.target_status_unknown:
                snapshot.overlay.overlayActive?R.string.target_status_active:R.string.target_status_inactive);
        stayAwakeStatus.setText(snapshot.stayAwake.stayAwakeActive
                ? R.string.target_status_active
                : R.string.target_status_inactive);

        cleanupOverlayCard.setBackgroundResource(snapshot.overlay.overlayActive?R.drawable.bg_card:R.drawable.bg_connection_pending);
        cleanupStayAwakeCard.setBackgroundResource(snapshot.stayAwake.stayAwakeActive?R.drawable.bg_card:R.drawable.bg_connection_pending);
        overlayStatus.setTextColor(getColor(snapshot.overlay.overlayActive?R.color.success:R.color.text_secondary));
        stayAwakeStatus.setTextColor(getColor(snapshot.stayAwake.stayAwakeActive?R.color.success:R.color.text_secondary));
        boolean permissionGranted = !snapshot.permissionRequired();
        boolean valid = permissionGranted && !snapshot.hasError();
        settingsPermissionCard.setBackgroundResource(valid
                ? R.drawable.bg_status_card
                : R.drawable.bg_connection_pending);
        cleanupOverlayButton.setEnabled(valid && snapshot.overlay.overlayActive);
        cleanupStayAwakeButton.setEnabled(valid && snapshot.stayAwake.stayAwakeActive);
        cleanupBothButton.setEnabled(valid && snapshot.anyActive());
        homeCleanupButton.setEnabled(valid && snapshot.anyActive());
        int activeCount = (snapshot.overlay.overlayActive ? 1 : 0)
                + (snapshot.stayAwake.stayAwakeActive ? 1 : 0);
        if (activeCount == 0) {
            homeCleanupSummary.setText(snapshot.overlay.status==OverlayDisplayRepository.Status.UNKNOWN?R.string.home_cleanup_checking:R.string.home_cleanup_none);
        } else {
            homeCleanupSummary.setText(
                    getString(R.string.home_cleanup_count, activeCount));
        }

        if (snapshot.permissionRequired()) {
            settingsPermissionIcon.setImageResource(R.drawable.ic_warning);
            settingsPermissionIcon.setColorFilter(Color.rgb(245, 158, 11));
            settingsPermissionTitle.setText(R.string.status_permission_required);
            settingsPermissionDetail.setText(R.string.status_permission_required_detail);
        } else if (snapshot.hasError()) {
            settingsPermissionIcon.setImageResource(R.drawable.ic_warning);
            settingsPermissionIcon.setColorFilter(Color.rgb(220, 38, 38));
            settingsPermissionTitle.setText(R.string.status_error);
            String error = snapshot.overlay.error != null
                    ? snapshot.overlay.error
                    : snapshot.stayAwake.error;
            settingsPermissionDetail.setText(error == null
                    ? getString(R.string.status_error_detail)
                    : error);
        } else {
            settingsPermissionIcon.setImageResource(R.drawable.ic_settings);
            settingsPermissionIcon.clearColorFilter();
            settingsPermissionTitle.setText(R.string.permission_granted_title);
            settingsPermissionDetail.setText(R.string.permission_granted_detail);
        }
    }

    private void renderTransferHistory() {
        long revision = TransferHistoryStore.revision(this);
        if (revision == lastHistoryRevision) {
            return;
        }
        lastHistoryRevision = revision;
        List<TransferHistoryStore.Entry> entries =
                TransferHistoryStore.load(this);
        pcToPhoneHistoryList.removeAllViews();
        phoneToPcHistoryList.removeAllViews();
        int pcToPhoneCount = 0;
        int phoneToPcCount = 0;
        for (TransferHistoryStore.Entry entry : entries) {
            boolean phoneToPc = TransferHistoryStore
                    .DIRECTION_PHONE_TO_PC.equals(entry.direction);
            LinearLayout targetList = phoneToPc
                    ? phoneToPcHistoryList : pcToPhoneHistoryList;
            View row = getLayoutInflater().inflate(
                    R.layout.item_transfer_history,
                    targetList, false);
            ImageView icon = row.findViewById(R.id.history_direction_icon);
            TextView name = row.findViewById(R.id.history_name);
            TextView time = row.findViewById(R.id.history_time);
            TextView status = row.findViewById(R.id.history_status);

            icon.setImageResource(phoneToPc
                    ? R.drawable.ic_upload : R.drawable.ic_download);
            name.setText(historyName(entry));
            status.setText(historyStatus(entry.status));
            int dateFlags = DateUtils.FORMAT_SHOW_TIME;
            if (!DateUtils.isToday(entry.timestamp)) {
                dateFlags |= DateUtils.FORMAT_SHOW_DATE
                        | DateUtils.FORMAT_NUMERIC_DATE;
            }
            time.setText(DateUtils.formatDateTime(this, entry.timestamp,
                    dateFlags));
            targetList.addView(row);
            if (phoneToPc) {
                phoneToPcCount++;
            } else {
                pcToPhoneCount++;
            }
        }
        updateHistorySection(pcToPhoneHistoryList,
                pcToPhoneHistoryEmpty, clearPcToPhoneHistory,
                pcToPhoneCount);
        updateHistorySection(phoneToPcHistoryList,
                phoneToPcHistoryEmpty, clearPhoneToPcHistory,
                phoneToPcCount);
    }

    private void updateHistorySection(LinearLayout list, View empty,
            Button clearButton, int count) {
        boolean hasEntries = count > 0;
        list.setVisibility(hasEntries ? View.VISIBLE : View.GONE);
        empty.setVisibility(hasEntries ? View.GONE : View.VISIBLE);
        clearButton.setEnabled(hasEntries);
    }

    private void clearTransferHistory(String direction) {
        TransferHistoryStore.clear(this, direction);
        lastHistoryRevision = Long.MIN_VALUE;
        renderTransferHistory();
    }

    private String historyName(TransferHistoryStore.Entry entry) {
        if (entry.folder && !entry.name.isEmpty()) {
            return entry.itemCount > 1
                    ? getString(R.string.recent_transfer_folder_count,
                    entry.name, entry.itemCount)
                    : entry.name;
        }
        if (entry.itemCount > 1) {
            return getString(R.string.recent_transfer_files_count,
                    entry.itemCount);
        }
        return entry.name.isEmpty()
                ? getString(R.string.recent_transfer_file) : entry.name;
    }

    private int historyStatus(String status) {
        if (TransferHistoryStore.STATUS_TRANSFERRING.equals(status)) {
            return R.string.recent_transfer_status_transferring;
        }
        if (TransferHistoryStore.STATUS_COMPLETED.equals(status)) {
            return R.string.recent_transfer_status_completed;
        }
        if (TransferHistoryStore.STATUS_FAILED.equals(status)) {
            return R.string.recent_transfer_status_failed;
        }
        if (TransferHistoryStore.STATUS_CANCELED.equals(status)) {
            return R.string.recent_transfer_status_canceled;
        }
        return R.string.recent_transfer_status_queued;
    }

    private void showHomeMenu(View anchor) {
        PopupMenu menu = new PopupMenu(this, anchor);
        menu.getMenu().add(R.string.about_menu_item);
        menu.setOnMenuItemClickListener(item -> {
            showAboutDialog();
            return true;
        });
        menu.show();
    }

    private void showAboutDialog() {
        Dialog dialog = new Dialog(this);
        dialog.setContentView(R.layout.dialog_about);
        TextView version = dialog.findViewById(R.id.about_version);
        try {
            String versionName = getPackageManager().getPackageInfo(
                    getPackageName(), 0).versionName;
            version.setText(getString(R.string.about_version_format, versionName));
        } catch (PackageManager.NameNotFoundException ignored) {
            version.setVisibility(View.GONE);
        }
        dialog.findViewById(R.id.about_close_button)
                .setOnClickListener(view -> dialog.dismiss());
        dialog.setCanceledOnTouchOutside(true);
        dialog.show();
        Window window = dialog.getWindow();
        if (window != null) {
            window.setBackgroundDrawable(new ColorDrawable(
                    Color.TRANSPARENT));
            window.setLayout((int) (getResources().getDisplayMetrics()
                            .widthPixels * 0.88f),
                    WindowManager.LayoutParams.WRAP_CONTENT);
        }
    }

    private void setPreferenceChecks(CleanupPreferences.Targets targets) {
        updatingPreferences = true;
        tileWidgetOverlay.setChecked(targets.overlay);
        tileWidgetStayAwake.setChecked(targets.stayAwake);
        updatingPreferences = false;
    }

    private void savePreferenceChecks(int changedViewId) {
        if (updatingPreferences) {
            return;
        }

        boolean overlay = tileWidgetOverlay.isChecked();
        boolean stayAwake = tileWidgetStayAwake.isChecked();
        if (!overlay && !stayAwake) {
            updatingPreferences = true;
            if (changedViewId == R.id.tile_widget_overlay) {
                tileWidgetOverlay.setChecked(true);
            } else {
                tileWidgetStayAwake.setChecked(true);
            }
            updatingPreferences = false;
            Toast.makeText(this, R.string.cleanup_target_required,
                    Toast.LENGTH_SHORT).show();
            return;
        }

        CleanupPreferences.save(this, overlay, stayAwake);
        notifySurfaces();
    }

    private enum Page {
        HOME,
        QUICK_SETTINGS,
        FILE_TRANSFER,
        SETTINGS
    }
}

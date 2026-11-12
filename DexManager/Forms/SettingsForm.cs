using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed partial class SettingsForm : Form, IMessageFilter
    {
        internal Action<Form> UpdateCheckRequested { get; set; }
        private const int CardContentTop = 44;
        private const int CardContentBottom = 18;
        private static int CardWidth { get { return _desktopCardWidth; } }
        private static int CardContentWidth { get { return CardWidth - 40; } }
        private const int WmMouseWheel = 0x020A;
        private const string ProjectUrl =
            "https://github.com/maze-mei/DX-Manager";

        private readonly SettingsService _settingsService;
        private readonly AppSettings _settings;
        private readonly AdbService _adbService;
        private readonly DisplayCleanupPermissionService
            _displayCleanupPermissionService;
        private readonly WirelessAdbService _wirelessAdbService;
        private readonly Func<string> _getSelectedDeviceSerial;
        private readonly Func<string> _getSelectedDeviceIdentity;
        private readonly Func<DeviceRegistrySnapshot> _getDeviceSnapshot;
        private readonly Func<DeviceRuntimeRegistrySnapshot>
            _getRuntimeSnapshot;
        private readonly LogService _logService;
        private readonly DeviceVersionDiagnosticService
            _deviceVersionDiagnosticService;
        private readonly DiagnosticReportService _diagnosticReportService;
        private readonly Action _showLogs;
        private readonly Action _showEnvironmentCheck;
        private readonly Action<AppTheme> _applyTheme;
        private readonly Action<bool> _settingsChanged;
        private readonly Func<string, Task> _detachPhoneTransfer;
        private readonly Action<string> _companionInstalled;
        private ThemePalette _theme;
        private readonly List<Control> _pages = new List<Control>();
        private readonly List<ThemedButton> _navigationButtons =
            new List<ThemedButton>();
        private Panel _contentHost;
        private Panel _bottomPanel;
        private RoundedPanel _sidebar;
        private Label _titleLabel;
        private Label _descriptionLabel;

        private RadioButton _automaticAdbBox;
        private RadioButton _manualAdbBox;
        private ThemedTextControl _manualAdbPathBox;
        private Panel _manualAdbPanel;
        private ThemedTextControl _scrcpyPathBox;
        private ThemedTextControl _screenshotFolderBox;
        private ThemedTextControl _deviceScreenshotFolderBox;
        private ThemedTextControl _logFolderBox;
        private CheckBox _startWithWindowsBox;
        private CheckBox _startMinimizedBox;
        private ThemedSelectControl _wakeUpModeBox;
        private CheckBox _autoHideBox;
        private CheckBox _pushCaptureBox;
        private CheckBox _managedFileTransferBox;
        private ThemedTextControl _fileTransferTargetFolderBox;
        private CheckBox _phoneToPcTransferBox;
        private ThemedTextControl _phoneToPcReceiveFolderBox;
        private CheckBox _resetDisplayOnStopBox;
        private CheckBox _disableStayAwakeBox;
        private CheckBox _autoStartDexBox;
        private CheckBox _showConnectedDeviceInfoBox;
        private CheckBox _miniControlBarBox;
        private ThemedSelectControl _miniControlBarSideBox;
        private ThemedNumberControl _deviceMonitorIntervalBox;
        private ThemedNumberControl _disconnectMonitorIntervalBox;
        private ThemedNumberControl _connectedStartDelayBox;
        private ThemedNumberControl _adbWakeUpDelayBox;
        private ThemedNumberControl _autoHideSecondsBox;
        private ThemedNumberControl _captureWaitSecondsBox;
        private ThemedNumberControl _processTimeoutBox;
        private ThemedNumberControl _virtualDisplayTimeoutBox;
        private Control _advancedOptionsCard;
        private Button _advancedOptionsButton;
        private ThemedHotkeyControl _captureHotkeyBox;
        private ThemedHotkeyControl _exitHotkeyBox;
        private CheckBox _lowLevelHotkeyBox;
        private CheckBox _keyboardDiagnosticsBox;
        private CheckBox _convertHangulBox;
        private ThemedSelectControl _keyInputModeBox;
        private CheckBox _rightWindowsBox;
        private CheckBox _convertEnterBox;
        private CheckBox _autoAppEnterBox;
        private CheckBox _ignoreShiftSpaceBox;
        private RadioButton _usbConnectionBox;
        private RadioButton _wirelessConnectionBox;
        private ThemedSelectControl _wirelessDeviceBox;
        private ThemedTextControl _wirelessHostBox;
        private ThemedNumberControl _wirelessPortBox;
        private CheckBox _wirelessAutoReconnectBox;
        private Label _wirelessStatusLabel;
        private ThemedNumberControl _pairingPortBox;
        private ThemedTextControl _pairingCodeBox;
        private Button _wirelessPrepareButton;
        private Button _wirelessConnectButton;
        private Button _wirelessDisconnectButton;
        private Button _pairButton;
        private ThemedSelectControl _languageBox;
        private ThemedSelectControl _themeBox;
        private Label _saveStatusLabel;
        private Timer _saveStatusTimer;
        private ThirdPartyLicensesForm _thirdPartyLicensesForm;
        private Button _displayCleanupPermissionButton;
        private Button _companionInstallButton;
        private Button _companionUninstallButton;
        private Label _displayCleanupStatusLabel;
        private DisplayCleanupPermissionStatus _displayCleanupStatus;
        private BundledCompanionStatus _bundledCompanionStatus;
        private bool _displayCleanupOperationRunning;
        private bool _loadingWirelessDevice;
        private string _loadedWirelessDeviceIdentity = string.Empty;
        private int _activePageIndex;
        private Label _deviceDiagnosticsStatusLabel;
        private Button _deviceDiagnosticsRefreshButton;
        private FlowLayoutPanel _deviceDiagnosticsCardsPanel;
        private Button _diagnosticReportButton;
        private List<DeviceVersionDiagnostic> _lastDeviceDiagnostics =
            new List<DeviceVersionDiagnostic>();
        private int _deviceDiagnosticsGeneration;

        public SettingsForm(
            SettingsService settingsService,
            AppSettings settings,
            AdbService adbService,
            WirelessAdbService wirelessAdbService,
            Func<string> getSelectedDeviceSerial,
            Func<string> getSelectedDeviceIdentity,
            Func<DeviceRegistrySnapshot> getDeviceSnapshot,
            Func<DeviceRuntimeRegistrySnapshot> getRuntimeSnapshot,
            LogService logService,
            Action showLogs,
            Action showEnvironmentCheck,
            Action<AppTheme> applyTheme,
            Action<bool> settingsChanged,
            Func<string, Task> detachPhoneTransfer,
            Action<string> companionInstalled,
            Action<bool> hotkeyEditing = null,
            Func<IList<EnvironmentCheckItem>> runEnvironmentCheck = null)
        {
            _hotkeyEditing = hotkeyEditing;
            _runStoreEnvironment = runEnvironmentCheck;
            _mainSelectedSerial = getSelectedDeviceSerial;
            _mainSelectedIdentity = getSelectedDeviceIdentity;
            _settingsService = settingsService;
            _settings = settings;
            _adbService = adbService;
            _displayCleanupPermissionService =
                new DisplayCleanupPermissionService(adbService);
            _wirelessAdbService = wirelessAdbService;
            _getSelectedDeviceSerial = ResolveStoreTargetSerial;
            _getSelectedDeviceIdentity = ResolveStoreTargetIdentity;
            _getDeviceSnapshot = getDeviceSnapshot ??
                delegate { return new DeviceRegistrySnapshot(); };
            _getRuntimeSnapshot = getRuntimeSnapshot ??
                delegate { return new DeviceRuntimeRegistrySnapshot(); };
            _logService = logService ?? new LogService();
            _deviceVersionDiagnosticService =
                new DeviceVersionDiagnosticService(adbService);
            _diagnosticReportService = new DiagnosticReportService();
            _showLogs = showLogs;
            _showEnvironmentCheck = showEnvironmentCheck;
            _applyTheme = applyTheme;
            _settingsChanged = settingsChanged;
            _detachPhoneTransfer = detachPhoneTransfer;
            _companionInstalled = companionInstalled;
            _theme = ThemeColors.Current;

            Text = LocalizationService.Get("App.Name") + " · " + StoreUiText.Get("SettingsTitle.Text", LocalizationService.Get("Main.Settings"));
            Icon = AppIconProvider.Current;
            StartPosition = FormStartPosition.CenterParent;
            Font = UiFonts.Pixels(14F);
            BackColor = _theme.WindowBackground;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            UiWindowStyle.ApplyStoreSize(this, 1040, 760);
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(820, 600);
            _desktopCardWidth = Math.Max(520, Math.Min(716, (int)(ClientSize.Width / Math.Max(1F, CurrentAutoScaleDimensions.Width / 96F)) - 296));

            _titleLabel = new UiLabel
            {
                AutoSize = false,
                Font = UiFonts.Pixels(28F, true),
                ForeColor = _theme.TextPrimary,
                Location = new Point(224, 22),
                Size = new Size(670, 40),
                Text = LocalizationService.Get("Main.Settings")
            };
            _descriptionLabel = new UiLabel
            {
                AutoEllipsis = true,
                ForeColor = _theme.TextTertiary,
                Location = new Point(226, 67),
                Size = new Size(670, 29),
                Text = LocalizationService.Get("Settings.Description")
            };

            _contentHost = new BufferedPanel
            {
                Location = new Point(220, 104),
                Size = new Size(684, 510),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                    AnchorStyles.Left | AnchorStyles.Right,
                BackColor = _theme.WindowBackground
            };
            _pages.AddRange(BuildStoreSettingsPages());
            foreach (var page in _pages)
            {
                page.Dock = DockStyle.Fill;
                page.Visible = false;
                _contentHost.Controls.Add(page);
            }

            _bottomPanel = new Panel
            {
                Location = new Point(220, 620),
                Size = new Size(684, 62),
                Anchor = AnchorStyles.Left | AnchorStyles.Right |
                    AnchorStyles.Bottom,
                BackColor = _theme.WindowBackground
            };
            _saveStatusLabel = new UiLabel
            {
                ForeColor = _theme.TextTertiary,
                Location = new Point(0, 14),
                Size = new Size(440, 36),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Visible = false
            };
            var saveButton = new ThemedButton
            {
                Primary = true,
                Text = LocalizationService.Get("Common.Save"),
                Location = new Point(440, 14),
                Size = new Size(100, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            saveButton.Visible = false;
            saveButton.Primary = false;
            _desktopSaveButton = saveButton;
            saveButton.Click += SaveButton_Click;
            var closeButton = new ThemedButton
            {
                Text = LocalizationService.Get("Common.Close"),
                Location = new Point(550, 14),
                Size = new Size(100, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            closeButton.Primary = true;
            _desktopCloseButton = closeButton;
            closeButton.Click += delegate { Close(); };
            _bottomPanel.Controls.Add(_saveStatusLabel);
            _bottomPanel.Controls.Add(saveButton);
            _bottomPanel.Controls.Add(closeButton);
            _saveStatusTimer = new Timer { Interval = 2800 };
            _saveStatusTimer.Tick += delegate
            {
                _saveStatusTimer.Stop();
                _saveStatusLabel.Visible = false;
            };
            FormClosed += delegate
            {
                Application.RemoveMessageFilter(this);
                _saveStatusTimer.Dispose();
            };

            Controls.Add(BuildSidebar());
            Controls.Add(_contentHost);
            Controls.Add(_titleLabel);
            Controls.Add(_descriptionLabel);
            Controls.Add(_bottomPanel);
            LayoutDesktopSettings();
            Resize += delegate { LayoutDesktopSettings(); };
            ApplyCurrentTheme();
            LoadValues();
            _storeUiReady = true;
            RestoreStoreExpansion();
            AttachStoreBindings();
            UpdateStoreCardSizes();
            ShowPage(0);
            Application.AddMessageFilter(this);
        }
    }
}

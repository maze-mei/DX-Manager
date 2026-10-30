using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed partial class MainForm : Form
    {
        private const int MaxRememberedApps = 20;
        private const string MissingStayAwakeValue = "<missing>";

        private static string NoStartAppText
        {
            get { return LocalizationService.Get("Main.NoApp"); }
        }

        private readonly SettingsService _settingsService;
        private readonly AppSettings _settings;
        private readonly LogService _logService;
        private readonly AdbService _adbService;
        private readonly WirelessAdbService _wirelessAdbService;
        private ScrcpyService _scrcpyService;
        private SingleWindowService _singleWindowService;
        private ScreenOffService _screenOffService;
        private readonly ScrcpyLaunchCoordinator _launchCoordinator;
        private readonly DeviceMonitorService _deviceMonitor;
        private DexOrchestrator _orchestrator;
        private CaptureCoordinator _captureCoordinator;
        private AutoHideService _autoHideService;
        private readonly AutoStartService _autoStartService;
        private EnvironmentCheckService _environmentCheckService;
        private KeyMappingService _keyMappingService;
        private FileTransferCoordinator _fileTransferCoordinator;
        private PhoneTransferReceiver _phoneTransferReceiver;
        private readonly DeviceRuntimeSessionRegistry _runtimeSessions;
        private DeviceRuntimeServiceSet _activeRuntime;
        private MiniControlBarManager _miniControlBarManager;
        private readonly PhysicalDeviceRegistry _physicalDeviceRegistry;
        private readonly DeviceRuntimeServiceFactory _runtimeServiceFactory;
        private readonly PathService _pathService;
        private readonly CaptureService _captureService;
        private readonly bool _isAutoRun;
        private readonly TrayService _trayService;
        private readonly Label _adbStatusValue;
        private readonly Label _deviceStatusValue;
        private readonly Label _scrcpyStatusValue;
        private readonly Label _dexStatusValue;
        private ThemePalette _theme;
        private readonly Label _pageTitle;
        private readonly StatusRing _indicatorDot;
        private readonly Label _indicatorStatus;
        private readonly Label _indicatorDetail;
        private readonly Label _deviceInfoLabel;
        private readonly Button _startButton;
        private readonly Button _stopButton;
        private readonly LinkLabel _applySettingsLink;
        private readonly ThemedSelectControl _resolutionBox;
        private readonly ThemedNumberControl _widthBox;
        private readonly ThemedNumberControl _heightBox;
        private readonly Label _widthLabel;
        private readonly Label _heightLabel;
        private readonly Label _dpiLabel;
        private readonly Label _resolutionLabel;
        private readonly Label _bitRateLabel;
        private readonly Label _maxFpsLabel;
        private readonly Label _startAppLabel;
        private readonly Label _optionsTitle;
        private readonly ThemedNumberControl _dpiBox;
        private readonly ThemedNumberControl _bitRateBox;
        private readonly ThemedSelectControl _maxFpsBox;
        private readonly CheckBox _turnScreenOffBox;
        private readonly CheckBox _stayAwakeBox;
        private readonly CheckBox _useHidKeyboardBox;
        private readonly CheckBox _useHidMouseBox;
        private readonly CheckBox _forceStopAppBox;
        private readonly CheckBox _flexDisplayBox;
        private readonly CheckBox _hidePhonePreviewBox;
        private readonly ThemedTextControl _additionalArgumentsBox;
        private readonly ThemedSelectControl _startAppBox;
        private readonly Button _loadAppsButton;
        private readonly Button _appProfileButton;
        private readonly ContextMenuStrip _appProfileMenu;
        private readonly ToolStripMenuItem _saveAppProfileMenuItem;
        private readonly ToolStripMenuItem _deleteAppProfileMenuItem;
        private readonly LinkLabel _advancedToggle;
        private readonly Label _modeHintLabel;
        private readonly Label _displaySettingsTitle;
        private RoundedPanel _sidebar;
        private RoundedPanel _statusCard;
        private RoundedPanel _displayCard;
        private RoundedPanel _optionsCard;
        private readonly Timer _phoneScreenWakeTimer;
        private ThemedButton _dexModeButton;
        private ThemedButton _singleModeButton1;
        private ThemedButton _singleModeButton2;
        private ThemedButton _singleModeButton3;
        private ThemedButton _settingsSidebarButton;
        private Label _deviceSectionLabel;
        private Label _modeSectionLabel;
        private Label _sidebarHintLabel;
        private Panel _deviceSectionDivider;
        private int _selectedMode;
        private int _phoneScreenWakeSuppression;
        private int _screenOffReapplyGeneration;
        private bool _loadingRunSettings;
        private readonly Dictionary<DexOrchestrator, int> _phonePreviewPendingStarts =
            new Dictionary<DexOrchestrator, int>();
        private bool _resolutionSelectionInitialized;
        private bool _resolutionWasCustom;
        private readonly object _stayAwakeTaskLock = new object();
        private readonly object _stayAwakeValuesSync = new object();
        private readonly Dictionary<string, string> _stayAwakeOriginalValues =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private Task _stayAwakeUpdateTask = Task.FromResult(0);
        private readonly HashSet<string> _managedSerialHistory =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _deferredPhoneWakeSerials =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _phoneScreenWakeInProgress =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _deviceConnectionSync = new object();
        private readonly Dictionary<string, DateTime> _deviceConnectedAtUtc =
            new Dictionary<string, DateTime>(
                StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _disconnectedSerials =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private DeviceState _lastDeviceState;
        private string _connectionError;
        private bool _allowExit;
        private bool _exitInProgress;
        private bool _systemShutdownInProgress;
        private Task _exitCleanupTask;
        private bool _forcedCloseContinuationScheduled;
        private bool[] _modeSettingsDirty = new bool[4];
        private LogForm _logForm;
        private SettingsForm _settingsForm;
        private EnvironmentCheckForm _environmentCheckForm;
        private FileTransferStatusForm _fileTransferStatusForm;
        private PhoneTransferStatusForm _phoneTransferStatusForm;
        private long _lastFileTransferProgressSequence;
        private long _lastPhoneTransferProgressSequence;
        private FileTransferCoordinator _fileTransferStatusSource;
        private PhoneTransferReceiver _phoneTransferStatusSource;

        internal bool ShouldDeferProcessCleanupToWindows
        {
            get
            {
                return _systemShutdownInProgress;
            }
        }
        private readonly Dictionary<string, DeviceUiContext> _deviceContexts =
            new Dictionary<string, DeviceUiContext>(
                StringComparer.OrdinalIgnoreCase);
        private readonly object _deviceContextsSync = new object();
        private readonly Dictionary<string, ThemedButton> _deviceTabButtons =
            new Dictionary<string, ThemedButton>(
                StringComparer.OrdinalIgnoreCase);
        private readonly ToolTip _deviceTabToolTip = new ToolTip
        {
            InitialDelay = 350,
            ReshowDelay = 100,
            AutoPopDelay = 5000,
            ShowAlways = true
        };
        private readonly DevicePresentationOrder _devicePresentationOrder =
            new DevicePresentationOrder();
        private readonly FlowLayoutPanel _deviceTabsPanel;
        private DeviceUiContext _selectedDeviceContext;
        private DeviceUiContext _initialDeviceContext;
        private string _selectedDeviceIdentity = string.Empty;
        private bool _interactiveServicesStarted;
        private bool _deviceTabsVisibleForRun;

        public MainForm(
            SettingsService settingsService,
            AppSettings settings,
            LogService logService,
            AdbService adbService,
            WirelessAdbService wirelessAdbService,
            ScrcpyService scrcpyService,
            SingleWindowService singleWindowService,
            ScreenOffService screenOffService,
            ScrcpyLaunchCoordinator launchCoordinator,
            DeviceMonitorService deviceMonitor,
            DexOrchestrator orchestrator,
            CaptureCoordinator captureCoordinator,
            AutoHideService autoHideService,
            AutoStartService autoStartService,
            EnvironmentCheckService environmentCheckService,
            KeyMappingService keyMappingService,
            FileTransferCoordinator fileTransferCoordinator,
            PhoneTransferReceiver phoneTransferReceiver,
            DeviceRuntimeSessionRegistry runtimeSessions,
            DeviceRuntimeServiceSet activeRuntime,
            PhysicalDeviceRegistry physicalDeviceRegistry,
            DeviceRuntimeServiceFactory runtimeServiceFactory,
            PathService pathService,
            CaptureService captureService,
            bool isAutoRun)
        {
            _settingsService = settingsService;
            _settings = settings;
            _lastAutoStartEnabled = settings.Features.AutoStartDexOnDeviceConnected;
            _logService = logService;
            _adbService = adbService;
            _wirelessAdbService = wirelessAdbService;
            _scrcpyService = scrcpyService;
            _singleWindowService = singleWindowService;
            _screenOffService = screenOffService;
            _launchCoordinator = launchCoordinator;
            _deviceMonitor = deviceMonitor;
            _orchestrator = orchestrator;
            _captureCoordinator = captureCoordinator;
            _autoHideService = autoHideService;
            _autoStartService = autoStartService;
            _environmentCheckService = environmentCheckService;
            _keyMappingService = keyMappingService;
            _fileTransferCoordinator = fileTransferCoordinator ??
                throw new ArgumentNullException("fileTransferCoordinator");
            _phoneTransferReceiver = phoneTransferReceiver ??
                throw new ArgumentNullException("phoneTransferReceiver");
            _runtimeSessions = runtimeSessions ??
                throw new ArgumentNullException("runtimeSessions");
            _activeRuntime = activeRuntime ??
                throw new ArgumentNullException("activeRuntime");
            _physicalDeviceRegistry = physicalDeviceRegistry ??
                throw new ArgumentNullException("physicalDeviceRegistry");
            _runtimeServiceFactory = runtimeServiceFactory ??
                throw new ArgumentNullException("runtimeServiceFactory");
            _pathService = pathService ??
                throw new ArgumentNullException("pathService");
            _captureService = captureService ??
                throw new ArgumentNullException("captureService");
            _miniControlBarManager = new MiniControlBarManager(
                _settings,
                _scrcpyService,
                _singleWindowService,
                _captureCoordinator,
                ShowMainWindow,
                _logService);
            _isAutoRun = isAutoRun;
            _lastDeviceState = DeviceState.Disconnected();
            _selectedMode = 0;
            _theme = ThemeColors.Use(_settings.Theme);

            Text = LocalizationService.Get("App.Name");
            Icon = AppIconProvider.Current;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = _theme.WindowBackground;
            Font = UiFonts.Pixels(14F);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            UiWindowStyle.ApplyStoreSize(this, 1200, 800);
            AutoScroll = false;

            _pageTitle = new UiLabel
            {
                AutoSize = true,
                Font = UiFonts.Create(22F, FontStyle.Bold),
                ForeColor = _theme.TextPrimary,
                Location = new Point(32, 28),
                Text = LocalizationService.Get("App.Name")
            };
            Controls.Add(_pageTitle);

            _deviceTabsPanel = new FlowLayoutPanel
            {
                Location = new Point(10, 42),
                Size = new Size(168, 116),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = _theme.NavigationBackground,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            _indicatorDot = new StatusRing
            {
                Location = new Point(33, 91)
            };
            _indicatorStatus = new UiLabel
            {
                AutoSize = false,
                Font = UiFonts.Create(15F, FontStyle.Bold),
                ForeColor = _theme.TextPrimary,
                Location = new Point(66, 90),
                Size = new Size(240, 31),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _indicatorDetail = new UiLabel
            {
                AutoEllipsis = true,
                ForeColor = _theme.TextTertiary,
                Location = new Point(35, 130),
                Size = new Size(570, 22)
            };
            _deviceInfoLabel = new UiLabel
            {
                AutoEllipsis = true,
                ForeColor = _theme.TextTertiary,
                Location = new Point(35, 157),
                Size = new Size(570, 22),
                Text = LocalizationService.Get("Main.WaitingPhone")
            };
            Controls.Add(_indicatorDot);
            Controls.Add(_indicatorStatus);
            Controls.Add(_indicatorDetail);
            Controls.Add(_deviceInfoLabel);
            SetConnectionIndicator(
                Color.DarkOrange,
                LocalizationService.Get("Main.Waiting"),
                LocalizationService.Get("Main.PreparingAdb"));

            _adbStatusValue = new UiLabel { Visible = false };
            _scrcpyStatusValue = new UiLabel { Visible = false };
            _dexStatusValue = new UiLabel { Visible = false };
            _deviceStatusValue = new UiLabel { Visible = false };
            AddDivider(204);

            _displaySettingsTitle = AddSectionTitle(
                LocalizationService.Get("Main.DisplaySettings.Dex"),
                32,
                226);
            _resolutionBox = CreateCustomSelect(105, 263, 130);
            _resolutionBox.TabIndex = 0;
            _resolutionBox.Items.Add(new ResolutionPreset("1600 x 900", 1600, 900));
            _resolutionBox.Items.Add(new ResolutionPreset("1920 x 1080", 1920, 1080));
            _resolutionBox.Items.Add(new ResolutionPreset("3840 x 2160 (4K)", 3840, 2160));
            _resolutionBox.Items.Add(new ResolutionPreset(
                LocalizationService.Get("Main.Custom"), 0, 0));
            _resolutionBox.SelectedIndexChanged += ResolutionBox_SelectedIndexChanged;
            _widthBox = CreateCustomNumber(
                320, 4096, 285, 263, 55, false);
            _heightBox = CreateCustomNumber(
                240, 4096, 395, 263, 55, false);
            _widthBox.MaxLength = 4;
            _heightBox.MaxLength = 4;
            _dpiBox = CreateCustomNumber(
                120, 640, 490, 263, 90, true);
            _widthBox.RestorePreviousValueOnMaximumReject = true;
            _heightBox.RestorePreviousValueOnMaximumReject = true;
            _dpiBox.RestorePreviousValueOnMinimumReject = true;
            _widthBox.MaximumValueRejected += ResolutionBox_MaximumValueRejected;
            _heightBox.MaximumValueRejected += ResolutionBox_MaximumValueRejected;
            _dpiBox.MinimumValueRejected += DpiBox_MinimumValueRejected;
            _bitRateBox = CreateCustomNumber(
                1, 9999, 105, 298, 130, false);
            _maxFpsBox = CreateCustomSelect(495, 298, 90);
            _widthBox.TabIndex = 1;
            _heightBox.TabIndex = 2;
            _dpiBox.TabIndex = 3;
            _bitRateBox.TabIndex = 4;
            _maxFpsBox.TabIndex = 5;
            _maxFpsBox.Items.Add(30);
            _maxFpsBox.Items.Add(60);
            _maxFpsBox.Items.Add(new ExperimentalFpsOption());
            _resolutionLabel = AddFieldLabel(
                LocalizationService.Get("Main.Resolution"), 32, 269);
            _widthLabel = AddFieldLabel(LocalizationService.Get("Main.Width"), 240, 269);
            _heightLabel = AddFieldLabel(LocalizationService.Get("Main.Height"), 345, 269);
            _dpiLabel = AddFieldLabel("DPI", 460, 269);
            _bitRateLabel = AddFieldLabel(
                LocalizationService.Get("Main.Bitrate"), 32, 304);
            _maxFpsLabel = AddFieldLabel(
                LocalizationService.Get("Main.MaxFps"), 425, 304);

            AddDivider(339);
            _optionsTitle = AddSectionTitle(
                LocalizationService.Get("Main.Options"), 32, 360);
            _turnScreenOffBox = CreateOption(LocalizationService.Get("Main.ScreenOff"), 32, 395);
            _useHidKeyboardBox = CreateOption(LocalizationService.Get("Main.HidKeyboard"), 32, 429);
            _useHidMouseBox = CreateOption(LocalizationService.Get("Main.HidMouse"), 32, 463);
            _forceStopAppBox = CreateOption(LocalizationService.Get("Main.ForceStop"), 392, 395);
            _flexDisplayBox = CreateOption(
                LocalizationService.Get("Main.FlexDisplay"),
                392,
                429);
            _flexDisplayBox.Visible = false;
            _stayAwakeBox = CreateOption(
                LocalizationService.Get("Main.StayAwake"),
                392,
                463);
            _hidePhonePreviewBox = CreateOption(
                LocalizationService.Get("Main.HidePhonePreview"),
                392,
                497);
            _deviceTabToolTip.SetToolTip(
                _hidePhonePreviewBox,
                LocalizationService.Get("Main.HidePhonePreview.Help"));

            _startAppBox = CreateCustomSelect(132, 502, 313);
            _startAppBox.SelectionChangeCommitted +=
                StartAppBox_SelectionChangeCommitted;
            AddNoStartAppItem();
            _loadAppsButton = CreateThemedButton(
                LocalizationService.Get("Main.LoadApps"),
                false,
                455,
                501,
                150);
            _loadAppsButton.Click += LoadAppsButton_Click;
            _startAppLabel = AddFieldLabel(
                LocalizationService.Get("Main.StartApp"), 32, 508);

            _saveAppProfileMenuItem = new ToolStripMenuItem(
                LocalizationService.Get("Main.AppProfile.Save"));
            _saveAppProfileMenuItem.Click += SaveAppProfileButton_Click;
            _deleteAppProfileMenuItem = new ToolStripMenuItem(
                LocalizationService.Get("Main.AppProfile.Delete"));
            _deleteAppProfileMenuItem.Click +=
                DeleteAppProfileButton_Click;
            _appProfileMenu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                Font = UiFonts.Create(9.5F)
            };
            _appProfileMenu.Items.Add(_saveAppProfileMenuItem);
            _appProfileMenu.Items.Add(_deleteAppProfileMenuItem);
            _appProfileButton = CreateThemedButton(
                LocalizationService.Get("Main.AppProfile.SelectApp"),
                false,
                445,
                471,
                160);
            _appProfileButton.Size = new Size(160, 27);
            _appProfileButton.Visible = false;
            _appProfileButton.Click += SaveAppProfileButton_Click;

            _additionalArgumentsBox = CreateCustomText(32, 577, 440);
            _additionalArgumentsBox.PlaceholderText = LocalizationService.IsKorean ? "예: --no-audio --video-codec=h264" : "e.g. --no-audio --video-codec=h264";
            _additionalArgumentsBox.Visible = false;
            _advancedToggle = new UiLinkLabel
            {
                AutoSize = true,
                LinkColor = _theme.Accent,
                ActiveLinkColor = _theme.AccentHover,
                Location = new Point(32, 546),
                Text = LocalizationService.Get("Main.AdvancedClosed")
            };
            _advancedToggle.LinkClicked += delegate
            {
                _additionalArgumentsBox.Visible = !_additionalArgumentsBox.Visible;
                LayoutDesktopContent();
                _advancedToggle.Text = _additionalArgumentsBox.Visible
                    ? LocalizationService.Get("Main.AdvancedOpen")
                    : LocalizationService.Get("Main.AdvancedClosed");
            };
            Controls.Add(_advancedToggle);

            _startButton = CreateThemedButton(
                LocalizationService.Get("Main.StartDex"),
                true,
                453,
                580,
                152);
            _startButton.Click += StartButton_Click;
            _stopButton = CreateThemedButton(
                LocalizationService.Get("Main.StopDex"),
                true,
                453,
                580,
                152);
            _stopButton.Click += StopButton_Click;
            _stopButton.Visible = false;
            _applySettingsLink = new UiLinkLabel
            {
                AutoSize = true,
                LinkBehavior = LinkBehavior.HoverUnderline,
                LinkColor = Color.FromArgb(37, 99, 235),
                ActiveLinkColor = Color.FromArgb(29, 78, 216),
                Location = new Point(338, 589),
                Text = LocalizationService.Get("Main.ApplyChanges"),
                Visible = false
            };
            _applySettingsLink.LinkClicked += delegate
            {
                ApplyRunSettingsButton_Click(
                    _applySettingsLink,
                    EventArgs.Empty);
            };
            Controls.Add(_resolutionBox);
            Controls.Add(_widthBox);
            Controls.Add(_heightBox);
            Controls.Add(_dpiBox);
            Controls.Add(_bitRateBox);
            Controls.Add(_maxFpsBox);
            Controls.Add(_turnScreenOffBox);
            Controls.Add(_useHidKeyboardBox);
            Controls.Add(_useHidMouseBox);
            Controls.Add(_forceStopAppBox);
            Controls.Add(_flexDisplayBox);
            Controls.Add(_hidePhonePreviewBox);
            Controls.Add(_stayAwakeBox);
            Controls.Add(_startAppBox);
            Controls.Add(_loadAppsButton);
            Controls.Add(_appProfileButton);
            Controls.Add(_additionalArgumentsBox);
            Controls.Add(_startButton);
            Controls.Add(_stopButton);
            Controls.Add(_applySettingsLink);
            _modeHintLabel = new UiLabel
            {
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(107, 114, 128),
                Location = new Point(32, 614),
                Size = new Size(360, 22),
                Text = LocalizationService.Get("Main.DexMode")
            };
            Controls.Add(_modeHintLabel);
            _phoneScreenWakeTimer = new Timer { Interval = 600 };
            _phoneScreenWakeTimer.Tick += PhoneScreenWakeTimer_Tick;
            AddModeSidebar();
            ApplyDesignLayout();
            ApplyTheme();
            AttachRunSettingChangeHandlers();
            LoadRunSettings();

            _initialDeviceContext = CreateInitialDeviceContext();
            _selectedDeviceContext = _initialDeviceContext;

            Shown += MainForm_Shown;
            FormClosing += MainForm_FormClosing;
            FormClosed += MainForm_FormClosed;
            _deviceMonitor.StateChanged += DeviceMonitor_StateChanged;
            _scrcpyService.RunningChanged += ScrcpyService_RunningChanged;
            _singleWindowService.RunningChanged +=
                SingleWindowService_RunningChanged;
            _captureCoordinator.ExitHotkeyPressed += CaptureCoordinator_ExitHotkeyPressed;
            _autoHideService.IdleHideRequested += AutoHideService_IdleHideRequested;
            _fileTransferCoordinator.ProgressChanged +=
                FileTransferCoordinator_ProgressChanged;
            _phoneTransferReceiver.ProgressChanged +=
                PhoneTransferReceiver_ProgressChanged;
            _physicalDeviceRegistry.SnapshotChanged +=
                PhysicalDeviceRegistry_SnapshotChanged;
            _trayService = new TrayService(
                ShowMainWindow,
                async delegate { await StartDexAsync(); },
                async delegate { await StopDexAsync(); },
                ShowSettingsForm,
                ShowEnvironmentCheck,
                ShowLogForm,
                ExitApplication);
        }

        private async void MainForm_Shown(object sender, EventArgs e)
        {
            var onboarding = new PortableOnboardingService(System.IO.Path.GetDirectoryName(_settingsService.SettingsFilePath));
            var existingInstallation = onboarding.HasAcceptedAnyFirstRunNotice;
            if (onboarding.ShouldShowNoticeForLaunch())
            {
                if (!DesktopDialogs.ConfirmFirstRun(this)) { _allowExit = true; Close(); return; }
                string error;
                if (!onboarding.TryAccept(out error)) ShowDesktopNotice(StoreUiText.Get("FirstRunDialogSaveFailed", "The acknowledgement could not be saved; this notice may appear again.") + " " + error, DesktopNoticeKind.Warning);
            }
            if (_exitInProgress || IsDisposed) return;
            ShowLocalUpdateNotice(existingInstallation, System.IO.Path.GetDirectoryName(_settingsService.SettingsFilePath));
            if (_exitInProgress || IsDisposed) return;
            _logService.Info(
                LocalizationService.Get("Log.Main.Shown"));
            try { _captureCoordinator.Start(); }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.CaptureHotkeyRegistrationFailed"),
                    ex);
                _trayService.ShowBalloon(
                    LocalizationService.Get("App.Name"),
                    LocalizationService.Get("Main.CaptureHotkeyFailed"));
            }

            if (_settings.Features.AutoHideEnabled) _autoHideService.Start();
            try { _keyMappingService.Start(); }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.KeyMappingStartFailed"),
                    ex);
                _trayService.ShowBalloon(
                    LocalizationService.Get("App.Name"),
                    LocalizationService.Get("Main.KeyMappingFailed"));
            }

            _miniControlBarManager.Start();
            _interactiveServicesStarted = true;

            BeginAutomaticUpdateCheck();
            await InitializeAdbAndMonitorAsync();
            if (_exitInProgress || IsDisposed) return;
            if (_isAutoRun && _settings.Features.StartMinimizedToTray)
                BeginInvoke((Action)delegate { if (!_explicitShowRequested) HideToTray(); });
        }
    }
}

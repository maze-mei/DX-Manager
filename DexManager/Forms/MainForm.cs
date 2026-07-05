using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed class MainForm : Form
    {
        private static string NoStartAppText
        {
            get { return LocalizationService.Get("Main.NoApp"); }
        }

        private readonly SettingsService _settingsService;
        private readonly AppSettings _settings;
        private readonly LogService _logService;
        private readonly AdbService _adbService;
        private readonly WirelessAdbService _wirelessAdbService;
        private readonly ScrcpyService _scrcpyService;
        private readonly SingleWindowService _singleWindowService;
        private readonly ScreenOffService _screenOffService;
        private readonly DeviceMonitorService _deviceMonitor;
        private readonly DexOrchestrator _orchestrator;
        private readonly CaptureCoordinator _captureCoordinator;
        private readonly AutoHideService _autoHideService;
        private readonly EnvironmentCheckService _environmentCheckService;
        private readonly KeyMappingService _keyMappingService;
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
        private readonly CheckBox _reuseDisplayBox;
        private readonly CheckBox _flexDisplayBox;
        private readonly ThemedTextControl _additionalArgumentsBox;
        private readonly ThemedSelectControl _startAppBox;
        private readonly Button _loadAppsButton;
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
        private int _selectedMode;
        private int _phoneScreenWakeSuppression;
        private int _lastManagedScrcpyCount;
        private int _screenOffReapplyGeneration;
        private bool _loadingRunSettings;
        private bool _resolutionSelectionInitialized;
        private bool _resolutionWasCustom;
        private bool? _lastAppliedStayAwakeState;
        private DeviceState _lastDeviceState;
        private string _connectionError;
        private bool _allowExit;
        private bool _exitInProgress;
        private readonly bool[] _modeSettingsDirty = new bool[4];
        private LogForm _logForm;
        private SettingsForm _settingsForm;
        private EnvironmentCheckForm _environmentCheckForm;

        public MainForm(
            SettingsService settingsService,
            AppSettings settings,
            LogService logService,
            AdbService adbService,
            WirelessAdbService wirelessAdbService,
            ScrcpyService scrcpyService,
            SingleWindowService singleWindowService,
            ScreenOffService screenOffService,
            DeviceMonitorService deviceMonitor,
            DexOrchestrator orchestrator,
            CaptureCoordinator captureCoordinator,
            AutoHideService autoHideService,
            EnvironmentCheckService environmentCheckService,
            KeyMappingService keyMappingService,
            bool isAutoRun)
        {
            _settingsService = settingsService;
            _settings = settings;
            _logService = logService;
            _adbService = adbService;
            _wirelessAdbService = wirelessAdbService;
            _scrcpyService = scrcpyService;
            _singleWindowService = singleWindowService;
            _screenOffService = screenOffService;
            _deviceMonitor = deviceMonitor;
            _orchestrator = orchestrator;
            _captureCoordinator = captureCoordinator;
            _autoHideService = autoHideService;
            _environmentCheckService = environmentCheckService;
            _keyMappingService = keyMappingService;
            _isAutoRun = isAutoRun;
            _lastDeviceState = DeviceState.Disconnected();
            _selectedMode = 0;
            _theme = ThemeColors.Use(_settings.Theme);

            Text = LocalizationService.Get("App.Name");
            Icon = AppIconProvider.Current;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = _theme.WindowBackground;
            Font = UiFonts.Create(9.5F);
            ClientSize = new Size(920, 696);
            MinimumSize = Size;
            AutoScroll = false;

            _pageTitle = new Label
            {
                AutoSize = true,
                Font = UiFonts.Create(22F, FontStyle.Bold),
                ForeColor = _theme.TextPrimary,
                Location = new Point(32, 28),
                Text = LocalizationService.Get("App.Name")
            };
            Controls.Add(_pageTitle);

            _indicatorDot = new StatusRing
            {
                Location = new Point(33, 91)
            };
            _indicatorStatus = new Label
            {
                AutoSize = false,
                Font = UiFonts.Create(15F, FontStyle.Bold),
                ForeColor = _theme.TextPrimary,
                Location = new Point(66, 90),
                Size = new Size(240, 31),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _indicatorDetail = new Label
            {
                AutoEllipsis = true,
                ForeColor = _theme.TextTertiary,
                Location = new Point(35, 130),
                Size = new Size(570, 22)
            };
            _deviceInfoLabel = new Label
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

            _adbStatusValue = new Label { Visible = false };
            _scrcpyStatusValue = new Label { Visible = false };
            _dexStatusValue = new Label { Visible = false };
            _deviceStatusValue = new Label { Visible = false };
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
                320, 7680, 285, 263, 55, false);
            _heightBox = CreateCustomNumber(
                240, 4320, 395, 263, 55, false);
            _dpiBox = CreateCustomNumber(
                80, 640, 490, 263, 90, true);
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
            _reuseDisplayBox = CreateOption(LocalizationService.Get("Main.ReuseDisplay"), 392, 429);
            _flexDisplayBox = CreateOption(
                LocalizationService.Get("Main.FlexDisplay"),
                392,
                429);
            _flexDisplayBox.Visible = false;
            _stayAwakeBox = CreateOption(
                LocalizationService.Get("Main.StayAwake"),
                392,
                463);

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

            _additionalArgumentsBox = CreateCustomText(32, 577, 440);
            _additionalArgumentsBox.Visible = false;
            _advancedToggle = new LinkLabel
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
            _applySettingsLink = new LinkLabel
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
            Controls.Add(_reuseDisplayBox);
            Controls.Add(_flexDisplayBox);
            Controls.Add(_stayAwakeBox);
            Controls.Add(_startAppBox);
            Controls.Add(_loadAppsButton);
            Controls.Add(_additionalArgumentsBox);
            Controls.Add(_startButton);
            Controls.Add(_stopButton);
            Controls.Add(_applySettingsLink);
            _modeHintLabel = new Label
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

            Shown += MainForm_Shown;
            FormClosing += MainForm_FormClosing;
            FormClosed += MainForm_FormClosed;
            _deviceMonitor.StateChanged += DeviceMonitor_StateChanged;
            _deviceMonitor.DeviceConnected += DeviceMonitor_DeviceConnected;
            _deviceMonitor.DeviceDisconnected += DeviceMonitor_DeviceDisconnected;
            _scrcpyService.RunningChanged += ScrcpyService_RunningChanged;
            _singleWindowService.RunningChanged +=
                SingleWindowService_RunningChanged;
            _captureCoordinator.ExitHotkeyPressed += CaptureCoordinator_ExitHotkeyPressed;
            _autoHideService.IdleHideRequested += AutoHideService_IdleHideRequested;
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

            await InitializeAdbAndMonitorAsync();
            if (_isAutoRun && _settings.Features.StartMinimizedToTray)
                BeginInvoke((Action)HideToTray);
        }

        private async Task InitializeAdbAndMonitorAsync()
        {
            _adbStatusValue.Text =
                LocalizationService.Get("Status.Initializing");
            SetConnectionIndicator(
                Color.DarkOrange,
                LocalizationService.Get("Main.Waiting"),
                LocalizationService.Get("Main.PreparingAdb"));
            try
            {
                await Task.Run(delegate
                {
                    _adbService.LogStartupDiagnostics();
                    _adbService.StartServer();
                    if (_wirelessAdbService.IsWirelessMode)
                    {
                        _wirelessAdbService.TryReconnect(true);
                        return;
                    }
                    var devices = _adbService.GetDevices();
                    _wirelessAdbService.SelectPreferredDevice(devices);
                    if (_settings.Features.ScrcpyWakeUpMode == ScrcpyWakeUpMode.AlwaysOnStartup)
                    {
                        _adbService.WakeUp(delegate { return _scrcpyService.RunWakeUp(_settings.Timing.AdbWakeUpDelayMs); });
                    }
                    else
                    {
                        if (_settings.Features.ScrcpyWakeUpMode == ScrcpyWakeUpMode.OnAdbFailure && !_adbService.IsAuthorizedDeviceConnected())
                        {
                            _adbService.WakeUp(delegate { return _scrcpyService.RunWakeUp(_settings.Timing.AdbWakeUpDelayMs); });
                        }
                    }
                });
                _adbStatusValue.Text =
                    LocalizationService.Get("Status.Ready");
                _connectionError = null;
                SetConnectionIndicator(
                    Color.DarkOrange,
                    LocalizationService.Get("Main.Waiting"),
                    LocalizationService.Get("Main.WaitingPhone"));
            }
            catch (Exception ex)
            {
                _adbStatusValue.Text =
                    LocalizationService.Get("Status.Error");
                _logService.Error(
                    LocalizationService.Get("Log.Main.AdbInitFailed"),
                    ex);
                _connectionError = LocalizationService.Format(
                    "Error.AdbInit",
                    ex.Message);
                SetConnectionIndicator(
                    Color.Firebrick,
                    LocalizationService.Get("Status.Error"),
                    _connectionError);
            }
            finally { _deviceMonitor.Start(); }
        }

        private async void StartButton_Click(object sender, EventArgs e)
        {
            if (_selectedMode == 0)
                await StartDexAsync();
            else
                await StartSingleWindowAsync(_selectedMode);
        }

        private async void StopButton_Click(object sender, EventArgs e)
        {
            if (_selectedMode == 0)
                await StopDexAsync();
            else
                await StopSingleWindowAsync(_selectedMode);
        }

        private async Task StartDexAsync()
        {
            try
            {
                if (_selectedMode == 0) ApplyRunSettings(false);
            }
            catch (Exception ex)
            {
                ShowError(
                    LocalizationService.Get(
                        "Error.ApplyLaunchSettings"),
                    ex);
                return;
            }

            _connectionError = null;
            SetOperationState(
                true,
                LocalizationService.Get("Status.Starting"));
            SetConnectionIndicator(
                Color.DarkOrange,
                LocalizationService.Get("Main.DexStarting"),
                LocalizationService.Get("Main.DexPreparing"));
            try
            {
                await _orchestrator.StartAsync();
                _modeSettingsDirty[0] = false;
            }
            catch (Exception ex)
            {
                ShowError(
                    LocalizationService.Get("Error.StartDex"),
                    ex);
            }
            finally { UpdateRunningState(); }
        }

        private async Task StopDexAsync()
        {
            _connectionError = null;
            SetOperationState(
                true,
                LocalizationService.Get("Status.Stopping"));
            SetConnectionIndicator(
                Color.DarkOrange,
                LocalizationService.Get("Main.DexStopping"),
                LocalizationService.Get("Main.DexCleaning"));
            try { await _orchestrator.StopAsync(); }
            catch (Exception ex)
            {
                ShowError(
                    LocalizationService.Get("Error.StopDex"),
                    ex);
            }
            finally { UpdateRunningState(); }
        }

        private async Task StartSingleWindowAsync(int slot)
        {
            try { ApplyRunSettings(false); }
            catch (Exception ex)
            {
                ShowError(
                    LocalizationService.Get(
                        "Error.ApplySingleSettings"),
                    ex);
                return;
            }

            _connectionError = null;
            SetOperationState(
                true,
                LocalizationService.Get("Status.Starting"));
            SetConnectionIndicator(
                Color.DarkOrange,
                LocalizationService.Format(
                    "Main.SingleStarting",
                    slot),
                LocalizationService.Get("Main.SinglePreparing"));
            try
            {
                var settings = GetSingleWindowSettings(slot);
                await Task.Run(delegate
                {
                    _singleWindowService.Start(slot, settings);
                });
                _modeSettingsDirty[slot] = false;
            }
            catch (Exception ex)
            {
                ShowError(
                    LocalizationService.Format(
                        "Error.StartSingle",
                        slot),
                    ex);
            }
            finally
            {
                UpdateRunningState();
            }
        }

        private async Task StopSingleWindowAsync(int slot)
        {
            _connectionError = null;
            SetOperationState(
                true,
                LocalizationService.Get("Status.Stopping"));
            SetConnectionIndicator(
                Color.DarkOrange,
                LocalizationService.Format(
                    "Main.SingleStopping",
                    slot),
                LocalizationService.Get("Main.SingleCleaning"));
            try
            {
                await Task.Run(delegate
                {
                    _singleWindowService.Stop(slot);
                });
            }
            catch (Exception ex)
            {
                ShowError(
                    LocalizationService.Format(
                        "Error.StopSingle",
                        slot),
                    ex);
            }
            finally
            {
                UpdateRunningState();
            }
        }

        private void DeviceMonitor_StateChanged(object sender, DeviceStateChangedEventArgs e)
        {
            RunOnUi(delegate
            {
                _lastDeviceState = e.Current;
                _adbStatusValue.Text =
                    e.Current.Status == AdbDeviceStatus.Unknown
                        ? LocalizationService.Get("Status.Idle")
                        : LocalizationService.Get("Status.Responding");
                _deviceStatusValue.Text = GetDeviceStatusText(e.Current);
                _deviceInfoLabel.Text = e.Current.Status == AdbDeviceStatus.Device
                    ? LocalizationService.Format(
                        "Main.ConnectedDevice",
                        AdbService.IsTcpIpSerial(e.Current.Serial)
                            ? "Wi-Fi"
                            : "USB",
                        e.Current.Serial)
                    : LocalizationService.Get("Main.WaitingPhone");
                if (e.Current.Status == AdbDeviceStatus.Device)
                    UpdateDeviceStayAwakeState();
                else
                {
                    _lastAppliedStayAwakeState = null;
                    System.Threading.Interlocked.Increment(
                        ref _screenOffReapplyGeneration);
                }
                if (!IsSelectedModeRunning())
                    UpdateIndicatorForDevice(e.Current);
            });
        }

        private void DeviceMonitor_DeviceConnected(object sender, DeviceStateChangedEventArgs e)
        {
            if (_settings.Features.AutoStartDexOnDeviceConnected)
                RunOnUi(async delegate { await StartDexAsync(); });
        }

        private void DeviceMonitor_DeviceDisconnected(object sender, DeviceStateChangedEventArgs e)
        {
            System.Threading.Interlocked.Increment(
                ref _screenOffReapplyGeneration);
            if (_orchestrator.IsRunning)
                RunOnUi(async delegate { await StopDexAsync(); });
            if (IsAnySingleWindowRunning())
                Task.Run(delegate { _singleWindowService.StopAll(); });
        }

        private void ScrcpyService_RunningChanged(object sender, EventArgs e)
        {
            RunOnUi(HandleScrcpyRunningChanged);
        }

        private void SingleWindowService_RunningChanged(object sender, EventArgs e)
        {
            RunOnUi(HandleScrcpyRunningChanged);
        }

        private void HandleScrcpyRunningChanged()
        {
            var previousCount = _lastManagedScrcpyCount;
            var currentCount = GetManagedScrcpyCount();
            _lastManagedScrcpyCount = currentCount;
            var generation = System.Threading.Interlocked.Increment(
                ref _screenOffReapplyGeneration);

            UpdateRunningState();
            UpdateDeviceStayAwakeState();
            UpdatePhoneScreenWakeSchedule();
            if (currentCount < previousCount &&
                ShouldReapplyScreenOff(generation))
            {
                ScheduleScreenOffReapply(generation);
            }
        }

        private int GetManagedScrcpyCount()
        {
            return (_scrcpyService.IsRunning ? 1 : 0) +
                _singleWindowService.RunningCount;
        }

        private bool IsScreenOffRequested()
        {
            return _scrcpyService.IsScreenOffRequested ||
                _singleWindowService.IsScreenOffRequested;
        }

        private bool ShouldReapplyScreenOff(int generation)
        {
            return generation == System.Threading.Interlocked.CompareExchange(
                    ref _screenOffReapplyGeneration,
                    0,
                    0) &&
                System.Threading.Interlocked.CompareExchange(
                    ref _phoneScreenWakeSuppression,
                    0,
                    0) == 0 &&
                GetManagedScrcpyCount() > 0 &&
                IsScreenOffRequested() &&
                _adbService.IsAuthorizedDeviceConnected();
        }

        private void ScheduleScreenOffReapply(int generation)
        {
            Task.Run(delegate
            {
                System.Threading.Thread.Sleep(750);
                if (!ShouldReapplyScreenOff(generation)) return;

                try
                {
                    _screenOffService.Reapply(
                        delegate
                        {
                            return ShouldReapplyScreenOff(generation);
                        });
                }
                catch (Exception ex)
                {
                    _logService.Error(
                        LocalizationService.Get(
                            "Log.Main.ScreenOffReapplyFailed"),
                        ex);
                }
            });
        }

        private void UpdateDeviceStayAwakeState()
        {
            if (_lastDeviceState == null ||
                _lastDeviceState.Status != AdbDeviceStatus.Device)
            {
                _lastAppliedStayAwakeState = null;
                return;
            }

            var requested = _scrcpyService.IsStayAwakeRequested ||
                _singleWindowService.IsStayAwakeRequested;
            if (_lastAppliedStayAwakeState.HasValue &&
                _lastAppliedStayAwakeState.Value == requested)
            {
                return;
            }

            try
            {
                var result = _adbService.Shell(
                    "settings put global stay_on_while_plugged_in " +
                    (requested ? "7" : "0"));
                if (!result.IsSuccess)
                {
                    _logService.Warning(LocalizationService.Format(
                        "Log.Main.StayAwakeCommandFailed",
                        result.StandardError));
                    return;
                }

                _lastAppliedStayAwakeState = requested;
                _logService.Info(
                    requested
                        ? LocalizationService.Get(
                            "Log.Main.StayAwakeEnabled")
                        : LocalizationService.Get(
                            "Log.Main.StayAwakeDisabled"));
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.StayAwakeChangeFailed"),
                    ex);
            }
        }

        private void UpdatePhoneScreenWakeSchedule()
        {
            _phoneScreenWakeTimer.Stop();
            if (IsAnyScrcpyRunning() ||
                System.Threading.Interlocked.CompareExchange(
                    ref _phoneScreenWakeSuppression,
                    0,
                    0) > 0)
            {
                return;
            }

            _phoneScreenWakeTimer.Start();
        }

        private void PhoneScreenWakeTimer_Tick(object sender, EventArgs e)
        {
            _phoneScreenWakeTimer.Stop();
            if (IsAnyScrcpyRunning() ||
                System.Threading.Interlocked.CompareExchange(
                    ref _phoneScreenWakeSuppression,
                    0,
                    0) > 0 ||
                !_adbService.IsAuthorizedDeviceConnected())
            {
                return;
            }

            Task.Run((Action)WakePhoneScreen);
        }

        private void WakePhoneScreen()
        {
            try
            {
                if (_settings.Features.DisableStayAwakeOnStop)
                {
                    _adbService.Shell(
                        "settings put global stay_on_while_plugged_in 0");
                }
                var result = _adbService.Shell("input keyevent 224");
                if (result.IsSuccess)
                    _logService.Info(LocalizationService.Get(
                        "Log.Main.PhoneScreenWoken"));
                else
                    _logService.Warning(LocalizationService.Format(
                        "Log.Main.PhoneScreenWakeCommandFailed",
                        result.StandardError));
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.PhoneScreenWakeFailed"),
                    ex);
            }
        }

        private void BeginPhoneScreenWakeSuppression()
        {
            System.Threading.Interlocked.Increment(
                ref _phoneScreenWakeSuppression);
            System.Threading.Interlocked.Increment(
                ref _screenOffReapplyGeneration);
            _phoneScreenWakeTimer.Stop();
        }

        private void EndPhoneScreenWakeSuppression()
        {
            if (System.Threading.Interlocked.Decrement(
                ref _phoneScreenWakeSuppression) < 0)
            {
                System.Threading.Interlocked.Exchange(
                    ref _phoneScreenWakeSuppression,
                    0);
            }
            var generation = System.Threading.Interlocked.Increment(
                ref _screenOffReapplyGeneration);
            if (ShouldReapplyScreenOff(generation))
                ScheduleScreenOffReapply(generation);
            UpdatePhoneScreenWakeSchedule();
        }
        private void CaptureCoordinator_ExitHotkeyPressed(object sender, EventArgs e) { RunOnUi(ExitApplication); }
        private void AutoHideService_IdleHideRequested(object sender, EventArgs e) { RunOnUi(HideApplicationForIdle); }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _deviceMonitor.Dispose();
            _captureCoordinator.Dispose();
            _autoHideService.Dispose();
            _keyMappingService.Dispose();
            _phoneScreenWakeTimer.Dispose();
            _screenOffService.Dispose();
            _singleWindowService.Dispose();
            _scrcpyService.Dispose();
            _trayService.Dispose();
            if (_logForm != null) _logForm.Dispose();
            if (_settingsForm != null) _settingsForm.Dispose();
            if (_environmentCheckForm != null) _environmentCheckForm.Dispose();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_allowExit) return;
            e.Cancel = true;
            HideToTray();
        }

        private void UpdateRunningState()
        {
            var running = IsSelectedModeRunning();
            _scrcpyStatusValue.Text = running
                ? LocalizationService.Get("Status.Running")
                : LocalizationService.Get("Status.Stopped");
            _dexStatusValue.Text = running
                ? LocalizationService.Get("Status.Running")
                : LocalizationService.Get("Status.Idle");
            _startButton.Enabled = !running;
            _stopButton.Enabled = running;
            _startButton.Visible = !running;
            _stopButton.Visible = running;
            UpdateApplySettingsLink();
            if (!string.IsNullOrWhiteSpace(_connectionError))
            {
                SetConnectionIndicator(
                    Color.Firebrick,
                    LocalizationService.Get("Status.Error"),
                    _connectionError);
                return;
            }
            if (running && _selectedMode == 0)
                SetConnectionIndicator(
                    Color.ForestGreen,
                    LocalizationService.Get("Main.DexRunning"),
                    LocalizationService.Get("Main.DexRunningDetail"));
            else if (running)
                SetConnectionIndicator(
                    Color.ForestGreen,
                    LocalizationService.Format(
                        "Main.SingleRunning",
                        _selectedMode),
                    GetSingleWindowStatusDetail(_selectedMode));
            else if (_selectedMode > 0)
                UpdateSingleWindowIndicator(_selectedMode);
            else
                UpdateIndicatorForDevice(_lastDeviceState);
        }

        private void SetOperationState(bool operationRunning, string status)
        {
            var running = IsSelectedModeRunning();
            _startButton.Visible = !running;
            _stopButton.Visible = running;
            _startButton.Enabled = !operationRunning && !running;
            _stopButton.Enabled = !operationRunning && running;
            _applySettingsLink.Enabled = !operationRunning;
            _dexStatusValue.Text = status;
        }

        private void UpdateIndicatorForDevice(DeviceState state)
        {
            if (!string.IsNullOrWhiteSpace(_connectionError))
            {
                SetConnectionIndicator(
                    Color.Firebrick,
                    LocalizationService.Get("Status.Error"),
                    _connectionError);
                return;
            }
            if (state != null && state.Status == AdbDeviceStatus.Device)
            {
                SetConnectionIndicator(
                    Color.ForestGreen,
                    LocalizationService.Get("Main.PhoneConnected"),
                    LocalizationService.Get("Main.WaitingDex"));
                return;
            }
            if (state != null && state.Status == AdbDeviceStatus.Unauthorized)
            {
                SetConnectionIndicator(
                    Color.DarkOrange,
                    LocalizationService.Get("Main.AuthorizationRequired"),
                    LocalizationService.Get("Main.AuthorizationDetail"));
                return;
            }
            if (state != null && state.Status == AdbDeviceStatus.Offline)
            {
                SetConnectionIndicator(
                    Color.Firebrick,
                    LocalizationService.Get("Main.DeviceOffline"),
                    LocalizationService.Get("Main.DeviceOfflineDetail"));
                return;
            }
            SetConnectionIndicator(
                Color.DarkOrange,
                LocalizationService.Get("Main.Waiting"),
                LocalizationService.Get("Main.WaitingPhone"));
        }

        private void SetConnectionIndicator(Color color, string status, string detail)
        {
            _indicatorDot.StatusColor = color;
            var argb = color.ToArgb();
            _indicatorDot.Complete =
                argb == Color.ForestGreen.ToArgb() ||
                argb == Color.Green.ToArgb() ||
                argb == Color.DarkGreen.ToArgb();
            _indicatorStatus.Text = status;
            var device = _lastDeviceState;
            _indicatorDetail.Text =
                device != null &&
                device.Status == AdbDeviceStatus.Device &&
                !string.IsNullOrWhiteSpace(_deviceInfoLabel.Text)
                    ? detail + "  ·  " + _deviceInfoLabel.Text
                    : detail;
        }

        private void ShowError(string message, Exception exception)
        {
            _logService.Error(message, exception);
            _connectionError = message + ": " + exception.Message;
            SetConnectionIndicator(
                Color.Firebrick,
                LocalizationService.Get("Status.Error"),
                _connectionError);
            MessageBox.Show(
                this,
                message + Environment.NewLine +
                    Environment.NewLine + exception.Message,
                LocalizationService.Get("App.Name"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private void ResolutionBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingRunSettings) return;

            if (_resolutionSelectionInitialized && _resolutionWasCustom)
                StoreCurrentCustomResolution();

            ApplyResolutionSelection();
        }

        private void ApplyResolutionSelection()
        {
            var preset = _resolutionBox.SelectedItem as ResolutionPreset;
            var custom = preset == null || preset.Width == 0;
            LayoutResolutionControls(custom);
            _widthBox.Enabled = custom;
            _heightBox.Enabled = custom;
            _widthBox.Visible = custom;
            _heightBox.Visible = custom;
            _widthLabel.Visible = custom;
            _heightLabel.Visible = custom;
            if (custom)
            {
                _widthBox.Value = Clamp(
                    GetCurrentCustomWidth(),
                    _widthBox);
                _heightBox.Value = Clamp(
                    GetCurrentCustomHeight(),
                    _heightBox);
            }
            else
            {
                _widthBox.Value = preset.Width;
                _heightBox.Value = preset.Height;
            }

            _resolutionWasCustom = custom;
            _resolutionSelectionInitialized = true;
        }

        private void LayoutResolutionControls(bool custom)
        {
            if (!custom)
            {
                _resolutionBox.Width = 304;
                return;
            }

            const int fieldTop = 72;
            const int labelGap = 6;
            const int groupGap = 10;
            const int dpiGap = 12;

            _resolutionBox.Width = 110;
            _heightBox.Left = _dpiBox.Left - dpiGap - _heightBox.Width;
            var heightLabelWidth = MeasureInlineLabel(_heightLabel);
            _heightLabel.Left =
                _heightBox.Left - labelGap - heightLabelWidth;
            _widthBox.Left =
                _heightLabel.Left - groupGap - _widthBox.Width;
            var widthLabelWidth = MeasureInlineLabel(_widthLabel);
            _widthLabel.Left =
                _widthBox.Left - labelGap - widthLabelWidth;

            _widthBox.Top = fieldTop;
            _heightBox.Top = fieldTop;
            _widthLabel.Top = GetInlineLabelTop(_widthLabel, fieldTop);
            _heightLabel.Top = GetInlineLabelTop(_heightLabel, fieldTop);
        }

        private static int MeasureInlineLabel(Label label)
        {
            return TextRenderer.MeasureText(
                label.Text,
                label.Font,
                Size.Empty,
                TextFormatFlags.NoPadding).Width;
        }

        private static int GetInlineLabelTop(
            Label label,
            int fieldTop)
        {
            var labelHeight = TextRenderer.MeasureText(
                label.Text,
                label.Font,
                Size.Empty,
                TextFormatFlags.NoPadding).Height;
            return fieldTop + (32 - labelHeight) / 2 + 1;
        }

        private void LoadRunSettings()
        {
            int width;
            int height;
            int dpi;
            string bitRate;
            int maxFps;
            bool turnScreenOff;
            bool stayAwake;
            bool useHidKeyboard;
            bool useHidMouse;
            bool forceStopStartApp;
            bool flexDisplay;
            string startAppPackage;
            string startAppName;
            string additionalArguments;

            _loadingRunSettings = true;
            _resolutionSelectionInitialized = false;
            if (_selectedMode == 0)
            {
                width = _settings.VirtualDisplay.Width;
                height = _settings.VirtualDisplay.Height;
                dpi = _settings.VirtualDisplay.Dpi;
                bitRate = _settings.Scrcpy.BitRate;
                maxFps = _settings.Scrcpy.MaxFps;
                turnScreenOff = _settings.Scrcpy.TurnScreenOff;
                stayAwake = _settings.Scrcpy.StayAwake;
                useHidKeyboard = _settings.Scrcpy.UseHidKeyboard;
                useHidMouse = _settings.Scrcpy.UseHidMouse;
                forceStopStartApp = _settings.Scrcpy.ForceStopStartApp;
                flexDisplay = false;
                startAppPackage = _settings.Scrcpy.StartAppPackage;
                startAppName = _settings.Scrcpy.StartAppName;
                additionalArguments = _settings.Scrcpy.AdditionalArguments;
                _reuseDisplayBox.Checked =
                    _settings.VirtualDisplay.ReuseExistingDisplay;
            }
            else
            {
                var slot = GetSingleWindowSettings(_selectedMode);
                width = slot.Width;
                height = slot.Height;
                dpi = slot.Dpi;
                bitRate = slot.BitRate;
                maxFps = slot.MaxFps;
                turnScreenOff = slot.TurnScreenOff;
                stayAwake = slot.StayAwake;
                useHidKeyboard = slot.UseHidKeyboard;
                useHidMouse = slot.UseHidMouse;
                forceStopStartApp = slot.ForceStopStartApp;
                flexDisplay = slot.FlexDisplay;
                startAppPackage = slot.StartAppPackage;
                startAppName = slot.StartAppName;
                additionalArguments = slot.AdditionalArguments;
            }

            _widthBox.Value = Clamp(width, _widthBox);
            _heightBox.Value = Clamp(height, _heightBox);
            _dpiBox.Value = Clamp(dpi, _dpiBox);
            _bitRateBox.Value = Clamp(
                ParseBitRateNumber(bitRate),
                _bitRateBox);
            _maxFpsBox.SelectedItem = maxFps == 30 ? 30 : 60;
            _turnScreenOffBox.Checked = turnScreenOff;
            _stayAwakeBox.Checked = stayAwake;
            _useHidKeyboardBox.Checked = useHidKeyboard;
            _useHidMouseBox.Checked = useHidMouse;
            _forceStopAppBox.Checked = forceStopStartApp;
            _flexDisplayBox.Checked = flexDisplay;
            _additionalArgumentsBox.Text = additionalArguments;
            SetSelectedAppPackage(startAppPackage, startAppName);
            _resolutionBox.SelectedIndex = FindResolutionPresetIndex(
                width,
                height);
            ApplyResolutionSelection();
            _loadingRunSettings = false;
            UpdateApplySettingsLink();
        }

        private int FindResolutionPresetIndex(int width, int height)
        {
            for (var index = 0; index < _resolutionBox.Items.Count; index++)
            {
                var preset = _resolutionBox.Items[index] as ResolutionPreset;
                if (preset != null && preset.Width == width && preset.Height == height)
                    return index;
            }

            return _resolutionBox.Items.Count - 1;
        }

        private async void ApplyRunSettingsButton_Click(object sender, EventArgs e)
        {
            try
            {
                ApplyRunSettings(false);
                if (_selectedMode > 0)
                {
                    await ApplySingleWindowSettingsAsync(_selectedMode);
                    return;
                }
                if (!_adbService.IsAuthorizedDeviceConnected())
                {
                    _logService.Info(LocalizationService.Get(
                        "Log.Main.SettingsDeferredNoDevice"));
                    MessageBox.Show(
                        this,
                        LocalizationService.Get("Main.ApplyNoDevice"),
                        LocalizationService.Get("App.Name"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    _modeSettingsDirty[0] = false;
                    return;
                }

                _connectionError = null;
                SetOperationState(
                    true,
                    LocalizationService.Get("Status.Applying"));
                SetConnectionIndicator(
                    Color.DarkOrange,
                    LocalizationService.Get("Main.ApplyStatus"),
                    LocalizationService.Get("Main.ApplyRestartDetail"));

                bool applied;
                BeginPhoneScreenWakeSuppression();
                try
                {
                    applied = await _orchestrator.ApplyRuntimeSettingsAsync();
                }
                finally
                {
                    EndPhoneScreenWakeSuppression();
                }
                if (!applied)
                {
                    MessageBox.Show(
                        this,
                        LocalizationService.Get("Main.ApplyDeferred"),
                        LocalizationService.Get("App.Name"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                UpdateRunningState();
                _modeSettingsDirty[0] = false;
                MessageBox.Show(
                    this,
                    LocalizationService.Get("Main.ApplySucceeded"),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.SettingsImmediateApplyFailed"),
                    ex);
                _connectionError = LocalizationService.Format(
                    "Main.ApplyFailedShort",
                    ex.Message);
                SetConnectionIndicator(
                    Color.Firebrick,
                    LocalizationService.Get("Status.Error"),
                    _connectionError);
                MessageBox.Show(
                    this,
                    LocalizationService.Format(
                        "Main.ApplyFailed",
                        Environment.NewLine,
                        ex.Message),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            finally
            {
                UpdateRunningState();
            }
        }

        private async Task ApplySingleWindowSettingsAsync(int slot)
        {
            if (!_adbService.IsAuthorizedDeviceConnected())
            {
                MessageBox.Show(
                    this,
                    LocalizationService.Format(
                        "Main.SingleSavedNoDevice",
                        slot,
                        Environment.NewLine),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                _modeSettingsDirty[slot] = false;
                return;
            }

            if (!_singleWindowService.IsRunning(slot))
            {
                MessageBox.Show(
                    this,
                    LocalizationService.Format(
                        "Main.SingleSaved",
                        slot),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                _modeSettingsDirty[slot] = false;
                return;
            }

            SetOperationState(
                true,
                LocalizationService.Get("Status.Applying"));
            SetConnectionIndicator(
                Color.DarkOrange,
                LocalizationService.Format(
                    "Main.SingleApplying",
                    slot),
                LocalizationService.Get("Main.SingleRestartDetail"));
            var settings = GetSingleWindowSettings(slot);
            BeginPhoneScreenWakeSuppression();
            try
            {
                await Task.Run(delegate
                {
                    _singleWindowService.Restart(slot, settings);
                });
            }
            finally
            {
                EndPhoneScreenWakeSuppression();
            }
            UpdateRunningState();
            _modeSettingsDirty[slot] = false;
            MessageBox.Show(
                this,
                LocalizationService.Format(
                    "Main.SingleApplied",
                    slot),
                LocalizationService.Get("App.Name"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private async void LoadAppsButton_Click(object sender, EventArgs e)
        {
            if (_loadAppsButton.Enabled == false) return;

            _loadAppsButton.Enabled = false;
                _loadAppsButton.Text = LocalizationService.Get("Main.Loading");
            try
            {
                var apps = await Task.Run(delegate { return _scrcpyService.ListApps(); });
                var selectedPackage = GetSelectedAppPackage();
                var selectedName = GetSelectedAppName(selectedPackage);

                _startAppBox.SelectedIndex = -1;
                _startAppBox.Items.Clear();
                AddNoStartAppItem();
                foreach (var app in apps) _startAppBox.Items.Add(app);

                var selected = false;
                if (string.IsNullOrWhiteSpace(selectedPackage))
                {
                    _startAppBox.SelectedIndex = 0;
                    selected = true;
                }
                foreach (var app in apps)
                {
                    if (!string.Equals(
                        app.PackageName,
                        selectedPackage,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    _startAppBox.SelectedItem = app;
                    selected = true;
                    break;
                }

                if (!selected)
                    SetSelectedAppPackage(selectedPackage, selectedName);
                SaveSelectedAppIdentity();
                _logService.Info(LocalizationService.Get(
                    "Log.Main.AppListDisplayed"));
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.AppListLoadFailed"),
                    ex);
                MessageBox.Show(
                    this,
                    LocalizationService.Format(
                        "Main.LoadAppsFailed",
                        Environment.NewLine,
                        ex.Message),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            finally
            {
                _loadAppsButton.Text = LocalizationService.Get("Main.LoadApps");
                _loadAppsButton.Enabled = true;
            }
        }

        private void ApplyRunSettings(bool showMessage)
        {
            var bitRate = ((int)_bitRateBox.Value).ToString() + "M";

            if (_selectedMode == 0)
            {
                _settings.VirtualDisplay.Width = (int)_widthBox.Value;
                _settings.VirtualDisplay.Height = (int)_heightBox.Value;
                if (IsCustomResolutionSelected())
                {
                    _settings.VirtualDisplay.CustomWidth =
                        (int)_widthBox.Value;
                    _settings.VirtualDisplay.CustomHeight =
                        (int)_heightBox.Value;
                }
                _settings.VirtualDisplay.Dpi = (int)_dpiBox.Value;
                _settings.VirtualDisplay.ReuseExistingDisplay =
                    _reuseDisplayBox.Checked;
                _settings.Scrcpy.BitRate = bitRate;
                _settings.Scrcpy.MaxFps = GetSelectedMaxFps();
                _settings.Scrcpy.TurnScreenOff = _turnScreenOffBox.Checked;
                _settings.Scrcpy.StayAwake = _stayAwakeBox.Checked;
                _settings.Scrcpy.UseHidKeyboard = _useHidKeyboardBox.Checked;
                _settings.Scrcpy.UseHidMouse = _useHidMouseBox.Checked;
                _settings.Scrcpy.ForceStopStartApp = _forceStopAppBox.Checked;
                var selectedPackage = GetSelectedAppPackage();
                var selectedName = GetSelectedAppName(selectedPackage);
                _settings.Scrcpy.StartAppPackage = selectedPackage;
                _settings.Scrcpy.StartAppName = selectedName;
                _settings.Scrcpy.AdditionalArguments =
                    _additionalArgumentsBox.Text.Trim();
            }
            else
            {
                var slot = GetSingleWindowSettings(_selectedMode);
                slot.Width = (int)_widthBox.Value;
                slot.Height = (int)_heightBox.Value;
                if (IsCustomResolutionSelected())
                {
                    slot.CustomWidth = (int)_widthBox.Value;
                    slot.CustomHeight = (int)_heightBox.Value;
                }
                slot.Dpi = (int)_dpiBox.Value;
                slot.BitRate = bitRate;
                slot.MaxFps = GetSelectedMaxFps();
                slot.TurnScreenOff = _turnScreenOffBox.Checked;
                slot.StayAwake = _stayAwakeBox.Checked;
                slot.UseHidKeyboard = _useHidKeyboardBox.Checked;
                slot.UseHidMouse = _useHidMouseBox.Checked;
                slot.ForceStopStartApp = _forceStopAppBox.Checked;
                slot.FlexDisplay = _flexDisplayBox.Checked;
                var selectedPackage = GetSelectedAppPackage();
                var selectedName = GetSelectedAppName(selectedPackage);
                slot.StartAppPackage = selectedPackage;
                slot.StartAppName = selectedName;
                slot.AdditionalArguments =
                    _additionalArgumentsBox.Text.Trim();
            }
            _settingsService.Save(_settings);
            _logService.Info(
                _selectedMode == 0
                    ? LocalizationService.Get(
                        "Log.Main.DexSettingsSaved")
                    : LocalizationService.Format(
                        "Log.Main.SingleWindowSettingsSaved",
                        _selectedMode));
            if (showMessage)
            {
                MessageBox.Show(
                    this,
                    LocalizationService.Get("Main.ApplyDeferred"),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private string GetSelectedAppPackage()
        {
            var app = _startAppBox.SelectedItem as ScrcpyAppInfo;
            if (app != null) return app.PackageName ?? string.Empty;
            return string.Empty;
        }

        private string GetSelectedAppName(string packageName)
        {
            var app = _startAppBox.SelectedItem as ScrcpyAppInfo;
            if (app != null && !string.IsNullOrWhiteSpace(app.PackageName))
                return app.Name ?? app.PackageName;

            if (_selectedMode == 0)
            {
                return string.Equals(
                    _settings.Scrcpy.StartAppPackage,
                    packageName,
                    StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(_settings.Scrcpy.StartAppName)
                    ? _settings.Scrcpy.StartAppName
                    : packageName;
            }

            var slot = GetSingleWindowSettings(_selectedMode);
            return string.Equals(
                slot.StartAppPackage,
                packageName,
                StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(slot.StartAppName)
                ? slot.StartAppName
                : packageName;
        }

        private int GetSelectedMaxFps()
        {
            return _maxFpsBox.SelectedItem is int
                ? (int)_maxFpsBox.SelectedItem
                : 60;
        }

        private static int ParseBitRateNumber(string value)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (normalized.EndsWith(
                "M",
                StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(
                    0,
                    normalized.Length - 1);
            }

            int result;
            return int.TryParse(normalized, out result) && result > 0
                ? result
                : 20;
        }

        private void SetSelectedAppPackage(
            string packageName,
            string appName)
        {
            packageName = packageName ?? string.Empty;
            foreach (var item in _startAppBox.Items)
            {
                var app = item as ScrcpyAppInfo;
                if (app == null ||
                    !string.Equals(
                        app.PackageName ?? string.Empty,
                        packageName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(appName) &&
                    string.Equals(
                        app.Name,
                        app.PackageName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    app.Name = appName;
                }
                _startAppBox.SelectedItem = item;
                return;
            }

            if (string.IsNullOrWhiteSpace(packageName))
            {
                _startAppBox.SelectedIndex = 0;
                return;
            }

            var placeholder = new ScrcpyAppInfo
            {
                Name = string.IsNullOrWhiteSpace(appName)
                    ? packageName
                    : appName,
                PackageName = packageName
            };
            _startAppBox.Items.Add(placeholder);
            _startAppBox.SelectedItem = placeholder;
        }

        private void StartAppBox_SelectionChangeCommitted(
            object sender,
            EventArgs e)
        {
            SaveSelectedAppIdentity();
        }

        private void SaveSelectedAppIdentity()
        {
            var packageName = GetSelectedAppPackage();
            var appName = GetSelectedAppName(packageName);
            if (_selectedMode == 0)
            {
                _settings.Scrcpy.StartAppPackage = packageName;
                _settings.Scrcpy.StartAppName = appName;
            }
            else
            {
                var slot = GetSingleWindowSettings(_selectedMode);
                slot.StartAppPackage = packageName;
                slot.StartAppName = appName;
            }
            _settingsService.Save(_settings);
        }

        private void AddNoStartAppItem()
        {
            _startAppBox.Items.Add(new ScrcpyAppInfo
            {
                Name = NoStartAppText,
                PackageName = string.Empty
            });
            if (_startAppBox.SelectedIndex < 0)
                _startAppBox.SelectedIndex = 0;
        }

        private static CheckBox CreateOption(string text, int x, int y)
        {
            return new ThemedCheckBox
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(284, 30)
            };
        }

        private static ThemedSelectControl CreateCustomSelect(
            int x,
            int y,
            int width)
        {
            return new ThemedSelectControl
            {
                Location = new Point(x, y),
                Size = new Size(width, 32)
            };
        }

        private static ThemedNumberControl CreateCustomNumber(
            int min,
            int max,
            int x,
            int y,
            int width,
            bool showStepButtons)
        {
            var control = new ThemedNumberControl
            {
                Minimum = min,
                Maximum = max,
                Increment = 1,
                ShowStepButtons = showStepButtons,
                Location = new Point(x, y),
                Size = new Size(width, 32)
            };
            control.Value = min;
            return control;
        }

        private static ThemedTextControl CreateCustomText(
            int x,
            int y,
            int width)
        {
            return new ThemedTextControl
            {
                Location = new Point(x, y),
                Size = new Size(width, 32)
            };
        }

        private ThemedButton CreateThemedButton(
            string text,
            bool primary,
            int x,
            int y,
            int width)
        {
            return new ThemedButton
            {
                Text = text,
                Primary = primary,
                ForeColor = _theme.TextPrimary,
                Location = new Point(x, y),
                Size = new Size(width, 34)
            };
        }

        private void OffsetMainContent(int offsetX)
        {
            foreach (Control control in Controls)
            {
                control.Left += offsetX;
            }
        }

        private void ApplyDesignLayout()
        {
            foreach (Control control in Controls)
            {
                var divider = control as Panel;
                if (divider != null && divider.Height == 1)
                    divider.Visible = false;
            }

            _statusCard = CreateCard(new Point(220, 64), new Size(686, 84));
            _displayCard = CreateCard(new Point(220, 164), new Size(686, 182));
            _optionsCard = CreateCard(new Point(220, 362), new Size(686, 270));

            _pageTitle.Location = new Point(220, 14);

            MoveToCard(_indicatorDot, _statusCard, 18, 22);
            MoveToCard(_indicatorStatus, _statusCard, 70, 16);
            _indicatorStatus.Size = new Size(360, 28);
            MoveToCard(_indicatorDetail, _statusCard, 70, 48);
            _indicatorDetail.Size = new Size(580, 20);
            _deviceInfoLabel.Visible = false;

            MoveToCard(_displaySettingsTitle, _displayCard, 20, 13);
            MoveToCard(_resolutionLabel, _displayCard, 20, 51);
            MoveToCard(_resolutionBox, _displayCard, 20, 72);
            _resolutionBox.Size = new Size(304, 32);
            MoveToCard(_widthLabel, _displayCard, 158, 78);
            MoveToCard(_widthBox, _displayCard, 196, 72);
            _widthBox.Size = new Size(55, 32);
            MoveToCard(_heightLabel, _displayCard, 257, 78);
            MoveToCard(_heightBox, _displayCard, 305, 72);
            _heightBox.Size = new Size(55, 32);
            MoveToCard(_dpiLabel, _displayCard, 362, 51);
            MoveToCard(_dpiBox, _displayCard, 362, 72);
            _dpiBox.Size = new Size(304, 32);
            MoveToCard(_bitRateLabel, _displayCard, 20, 113);
            MoveToCard(_bitRateBox, _displayCard, 20, 134);
            _bitRateBox.Size = new Size(304, 32);
            MoveToCard(_maxFpsLabel, _displayCard, 362, 113);
            MoveToCard(_maxFpsBox, _displayCard, 362, 134);
            _maxFpsBox.Size = new Size(304, 32);

            MoveToCard(_optionsTitle, _optionsCard, 20, 13);
            MoveToCard(_turnScreenOffBox, _optionsCard, 20, 49);
            MoveToCard(_useHidKeyboardBox, _optionsCard, 20, 84);
            MoveToCard(_useHidMouseBox, _optionsCard, 20, 119);
            MoveToCard(_forceStopAppBox, _optionsCard, 362, 49);
            MoveToCard(_reuseDisplayBox, _optionsCard, 362, 84);
            MoveToCard(_flexDisplayBox, _optionsCard, 362, 84);
            MoveToCard(_stayAwakeBox, _optionsCard, 362, 119);
            foreach (var option in new[]
            {
                _turnScreenOffBox,
                _useHidKeyboardBox,
                _useHidMouseBox,
                _forceStopAppBox,
                _reuseDisplayBox,
                _flexDisplayBox,
                _stayAwakeBox
            })
            {
                option.Size = new Size(284, 30);
            }
            MoveToCard(_startAppLabel, _optionsCard, 20, 168);
            MoveToCard(_startAppBox, _optionsCard, 20, 189);
            _startAppBox.Size = new Size(470, 32);
            MoveToCard(_loadAppsButton, _optionsCard, 500, 189);
            _loadAppsButton.Size = new Size(146, 32);
            MoveToCard(_advancedToggle, _optionsCard, 20, 238);
            MoveToCard(_additionalArgumentsBox, _optionsCard, 190, 232);
            _additionalArgumentsBox.Size = new Size(456, 32);

            _startButton.Location = new Point(754, 646);
            _stopButton.Location = _startButton.Location;
            _applySettingsLink.Location = new Point(638, 655);
            _modeHintLabel.Visible = false;

            _sidebar.Location = new Point(14, 14);
            _sidebar.Size = new Size(188, 618);
            _sidebar.BringToFront();
        }

        private RoundedPanel CreateCard(Point location, Size size)
        {
            var card = new RoundedPanel
            {
                Location = location,
                Size = size,
                Radius = 14,
                FillColor = _theme.CardBackground,
                BorderColor = _theme.CardBorder
            };
            Controls.Add(card);
            card.SendToBack();
            return card;
        }

        private static void MoveToCard(
            Control control,
            Control card,
            int x,
            int y)
        {
            control.Parent = card;
            control.Location = new Point(x, y);
        }

        private void ApplyTheme()
        {
            BackColor = _theme.WindowBackground;
            _pageTitle.ForeColor = _theme.TextPrimary;
            _indicatorStatus.ForeColor = _theme.TextPrimary;
            _indicatorDetail.ForeColor = _theme.TextTertiary;
            _deviceInfoLabel.ForeColor = _theme.TextTertiary;

            ApplyCardTheme(_statusCard);
            ApplyCardTheme(_displayCard);
            ApplyCardTheme(_optionsCard);
            _displaySettingsTitle.ForeColor = _theme.TextSecondary;
            _optionsTitle.ForeColor = _theme.TextSecondary;

            foreach (var label in new[]
            {
                _resolutionLabel,
                _widthLabel,
                _heightLabel,
                _dpiLabel,
                _bitRateLabel,
                _maxFpsLabel,
                _startAppLabel
            })
            {
                label.ForeColor = _theme.TextTertiary;
                label.BackColor = _theme.CardBackground;
            }

            foreach (var option in new[]
            {
                _turnScreenOffBox,
                _useHidKeyboardBox,
                _useHidMouseBox,
                _forceStopAppBox,
                _reuseDisplayBox,
                _flexDisplayBox,
                _stayAwakeBox
            })
            {
                option.BackColor = _theme.CardBackground;
                option.ForeColor = _theme.TextPrimary;
            }

            foreach (var control in new Control[]
            {
                _resolutionBox,
                _widthBox,
                _heightBox,
                _dpiBox,
                _bitRateBox,
                _maxFpsBox,
                _startAppBox,
                _additionalArgumentsBox
            })
            {
                control.BackColor = _theme.CardSoft;
                control.ForeColor = _theme.TextPrimary;
            }

            _advancedToggle.BackColor = _theme.CardBackground;
            _advancedToggle.LinkColor = _theme.Accent;
            _advancedToggle.ActiveLinkColor = _theme.AccentHover;
            _applySettingsLink.LinkColor = _theme.Accent;
            _applySettingsLink.ActiveLinkColor = _theme.AccentHover;

            _sidebar.BackColor = _theme.WindowBackground;
            _sidebar.FillColor = _theme.NavigationBackground;
            _sidebar.BorderColor = _theme.CardBorder;
            foreach (Control control in _sidebar.Controls)
            {
                control.BackColor = _theme.NavigationBackground;
                var label = control as Label;
                if (label != null)
                    label.ForeColor = _theme.TextTertiary;
                var button = control as ThemedButton;
                if (button != null)
                    button.ForeColor = _theme.TextSecondary;
                control.Invalidate();
            }

            _indicatorDot.Invalidate();
            _resolutionBox.Invalidate();
            _widthBox.Invalidate();
            _heightBox.Invalidate();
            _dpiBox.Invalidate();
            _bitRateBox.Invalidate();
            _maxFpsBox.Invalidate();
            _startAppBox.Invalidate();
            _startButton.Invalidate();
            _stopButton.Invalidate();
            _loadAppsButton.Invalidate();
            Invalidate(true);
        }

        private void ApplyThemeSelection(AppTheme theme)
        {
            _theme = ThemeColors.Use(theme);
            ApplyTheme();
            if (_settingsForm != null &&
                !_settingsForm.IsDisposed)
            {
                _settingsForm.ApplyCurrentTheme();
            }
            if (_logForm != null && !_logForm.IsDisposed)
                _logForm.ApplyCurrentTheme();
            if (_environmentCheckForm != null &&
                !_environmentCheckForm.IsDisposed)
            {
                _environmentCheckForm.ApplyCurrentTheme();
            }
        }

        private void ApplyCardTheme(RoundedPanel card)
        {
            card.BackColor = _theme.WindowBackground;
            card.FillColor = _theme.CardBackground;
            card.BorderColor = _theme.CardBorder;
            foreach (Control control in card.Controls)
            {
                var label = control as Label;
                if (label != null)
                    label.BackColor = _theme.CardBackground;
            }
        }

        private void AddModeSidebar()
        {
            _sidebar = new RoundedPanel
            {
                Location = new Point(14, 14),
                Size = new Size(188, 587),
                Radius = 14,
                BackColor = _theme.NavigationBackground,
                FillColor = _theme.NavigationBackground,
                BorderColor = _theme.CardBorder
            };

            _sidebar.Controls.Add(new Label
            {
                AutoSize = true,
                Font = UiFonts.Create(9.5F, FontStyle.Bold),
                ForeColor = _theme.TextTertiary,
                BackColor = _theme.NavigationBackground,
                Location = new Point(20, 18),
                Text = LocalizationService.Get("Main.Mode")
            });

            _dexModeButton = CreateSidebarButton(
                LocalizationService.Get("Main.Dex"), 52, true);
            _dexModeButton.Click += delegate { SelectDexMode(); };
            _sidebar.Controls.Add(_dexModeButton);

            _singleModeButton1 = CreateSidebarButton(
                LocalizationService.Format("Main.SingleWindow", 1),
                94,
                false);
            _singleModeButton1.Click += delegate { SelectSingleWindowPreview(1); };
            _sidebar.Controls.Add(_singleModeButton1);

            _singleModeButton2 = CreateSidebarButton(
                LocalizationService.Format("Main.SingleWindow", 2),
                136,
                false);
            _singleModeButton2.Click += delegate { SelectSingleWindowPreview(2); };
            _sidebar.Controls.Add(_singleModeButton2);

            _singleModeButton3 = CreateSidebarButton(
                LocalizationService.Format("Main.SingleWindow", 3),
                178,
                false);
            _singleModeButton3.Click += delegate { SelectSingleWindowPreview(3); };
            _sidebar.Controls.Add(_singleModeButton3);

            _sidebar.Controls.Add(new Label
            {
                AutoEllipsis = true,
                ForeColor = _theme.TextTertiary,
                BackColor = _theme.NavigationBackground,
                Location = new Point(20, 238),
                Size = new Size(148, 70),
                Text = LocalizationService.Get("Main.SidebarHint")
            });

            var settingsButton = CreateSidebarButton(
                LocalizationService.Get("Main.Settings"),
                _sidebar.Height - 48,
                false,
                false);
            settingsButton.ShowSettingsIcon = true;
            settingsButton.TrailingText =
                "v" + Application.ProductVersion;
            settingsButton.Anchor =
                AnchorStyles.Left | AnchorStyles.Bottom;
            settingsButton.Click += delegate { ShowSettingsForm(); };
            _sidebar.Controls.Add(settingsButton);

            Controls.Add(_sidebar);
            _sidebar.BringToFront();
        }

        private ThemedButton CreateSidebarButton(
            string text,
            int y,
            bool selected,
            bool showDot = true)
        {
            return new ThemedButton
            {
                Text = text,
                Primary = selected,
                CornerRadius = 18,
                NavigationStyle = true,
                ShowNavigationDot = showDot,
                TabStop = false,
                Location = new Point(10, y),
                Size = new Size(168, 34),
                BackColor = _theme.NavigationBackground,
                ForeColor = _theme.TextSecondary
            };
        }

        private void SelectDexMode()
        {
            SaveCurrentModeBeforeSwitch();
            _selectedMode = 0;
            SetSelectedModeButton(0);
            _modeHintLabel.Text = LocalizationService.Get("Main.DexMode");
            _displaySettingsTitle.Text =
                LocalizationService.Get("Main.DisplaySettings.Dex");
            _startButton.Text = LocalizationService.Get("Main.StartDex");
            _stopButton.Text = LocalizationService.Get("Main.StopDex");
            _reuseDisplayBox.Visible = true;
            _reuseDisplayBox.Enabled = true;
            _flexDisplayBox.Visible = false;
            _flexDisplayBox.Enabled = false;
            LoadRunSettings();
            UpdateRunningState();
        }

        private void SelectSingleWindowPreview(int slot)
        {
            SaveCurrentModeBeforeSwitch();
            _selectedMode = slot;
            SetSelectedModeButton(slot);
            _modeHintLabel.Text =
                LocalizationService.Format("Main.SingleMode", slot);
            _displaySettingsTitle.Text =
                LocalizationService.Format(
                    "Main.DisplaySettings.Single",
                    slot);
            _startButton.Text = LocalizationService.Get("Main.StartSingle");
            _stopButton.Text = LocalizationService.Get("Main.StopSingle");
            _reuseDisplayBox.Visible = false;
            _reuseDisplayBox.Enabled = false;
            _flexDisplayBox.Visible = true;
            _flexDisplayBox.Enabled = true;
            LoadRunSettings();
            UpdateRunningState();
        }

        private void SaveCurrentModeBeforeSwitch()
        {
            try
            {
                ApplyRunSettings(false);
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.Main.ModeSwitchSaveFailed",
                    ex.Message));
            }
        }

        private SingleWindowSlotSettings GetSingleWindowSettings(int slot)
        {
            if (slot < 1 || slot > _settings.SingleWindowSlots.Count)
                throw new ArgumentOutOfRangeException("slot");
            return _settings.SingleWindowSlots[slot - 1];
        }

        private bool IsCustomResolutionSelected()
        {
            var preset = _resolutionBox.SelectedItem as ResolutionPreset;
            return preset == null || preset.Width == 0;
        }

        private int GetCurrentCustomWidth()
        {
            return _selectedMode == 0
                ? _settings.VirtualDisplay.CustomWidth
                : GetSingleWindowSettings(_selectedMode).CustomWidth;
        }

        private int GetCurrentCustomHeight()
        {
            return _selectedMode == 0
                ? _settings.VirtualDisplay.CustomHeight
                : GetSingleWindowSettings(_selectedMode).CustomHeight;
        }

        private void StoreCurrentCustomResolution()
        {
            var width = (int)_widthBox.Value;
            var height = (int)_heightBox.Value;
            if (_selectedMode == 0)
            {
                _settings.VirtualDisplay.CustomWidth = width;
                _settings.VirtualDisplay.CustomHeight = height;
                return;
            }

            var slot = GetSingleWindowSettings(_selectedMode);
            slot.CustomWidth = width;
            slot.CustomHeight = height;
        }

        private bool IsSelectedModeRunning()
        {
            return _selectedMode == 0
                ? _scrcpyService.IsRunning
                : _singleWindowService.IsRunning(_selectedMode);
        }

        private void AttachRunSettingChangeHandlers()
        {
            EventHandler changed = delegate { MarkRunSettingsDirty(); };
            _resolutionBox.SelectedIndexChanged += changed;
            _widthBox.ValueChanged += changed;
            _heightBox.ValueChanged += changed;
            _dpiBox.ValueChanged += changed;
            _bitRateBox.TextChanged += changed;
            _maxFpsBox.SelectedIndexChanged += changed;
            _turnScreenOffBox.CheckedChanged += changed;
            _stayAwakeBox.CheckedChanged += changed;
            _useHidKeyboardBox.CheckedChanged += changed;
            _useHidMouseBox.CheckedChanged += changed;
            _forceStopAppBox.CheckedChanged += changed;
            _reuseDisplayBox.CheckedChanged += changed;
            _flexDisplayBox.CheckedChanged += changed;
            _additionalArgumentsBox.TextChanged += changed;
            _startAppBox.SelectedIndexChanged += changed;
        }

        private void MarkRunSettingsDirty()
        {
            if (_loadingRunSettings ||
                _selectedMode < 0 ||
                _selectedMode >= _modeSettingsDirty.Length)
            {
                return;
            }

            _modeSettingsDirty[_selectedMode] = true;
            UpdateApplySettingsLink();
        }

        private void UpdateApplySettingsLink()
        {
            if (_applySettingsLink == null) return;
            _applySettingsLink.Visible =
                IsSelectedModeRunning() &&
                _modeSettingsDirty[_selectedMode];
        }

        private bool IsAnySingleWindowRunning()
        {
            for (var slot = 1; slot <= 3; slot++)
            {
                if (_singleWindowService.IsRunning(slot)) return true;
            }
            return false;
        }

        private bool IsAnyScrcpyRunning()
        {
            return _scrcpyService.IsRunning ||
                IsAnySingleWindowRunning();
        }

        private string GetSingleWindowStatusDetail(int slot)
        {
            var settings = GetSingleWindowSettings(slot);
            var app = string.IsNullOrWhiteSpace(settings.StartAppName)
                ? settings.StartAppPackage
                : settings.StartAppName;
            return string.IsNullOrWhiteSpace(app)
                ? LocalizationService.Get("Main.SingleRunningDetail")
                : LocalizationService.Format(
                    "Main.AppRunningDetail",
                    app);
        }

        private void UpdateSingleWindowIndicator(int slot)
        {
            if (!string.IsNullOrWhiteSpace(_connectionError))
            {
                SetConnectionIndicator(
                    Color.Firebrick,
                    LocalizationService.Get("Status.Error"),
                    _connectionError);
                return;
            }
            if (_lastDeviceState == null ||
                _lastDeviceState.Status != AdbDeviceStatus.Device)
            {
                UpdateIndicatorForDevice(_lastDeviceState);
                return;
            }

            var settings = GetSingleWindowSettings(slot);
            SetConnectionIndicator(
                _theme.Accent,
                LocalizationService.Format("Main.SingleReady", slot),
                string.IsNullOrWhiteSpace(settings.StartAppPackage)
                    ? LocalizationService.Get("Main.SelectApp")
                    : LocalizationService.Get("Main.PressStart"));
        }

        private void SetSelectedModeButton(int slot)
        {
            SetButtonPrimary(_dexModeButton, slot == 0);
            SetButtonPrimary(_singleModeButton1, slot == 1);
            SetButtonPrimary(_singleModeButton2, slot == 2);
            SetButtonPrimary(_singleModeButton3, slot == 3);
        }

        private static void SetButtonPrimary(ThemedButton button, bool selected)
        {
            if (button == null) return;
            button.Primary = selected;
            button.Invalidate();
        }

        private void AddTopMenu(string text, int x, Action action)
        {
            var menu = new LinkLabel
            {
                AutoSize = true,
                LinkColor = Color.FromArgb(75, 85, 99),
                ActiveLinkColor = Color.FromArgb(37, 99, 235),
                Location = new Point(x, 43),
                Text = text
            };
            menu.LinkClicked += delegate { action(); };
            Controls.Add(menu);
        }

        private Label AddSectionTitle(string text, int x, int y)
        {
            var label = new Label
            {
                AutoSize = true,
                Font = UiFonts.Create(13F, FontStyle.Bold),
                ForeColor = _theme.TextSecondary,
                Location = new Point(x, y),
                Text = text
            };
            Controls.Add(label);
            return label;
        }

        private Label AddFieldLabel(string text, int x, int y)
        {
            var label = new Label
            {
                AutoSize = true,
                ForeColor = _theme.TextTertiary,
                Location = new Point(x, y),
                Text = text
            };
            Controls.Add(label);
            return label;
        }

        private void AddDivider(int y)
        {
            Controls.Add(new Panel
            {
                BackColor = _theme.CardBorder,
                Location = new Point(32, y),
                Size = new Size(573, 1)
            });
        }

        private static string GetDeviceStatusText(DeviceState state)
        {
            if (state.Status == AdbDeviceStatus.Device)
                return LocalizationService.Format(
                    "Device.Connected",
                    state.Serial);
            if (state.Status == AdbDeviceStatus.Unauthorized)
                return LocalizationService.Get("Device.Unauthorized");
            if (state.Status == AdbDeviceStatus.Offline)
                return LocalizationService.Get("Device.Offline");
            return LocalizationService.Get("Device.Disconnected");
        }

        private static decimal Clamp(int value, ThemedNumberControl box)
        {
            if (value < box.Minimum) return box.Minimum;
            if (value > box.Maximum) return box.Maximum;
            return value;
        }

        private void RunOnUi(Action action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) BeginInvoke(action); else action();
        }

        private void HideToTray() { HideToTray(true); }
        private void HideToTray(bool showBalloon)
        {
            Hide();
            ShowInTaskbar = false;
            if (showBalloon)
                _trayService.ShowBalloon(
                    LocalizationService.Get("App.Name"),
                    LocalizationService.Get("Main.TrayContinues"));
        }

        private void ShowMainWindow()
        {
            RunOnUi(delegate
            {
                _autoHideService.ResetIdleHideState();
                ShowInTaskbar = true;
                Show();
                WindowState = FormWindowState.Normal;
                Activate();
            });
        }

        private void ShowLogForm()
        {
            RunOnUi(delegate
            {
                _autoHideService.ResetIdleHideState();
                if (_logForm == null || _logForm.IsDisposed) _logForm = new LogForm(_logService);
                if (!_logForm.Visible) _logForm.Show();
                _logForm.WindowState = FormWindowState.Normal;
                _logForm.BringToFront();
                _logForm.Activate();
            });
        }

        private void ShowSettingsForm()
        {
            RunOnUi(delegate
            {
                _autoHideService.ResetIdleHideState();
                if (_settingsForm == null || _settingsForm.IsDisposed)
                {
                    _settingsForm = new SettingsForm(
                        _settingsService,
                        _settings,
                        _adbService,
                        _wirelessAdbService,
                        ShowLogForm,
                        ShowEnvironmentCheck,
                        ApplyThemeSelection);
                    _settingsForm.FormClosed += delegate { _settingsForm = null; };
                }
                if (!_settingsForm.Visible) _settingsForm.Show();
                _settingsForm.WindowState = FormWindowState.Normal;
                _settingsForm.BringToFront();
                _settingsForm.Activate();
            });
        }

        private void ShowEnvironmentCheck()
        {
            RunOnUi(delegate
            {
                _autoHideService.ResetIdleHideState();
                if (_environmentCheckForm == null || _environmentCheckForm.IsDisposed)
                {
                    _environmentCheckForm = new EnvironmentCheckForm(_environmentCheckService);
                    _environmentCheckForm.FormClosed += delegate { _environmentCheckForm = null; };
                }
                if (!_environmentCheckForm.Visible) _environmentCheckForm.Show();
                _environmentCheckForm.WindowState = FormWindowState.Normal;
                _environmentCheckForm.BringToFront();
                _environmentCheckForm.Activate();
            });
        }

        private void HideApplicationForIdle()
        {
            if (_logForm != null && !_logForm.IsDisposed) _logForm.Hide();
            if (_settingsForm != null && !_settingsForm.IsDisposed) _settingsForm.Hide();
            if (_environmentCheckForm != null && !_environmentCheckForm.IsDisposed) _environmentCheckForm.Hide();
            HideToTray(false);
        }

        private async void ExitApplication()
        {
            if (InvokeRequired) { BeginInvoke((Action)ExitApplication); return; }
            if (_exitInProgress) return;
            _exitInProgress = true;
            _captureCoordinator.Stop();
            BeginPhoneScreenWakeSuppression();
            _dexStatusValue.Text =
                LocalizationService.Get("Status.ShuttingDown");
            Enabled = false;
            _deviceMonitor.Stop();
            await Task.Run(delegate { _singleWindowService.StopAll(); });
            await _orchestrator.ShutdownAsync();
            if (_adbService.IsAuthorizedDeviceConnected())
                await Task.Run((Action)WakePhoneScreen);
            _allowExit = true;
            Close();
        }

        private sealed class ResolutionPreset
        {
            public ResolutionPreset(string text, int width, int height)
            {
                Text = text;
                Width = width;
                Height = height;
            }

            public string Text { get; private set; }
            public int Width { get; private set; }
            public int Height { get; private set; }
            public override string ToString() { return Text; }
        }
    }
}

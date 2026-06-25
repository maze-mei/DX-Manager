using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.Forms
{
    public sealed class MainForm : Form
    {
        private readonly SettingsService _settingsService;
        private readonly AppSettings _settings;
        private readonly LogService _logService;
        private readonly AdbService _adbService;
        private readonly ScrcpyService _scrcpyService;
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
        private readonly Label _indicatorDot;
        private readonly Label _indicatorStatus;
        private readonly Label _indicatorDetail;
        private readonly Label _deviceInfoLabel;
        private readonly Button _startButton;
        private readonly Button _stopButton;
        private readonly ComboBox _resolutionBox;
        private readonly NumericUpDown _widthBox;
        private readonly NumericUpDown _heightBox;
        private readonly Label _widthLabel;
        private readonly Label _heightLabel;
        private readonly Label _dpiLabel;
        private readonly NumericUpDown _dpiBox;
        private readonly TextBox _bitRateBox;
        private readonly ComboBox _maxFpsBox;
        private readonly CheckBox _turnScreenOffBox;
        private readonly CheckBox _stayAwakeBox;
        private readonly CheckBox _useHidKeyboardBox;
        private readonly CheckBox _useHidMouseBox;
        private readonly CheckBox _forceStopAppBox;
        private readonly CheckBox _reuseDisplayBox;
        private readonly TextBox _additionalArgumentsBox;
        private readonly ComboBox _startAppBox;
        private readonly Button _loadAppsButton;
        private DeviceState _lastDeviceState;
        private string _connectionError;
        private bool _allowExit;
        private bool _exitInProgress;
        private LogForm _logForm;
        private SettingsForm _settingsForm;
        private EnvironmentCheckForm _environmentCheckForm;

        public MainForm(
            SettingsService settingsService,
            AppSettings settings,
            LogService logService,
            AdbService adbService,
            ScrcpyService scrcpyService,
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
            _scrcpyService = scrcpyService;
            _deviceMonitor = deviceMonitor;
            _orchestrator = orchestrator;
            _captureCoordinator = captureCoordinator;
            _autoHideService = autoHideService;
            _environmentCheckService = environmentCheckService;
            _keyMappingService = keyMappingService;
            _isAutoRun = isAutoRun;
            _lastDeviceState = DeviceState.Disconnected();

            Text = "DeX Manager";
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(248, 250, 252);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            ClientSize = new Size(640, 616);
            MinimumSize = Size;

            Controls.Add(new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = Color.FromArgb(17, 24, 39),
                Location = new Point(32, 28),
                Text = "DeX Manager"
            });
            AddTopMenu("로그", 454, ShowLogForm);
            AddTopMenu("설정", 510, ShowSettingsForm);
            AddTopMenu("환경 점검", 560, ShowEnvironmentCheck);

            _indicatorDot = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                Location = new Point(33, 91),
                Text = "●"
            };
            _indicatorStatus = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                ForeColor = Color.FromArgb(17, 24, 39),
                Location = new Point(66, 90),
                Size = new Size(240, 31),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _indicatorDetail = new Label
            {
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(75, 85, 99),
                Location = new Point(35, 130),
                Size = new Size(570, 22)
            };
            _deviceInfoLabel = new Label
            {
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(75, 85, 99),
                Location = new Point(35, 157),
                Size = new Size(570, 22),
                Text = "휴대폰 USB 연결을 기다립니다."
            };
            Controls.Add(_indicatorDot);
            Controls.Add(_indicatorStatus);
            Controls.Add(_indicatorDetail);
            Controls.Add(_deviceInfoLabel);
            SetConnectionIndicator(Color.DarkOrange, "연결 대기 중", "ADB 초기화를 준비합니다.");

            _adbStatusValue = new Label { Visible = false };
            _scrcpyStatusValue = new Label { Visible = false };
            _dexStatusValue = new Label { Visible = false };
            _deviceStatusValue = new Label { Visible = false };
            AddDivider(204);

            AddSectionTitle("DeX 화면 설정", 32, 226);
            _resolutionBox = CreateStyledCombo(105, 263, 160, true);
            _resolutionBox.Items.Add(new ResolutionPreset("1600 x 900", 1600, 900));
            _resolutionBox.Items.Add(new ResolutionPreset("1920 x 1080", 1920, 1080));
            _resolutionBox.Items.Add(new ResolutionPreset("3840 x 2160 (4K)", 3840, 2160));
            _resolutionBox.Items.Add(new ResolutionPreset("사용자 지정", 0, 0));
            _resolutionBox.SelectedIndexChanged += ResolutionBox_SelectedIndexChanged;
            _widthBox = CreateStyledNumber(320, 7680, 305, 263, 55);
            _heightBox = CreateStyledNumber(240, 4320, 405, 263, 55);
            _dpiBox = CreateStyledNumber(80, 640, 495, 263, 90);
            _bitRateBox = CreateStyledTextBox(105, 298, 130, true);
            _maxFpsBox = CreateStyledCombo(495, 298, 90, true);
            _maxFpsBox.Items.Add(30);
            _maxFpsBox.Items.Add(60);
            AddFieldLabel("해상도", 32, 269);
            _widthLabel = AddFieldLabel("가로", 270, 269);
            _heightLabel = AddFieldLabel("세로", 370, 269);
            _dpiLabel = AddFieldLabel("DPI", 425, 269);
            AddFieldLabel("비트레이트", 32, 304);
            AddFieldLabel("최대 FPS", 425, 304);

            AddDivider(339);
            AddSectionTitle("실행 옵션", 32, 360);
            _turnScreenOffBox = CreateOption("폰 화면 끄기 (-S)", 32, 395);
            _useHidKeyboardBox = CreateOption("HID 키보드 (-K)", 32, 429);
            _useHidMouseBox = CreateOption("HID 마우스 (-M)", 32, 463);
            _forceStopAppBox = CreateOption("선택 앱 강제 종료", 392, 395);
            _reuseDisplayBox = CreateOption("기존 가상화면 재사용", 392, 429);
            _stayAwakeBox = CreateOption("잠자기 방지 (-w)", 392, 463);

            _startAppBox = CreateStyledCombo(132, 502, 313);
            _startAppBox.DropDownStyle = ComboBoxStyle.DropDown;
            _loadAppsButton = CreateThemedButton("앱 목록 불러오기", false, 455, 501, 150);
            _loadAppsButton.Click += LoadAppsButton_Click;
            AddFieldLabel("시작 앱", 32, 508);

            _additionalArgumentsBox = CreateStyledTextBox(32, 577, 440);
            _additionalArgumentsBox.Visible = false;
            var advancedToggle = new LinkLabel
            {
                AutoSize = true,
                LinkColor = Color.FromArgb(75, 85, 99),
                ActiveLinkColor = Color.FromArgb(37, 99, 235),
                Location = new Point(32, 546),
                Text = "▶  고급 옵션 (추가 인자)"
            };
            advancedToggle.LinkClicked += delegate
            {
                _additionalArgumentsBox.Visible = !_additionalArgumentsBox.Visible;
                advancedToggle.Text = _additionalArgumentsBox.Visible
                    ? "▼  고급 옵션 (추가 인자)"
                    : "▶  고급 옵션 (추가 인자)";
            };
            Controls.Add(advancedToggle);

            _startButton = CreateThemedButton("DeX 시작", true, 353, 580, 120);
            _startButton.Click += StartButton_Click;
            _stopButton = CreateThemedButton("DeX 중지", true, 353, 580, 120);
            _stopButton.Click += StopButton_Click;
            _stopButton.Visible = false;
            var applyRunSettingsButton = CreateThemedButton("실행 설정 적용", false, 485, 580, 120);
            applyRunSettingsButton.Click += ApplyRunSettingsButton_Click;
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
            Controls.Add(_stayAwakeBox);
            Controls.Add(_startAppBox);
            Controls.Add(_loadAppsButton);
            Controls.Add(_additionalArgumentsBox);
            Controls.Add(_startButton);
            Controls.Add(_stopButton);
            Controls.Add(applyRunSettingsButton);
            LoadRunSettings();

            Shown += MainForm_Shown;
            FormClosing += MainForm_FormClosing;
            FormClosed += MainForm_FormClosed;
            _deviceMonitor.StateChanged += DeviceMonitor_StateChanged;
            _deviceMonitor.DeviceConnected += DeviceMonitor_DeviceConnected;
            _deviceMonitor.DeviceDisconnected += DeviceMonitor_DeviceDisconnected;
            _scrcpyService.RunningChanged += ScrcpyService_RunningChanged;
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
            _logService.Info("메인 창을 표시했습니다.");
            try { _captureCoordinator.Start(); }
            catch (Exception ex)
            {
                _logService.Error("캡처 단축키 등록에 실패했습니다.", ex);
                _trayService.ShowBalloon("DEX Manager", "F8 캡처 단축키를 등록하지 못했습니다.");
            }

            if (_settings.Features.AutoHideEnabled) _autoHideService.Start();
            try { _keyMappingService.Start(); }
            catch (Exception ex)
            {
                _logService.Error("키 매핑 시작에 실패했습니다.", ex);
                _trayService.ShowBalloon("DEX Manager", "Scrcpy 키 매핑을 시작하지 못했습니다.");
            }

            await InitializeAdbAndMonitorAsync();
            if (_isAutoRun && _settings.Features.StartMinimizedToTray)
                BeginInvoke((Action)HideToTray);
        }

        private async Task InitializeAdbAndMonitorAsync()
        {
            _adbStatusValue.Text = "초기화 중";
            SetConnectionIndicator(Color.DarkOrange, "연결 대기 중", "ADB 서버를 준비하고 있습니다.");
            try
            {
                await Task.Run(delegate
                {
                    _adbService.LogStartupDiagnostics();
                    if (_settings.Features.ScrcpyWakeUpMode == ScrcpyWakeUpMode.AlwaysOnStartup)
                    {
                        _adbService.WakeUp(delegate { return _scrcpyService.RunWakeUp(_settings.Timing.AdbWakeUpDelayMs); });
                    }
                    else
                    {
                        _adbService.StartServer();
                        _adbService.GetDevices();
                        if (_settings.Features.ScrcpyWakeUpMode == ScrcpyWakeUpMode.OnAdbFailure && !_adbService.IsAuthorizedDeviceConnected())
                        {
                            _adbService.WakeUp(delegate { return _scrcpyService.RunWakeUp(_settings.Timing.AdbWakeUpDelayMs); });
                        }
                    }
                });
                _adbStatusValue.Text = "준비";
                _connectionError = null;
                SetConnectionIndicator(Color.DarkOrange, "연결 대기 중", "휴대폰 연결을 기다립니다.");
            }
            catch (Exception ex)
            {
                _adbStatusValue.Text = "오류";
                _logService.Error("ADB 초기화에 실패했습니다.", ex);
                _connectionError = "ADB 초기화 실패: " + ex.Message;
                SetConnectionIndicator(Color.Firebrick, "오류", _connectionError);
            }
            finally { _deviceMonitor.Start(); }
        }

        private async void StartButton_Click(object sender, EventArgs e) { await StartDexAsync(); }
        private async void StopButton_Click(object sender, EventArgs e) { await StopDexAsync(); }

        private async Task StartDexAsync()
        {
            try { ApplyRunSettings(false); }
            catch (Exception ex) { ShowError("실행 설정을 적용하지 못했습니다.", ex); return; }

            _connectionError = null;
            SetOperationState(true, "시작 중");
            SetConnectionIndicator(Color.DarkOrange, "DeX 시작 중", "가상화면과 Scrcpy를 준비합니다.");
            try { await _orchestrator.StartAsync(); }
            catch (Exception ex) { ShowError("DeX를 시작하지 못했습니다.", ex); }
            finally { UpdateRunningState(); }
        }

        private async Task StopDexAsync()
        {
            _connectionError = null;
            SetOperationState(true, "중지 중");
            SetConnectionIndicator(Color.DarkOrange, "DeX 중지 중", "가상화면을 정리합니다.");
            try { await _orchestrator.StopAsync(); }
            catch (Exception ex) { ShowError("DeX를 중지하는 중 오류가 발생했습니다.", ex); }
            finally { UpdateRunningState(); }
        }

        private void DeviceMonitor_StateChanged(object sender, DeviceStateChangedEventArgs e)
        {
            RunOnUi(delegate
            {
                _lastDeviceState = e.Current;
                _adbStatusValue.Text = e.Current.Status == AdbDeviceStatus.Unknown ? "대기" : "응답";
                _deviceStatusValue.Text = GetDeviceStatusText(e.Current);
                _deviceInfoLabel.Text = e.Current.Status == AdbDeviceStatus.Device
                    ? "연결된 Android 장치  ·  " + e.Current.Serial
                    : "휴대폰 USB 연결을 기다립니다.";
                if (!_scrcpyService.IsRunning)
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
            if (_orchestrator.IsRunning)
                RunOnUi(async delegate { await StopDexAsync(); });
        }

        private void ScrcpyService_RunningChanged(object sender, EventArgs e) { RunOnUi(UpdateRunningState); }
        private void CaptureCoordinator_ExitHotkeyPressed(object sender, EventArgs e) { RunOnUi(ExitApplication); }
        private void AutoHideService_IdleHideRequested(object sender, EventArgs e) { RunOnUi(HideApplicationForIdle); }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _deviceMonitor.Dispose();
            _captureCoordinator.Dispose();
            _autoHideService.Dispose();
            _keyMappingService.Dispose();
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
            var running = _scrcpyService.IsRunning;
            _scrcpyStatusValue.Text = running ? "실행 중" : "중지";
            _dexStatusValue.Text = running ? "실행 중" : "대기";
            _startButton.Enabled = !running;
            _stopButton.Enabled = running;
            _startButton.Visible = !running;
            _stopButton.Visible = running;
            if (!string.IsNullOrWhiteSpace(_connectionError))
            {
                SetConnectionIndicator(Color.Firebrick, "오류", _connectionError);
                return;
            }
            if (running)
                SetConnectionIndicator(Color.ForestGreen, "DeX 실행 중", "Scrcpy 가상화면이 정상 실행 중입니다.");
            else
                UpdateIndicatorForDevice(_lastDeviceState);
        }

        private void SetOperationState(bool operationRunning, string status)
        {
            var running = _scrcpyService.IsRunning;
            _startButton.Visible = !running;
            _stopButton.Visible = running;
            _startButton.Enabled = !operationRunning && !running;
            _stopButton.Enabled = !operationRunning && running;
            _dexStatusValue.Text = status;
        }

        private void UpdateIndicatorForDevice(DeviceState state)
        {
            if (!string.IsNullOrWhiteSpace(_connectionError))
            {
                SetConnectionIndicator(Color.Firebrick, "오류", _connectionError);
                return;
            }
            if (state != null && state.Status == AdbDeviceStatus.Device)
            {
                SetConnectionIndicator(Color.ForestGreen, "휴대폰 연결됨", "DeX 시작을 기다립니다.");
                return;
            }
            if (state != null && state.Status == AdbDeviceStatus.Unauthorized)
            {
                SetConnectionIndicator(Color.DarkOrange, "승인 필요", "휴대폰에서 USB 디버깅 RSA 승인을 확인하세요.");
                return;
            }
            if (state != null && state.Status == AdbDeviceStatus.Offline)
            {
                SetConnectionIndicator(Color.Firebrick, "장치 오프라인", "USB 연결 또는 ADB 상태를 확인하세요.");
                return;
            }
            SetConnectionIndicator(Color.DarkOrange, "연결 대기 중", "휴대폰 USB 연결을 기다립니다.");
        }

        private void SetConnectionIndicator(Color color, string status, string detail)
        {
            _indicatorDot.ForeColor = color;
            _indicatorStatus.Text = status;
            _indicatorDetail.Text = detail;
        }

        private void ShowError(string message, Exception exception)
        {
            _logService.Error(message, exception);
            _connectionError = message + ": " + exception.Message;
            SetConnectionIndicator(Color.Firebrick, "오류", _connectionError);
            MessageBox.Show(this, message + "\r\n\r\n" + exception.Message, "DEX Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void ResolutionBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var preset = _resolutionBox.SelectedItem as ResolutionPreset;
            var custom = preset == null || preset.Width == 0;
            _widthBox.Enabled = custom;
            _heightBox.Enabled = custom;
            _widthBox.Visible = custom;
            _heightBox.Visible = custom;
            _widthLabel.Visible = custom;
            _heightLabel.Visible = custom;
            _dpiBox.Location = new Point(custom ? 515 : 495, 263);
            _dpiLabel.Location = new Point(custom ? 480 : 425, 269);
            if (!custom)
            {
                _widthBox.Value = preset.Width;
                _heightBox.Value = preset.Height;
            }
        }

        private void LoadRunSettings()
        {
            _widthBox.Value = Clamp(_settings.VirtualDisplay.Width, _widthBox);
            _heightBox.Value = Clamp(_settings.VirtualDisplay.Height, _heightBox);
            _dpiBox.Value = Clamp(_settings.VirtualDisplay.Dpi, _dpiBox);
            _bitRateBox.Text = _settings.Scrcpy.BitRate;
            _maxFpsBox.SelectedItem = _settings.Scrcpy.MaxFps == 30 ? 30 : 60;
            _turnScreenOffBox.Checked = _settings.Scrcpy.TurnScreenOff;
            _stayAwakeBox.Checked = _settings.Scrcpy.StayAwake;
            _useHidKeyboardBox.Checked = _settings.Scrcpy.UseHidKeyboard;
            _useHidMouseBox.Checked = _settings.Scrcpy.UseHidMouse;
            _forceStopAppBox.Checked = _settings.Scrcpy.ForceStopStartApp;
            _reuseDisplayBox.Checked = _settings.VirtualDisplay.ReuseExistingDisplay;
            _additionalArgumentsBox.Text = _settings.Scrcpy.AdditionalArguments;
            _startAppBox.Text = _settings.Scrcpy.StartAppPackage;
            _resolutionBox.SelectedIndex = FindResolutionPresetIndex(
                _settings.VirtualDisplay.Width,
                _settings.VirtualDisplay.Height);
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
                if (!_adbService.IsAuthorizedDeviceConnected())
                {
                    _logService.Info(
                        "실행 설정을 저장했습니다. 연결된 ADB 장치가 없어 다음 DeX 시작 때 적용합니다.");
                    MessageBox.Show(
                        this,
                        "실행 설정을 저장했습니다. 연결된 휴대폰이 없어 다음 DeX 시작 때 적용됩니다.",
                        "DEX Manager",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                _connectionError = null;
                SetOperationState(true, "설정 적용 중");
                SetConnectionIndicator(
                    Color.DarkOrange,
                    "설정 적용 중",
                    "기존 가상화면을 제거하고 새 설정으로 다시 시작합니다.");

                var applied = await _orchestrator.ApplyRuntimeSettingsAsync();
                if (!applied)
                {
                    MessageBox.Show(
                        this,
                        "실행 설정은 저장했습니다. 현재 적용하지 못해 다음 DeX 시작 때 사용합니다.",
                        "DEX Manager",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                UpdateRunningState();
                MessageBox.Show(
                    this,
                    "실행 설정을 저장하고 새 가상화면으로 다시 시작했습니다.",
                    "DEX Manager",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _logService.Error("실행 설정은 저장했지만 즉시 적용하지 못했습니다.", ex);
                _connectionError = "실행 설정 즉시 적용 실패: " + ex.Message;
                SetConnectionIndicator(Color.Firebrick, "오류", _connectionError);
                MessageBox.Show(
                    this,
                    "실행 설정은 저장했습니다.\r\n현재 적용하지 못해 다음 DeX 시작 때 사용합니다.\r\n\r\n" + ex.Message,
                    "DEX Manager",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            finally
            {
                UpdateRunningState();
            }
        }

        private async void LoadAppsButton_Click(object sender, EventArgs e)
        {
            if (_loadAppsButton.Enabled == false) return;

            _loadAppsButton.Enabled = false;
            _loadAppsButton.Text = "불러오는 중...";
            try
            {
                var apps = await Task.Run(delegate { return _scrcpyService.ListApps(); });
                var selectedPackage = GetSelectedAppPackage();

                _startAppBox.BeginUpdate();
                _startAppBox.Items.Clear();
                foreach (var app in apps) _startAppBox.Items.Add(app);
                _startAppBox.EndUpdate();

                var selected = false;
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

                if (!selected) _startAppBox.Text = selectedPackage;
                _logService.Info("Scrcpy 앱 목록을 메인 화면에 표시했습니다.");
            }
            catch (Exception ex)
            {
                _logService.Error("Scrcpy 앱 목록을 불러오지 못했습니다.", ex);
                MessageBox.Show(
                    this,
                    "앱 목록을 불러오지 못했습니다.\r\n\r\n" + ex.Message,
                    "DEX Manager",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            finally
            {
                _loadAppsButton.Text = "앱 목록 불러오기";
                _loadAppsButton.Enabled = true;
            }
        }

        private void ApplyRunSettings(bool showMessage)
        {
            var bitRate = _bitRateBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(bitRate))
                throw new InvalidOperationException("비트레이트를 입력하세요. 예: 20M");

            _settings.VirtualDisplay.Width = (int)_widthBox.Value;
            _settings.VirtualDisplay.Height = (int)_heightBox.Value;
            _settings.VirtualDisplay.Dpi = (int)_dpiBox.Value;
            _settings.VirtualDisplay.ReuseExistingDisplay = _reuseDisplayBox.Checked;
            _settings.Scrcpy.BitRate = bitRate;
            _settings.Scrcpy.MaxFps = (int)_maxFpsBox.SelectedItem;
            _settings.Scrcpy.TurnScreenOff = _turnScreenOffBox.Checked;
            _settings.Scrcpy.StayAwake = _stayAwakeBox.Checked;
            _settings.Scrcpy.UseHidKeyboard = _useHidKeyboardBox.Checked;
            _settings.Scrcpy.UseHidMouse = _useHidMouseBox.Checked;
            _settings.Scrcpy.ForceStopStartApp = _forceStopAppBox.Checked;
            _settings.Scrcpy.StartAppPackage = GetSelectedAppPackage();
            _settings.Scrcpy.AdditionalArguments = _additionalArgumentsBox.Text.Trim();
            _settingsService.Save(_settings);
            _logService.Info("메인 화면의 DeX/Scrcpy 실행 설정을 저장했습니다.");
            if (showMessage)
            {
                MessageBox.Show(this, "실행 설정을 저장했습니다. DeX 실행 중이면 다음 시작부터 적용됩니다.", "DEX Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private string GetSelectedAppPackage()
        {
            var app = _startAppBox.SelectedItem as ScrcpyAppInfo;
            return app == null ? _startAppBox.Text.Trim() : app.PackageName;
        }

        private static NumericUpDown CreateNumber(int min, int max, int width)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, Width = width };
        }

        private static CheckBox CreateOption(string text, int x, int y)
        {
            return new ThemedCheckBox
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(220, 28)
            };
        }

        private ComboBox CreateStyledCombo(
            int x,
            int y,
            int width,
            bool centerText = false)
        {
            var box = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(31, 41, 55),
                Font = new Font("Segoe UI", 9F),
                Location = new Point(x, y),
                Size = new Size(width, 28)
            };
            if (centerText)
            {
                box.DrawMode = DrawMode.OwnerDrawFixed;
                box.DrawItem += CenteredComboBox_DrawItem;
            }
            return box;
        }

        private NumericUpDown CreateStyledNumber(int min, int max, int x, int y, int width)
        {
            return new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(31, 41, 55),
                Font = new Font("Segoe UI", 9F),
                TextAlign = HorizontalAlignment.Center,
                Location = new Point(x, y),
                Size = new Size(width, 28)
            };
        }

        private TextBox CreateStyledTextBox(
            int x,
            int y,
            int width,
            bool centerText = false)
        {
            return new TextBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(31, 41, 55),
                Font = new Font("Segoe UI", 9F),
                TextAlign = centerText
                    ? HorizontalAlignment.Center
                    : HorizontalAlignment.Left,
                Location = new Point(x, y),
                Size = new Size(width, 28)
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
                Location = new Point(x, y),
                Size = new Size(width, 34)
            };
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

        private void AddSectionTitle(string text, int x, int y)
        {
            Controls.Add(new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(17, 24, 39),
                Location = new Point(x, y),
                Text = text
            });
        }

        private Label AddFieldLabel(string text, int x, int y)
        {
            var label = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(75, 85, 99),
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
                BackColor = Color.FromArgb(229, 231, 235),
                Location = new Point(32, y),
                Size = new Size(573, 1)
            });
        }

        private static void CenteredComboBox_DrawItem(
            object sender,
            DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var comboBox = sender as ComboBox;
            if (comboBox == null) return;

            e.DrawBackground();
            var text = comboBox.GetItemText(comboBox.Items[e.Index]);
            var color = (e.State & DrawItemState.Selected) == DrawItemState.Selected
                ? SystemColors.HighlightText
                : Color.FromArgb(31, 41, 55);
            TextRenderer.DrawText(
                e.Graphics,
                text,
                comboBox.Font,
                e.Bounds,
                color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        }

        private static string GetDeviceStatusText(DeviceState state)
        {
            if (state.Status == AdbDeviceStatus.Device) return "연결됨 (" + state.Serial + ")";
            if (state.Status == AdbDeviceStatus.Unauthorized) return "RSA 승인 필요";
            if (state.Status == AdbDeviceStatus.Offline) return "오프라인";
            return "연결 안 됨";
        }

        private static decimal Clamp(int value, NumericUpDown box)
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
            if (showBalloon) _trayService.ShowBalloon("DEX Manager", "프로그램이 시스템 트레이에서 계속 실행됩니다.");
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
                _logForm.Show();
                _logForm.WindowState = FormWindowState.Normal;
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
                    _settingsForm = new SettingsForm(_settingsService, _settings, _adbService);
                    _settingsForm.FormClosed += delegate { _settingsForm = null; };
                }
                _settingsForm.Show(this);
                _settingsForm.WindowState = FormWindowState.Normal;
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
                _environmentCheckForm.Show(this);
                _environmentCheckForm.WindowState = FormWindowState.Normal;
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
            _dexStatusValue.Text = "종료 정리 중";
            Enabled = false;
            _deviceMonitor.Stop();
            await _orchestrator.ShutdownAsync();
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

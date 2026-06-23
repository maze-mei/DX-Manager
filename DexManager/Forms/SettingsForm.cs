using System;
using System.Drawing;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed class SettingsForm : Form
    {
        private readonly SettingsService _settingsService;
        private readonly AppSettings _settings;
        private readonly AdbService _adbService;

        private RadioButton _automaticAdbBox;
        private RadioButton _manualAdbBox;
        private TextBox _manualAdbPathBox;
        private Panel _manualAdbPanel;
        private TextBox _scrcpyPathBox;
        private TextBox _screenshotFolderBox;
        private TextBox _deviceScreenshotFolderBox;
        private TextBox _logFolderBox;
        private CheckBox _startWithWindowsBox;
        private CheckBox _startMinimizedBox;
        private ComboBox _wakeUpModeBox;
        private CheckBox _autoHideBox;
        private CheckBox _pushCaptureBox;
        private CheckBox _resetDisplayOnStopBox;
        private CheckBox _disableStayAwakeBox;
        private CheckBox _autoStartDexBox;
        private NumericUpDown _deviceMonitorIntervalBox;
        private NumericUpDown _disconnectMonitorIntervalBox;
        private NumericUpDown _connectedStartDelayBox;
        private NumericUpDown _adbWakeUpDelayBox;
        private NumericUpDown _autoHideSecondsBox;
        private NumericUpDown _captureWaitSecondsBox;
        private NumericUpDown _processTimeoutBox;
        private TextBox _captureHotkeyBox;
        private TextBox _exitHotkeyBox;
        private CheckBox _lowLevelHotkeyBox;
        private CheckBox _keyboardDiagnosticsBox;
        private CheckBox _convertHangulBox;
        private ComboBox _hangulInputModeBox;
        private CheckBox _rightWindowsBox;
        private CheckBox _convertEnterBox;
        private ComboBox _enterInputModeBox;
        private CheckBox _ignoreShiftSpaceBox;

        public SettingsForm(
            SettingsService settingsService,
            AppSettings settings,
            AdbService adbService)
        {
            _settingsService = settingsService;
            _settings = settings;
            _adbService = adbService;

            Text = "DEX Manager 설정";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(820, 610);
            MinimumSize = new Size(760, 540);

            var description = new Label
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(10),
                Text = "해상도와 Scrcpy 실행 옵션은 메인 창에서 변경합니다. ADB/경로와 고급 동작은 이 창에서 관리합니다."
            };

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildGeneralTab());
            tabs.TabPages.Add(BuildPathTab());
            tabs.TabPages.Add(BuildKeyboardTab());
            tabs.TabPages.Add(BuildTimingTab());

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8)
            };
            var saveButton = new Button { Text = "저장", Size = new Size(96, 32) };
            saveButton.Click += SaveButton_Click;
            var closeButton = new Button { Text = "닫기", Size = new Size(96, 32) };
            closeButton.Click += delegate { Close(); };
            buttons.Controls.Add(saveButton);
            buttons.Controls.Add(closeButton);

            Controls.Add(tabs);
            Controls.Add(description);
            Controls.Add(buttons);
            LoadValues();
        }

        private TabPage BuildGeneralTab()
        {
            var page = new TabPage("기본");
            var table = CreateTable();
            _startWithWindowsBox = AddCheck(table, "Windows 시작 시 자동 실행");
            _startMinimizedBox = AddCheck(table, "자동 실행 시 트레이로 시작");
            _wakeUpModeBox = AddCombo<ScrcpyWakeUpMode>(table, "ADB Wake-up 방식");
            _autoHideBox = AddCheck(table, "자동 숨김 사용");
            _autoStartDexBox = AddCheck(table, "기기 연결 시 DeX 자동 시작");
            _resetDisplayOnStopBox = AddCheck(table, "중지/종료 시 가상화면 제거");
            _disableStayAwakeBox = AddCheck(table, "중지/종료 시 stay awake 해제");
            page.Controls.Add(Wrap(table));
            return page;
        }

        private TabPage BuildPathTab()
        {
            var page = new TabPage("경로 / ADB");
            var table = CreateTable();

            _automaticAdbBox = new RadioButton
            {
                Text = "자동 선택 (Windows 버전과 Scrcpy 경로 기준)",
                AutoSize = true
            };
            _manualAdbBox = new RadioButton
            {
                Text = "수동 지정",
                AutoSize = true
            };
            _automaticAdbBox.CheckedChanged += delegate { UpdateManualAdbControls(); };
            _manualAdbBox.CheckedChanged += delegate { UpdateManualAdbControls(); };
            AddRow(table, "ADB 모드", _automaticAdbBox);
            AddRow(table, string.Empty, _manualAdbBox);
            AddReadOnly(table, "현재 OS", WindowsVersionHelper.GetDisplayName());
            AddReadOnly(table, "현재 사용 중인 ADB", _adbService.AdbPath);
            AddReadOnly(table, "ADB 버전", GetAdbVersionText());

            _manualAdbPanel = CreatePathPanel(out _manualAdbPathBox, true);
            AddRow(table, "수동 ADB 경로", _manualAdbPanel);
            _scrcpyPathBox = AddPath(table, "Scrcpy 실행 파일", true);
            _screenshotFolderBox = AddPath(table, "PC 스크린샷 폴더", false);
            _deviceScreenshotFolderBox = AddText(table, "폰 전송 폴더");
            _logFolderBox = AddPath(table, "로그 폴더", false);
            page.Controls.Add(Wrap(table));
            return page;
        }

        private TabPage BuildKeyboardTab()
        {
            var page = new TabPage("키 입력");
            var table = CreateTable();
            _captureHotkeyBox = AddText(table, "캡처 단축키");
            _exitHotkeyBox = AddText(table, "종료 단축키");
            _lowLevelHotkeyBox = AddCheck(table, "저수준 키보드 후크로 단축키 처리");
            _keyboardDiagnosticsBox = AddCheck(table, "키 진단 로그 남기기");
            _convertHangulBox = AddCheck(table, "한영키를 Shift+Space로 보정");
            _hangulInputModeBox = AddCombo<KeyInputMode>(table, "한영키 전송 방식");
            _rightWindowsBox = AddCheck(table, "오른쪽 Windows 키 보정");
            _convertEnterBox = AddCheck(table, "Scroll Lock으로 Enter/Shift+Enter 모드 전환");
            _enterInputModeBox = AddCombo<KeyInputMode>(table, "Enter 전송 방식");
            _ignoreShiftSpaceBox = AddCheck(table, "직접 Shift+Space 입력 무시");
            page.Controls.Add(Wrap(table));
            return page;
        }

        private TabPage BuildTimingTab()
        {
            var page = new TabPage("시간 / 캡처");
            var table = CreateTable();
            _deviceMonitorIntervalBox = AddNumber(table, "기기 감시 주기(ms)", 200, 60000);
            _disconnectMonitorIntervalBox = AddNumber(table, "분리 감시 주기(ms)", 200, 60000);
            _connectedStartDelayBox = AddNumber(table, "연결 후 시작 대기(ms)", 0, 60000);
            _adbWakeUpDelayBox = AddNumber(table, "ADB Wake-up 대기(ms)", 0, 60000);
            _autoHideSecondsBox = AddNumber(table, "자동 숨김 대기(초)", 1, 3600);
            _captureWaitSecondsBox = AddNumber(table, "캡처 선택 대기(초)", 1, 60);
            _processTimeoutBox = AddNumber(table, "프로세스 제한시간(ms)", 1000, 120000);
            _pushCaptureBox = AddCheck(table, "캡처 후 폰으로 전송");
            page.Controls.Add(Wrap(table));
            return page;
        }

        private void LoadValues()
        {
            _automaticAdbBox.Checked =
                _settings.Paths.AdbSelectionMode == AdbSelectionMode.Auto;
            _manualAdbBox.Checked = !_automaticAdbBox.Checked;
            _manualAdbPathBox.Text = _settings.Paths.AdbPath;
            _scrcpyPathBox.Text = _settings.Paths.ScrcpyPath;
            _screenshotFolderBox.Text = _settings.Paths.ScreenshotFolder;
            _deviceScreenshotFolderBox.Text = _settings.Paths.DeviceScreenshotFolder;
            _logFolderBox.Text = _settings.Paths.LogFolder;

            _startWithWindowsBox.Checked = _settings.Features.StartWithWindows;
            _startMinimizedBox.Checked = _settings.Features.StartMinimizedToTray;
            _wakeUpModeBox.SelectedItem = _settings.Features.ScrcpyWakeUpMode;
            _autoHideBox.Checked = _settings.Features.AutoHideEnabled;
            _autoStartDexBox.Checked = _settings.Features.AutoStartDexOnDeviceConnected;
            _resetDisplayOnStopBox.Checked = _settings.Features.ResetVirtualDisplayOnStop;
            _disableStayAwakeBox.Checked = _settings.Features.DisableStayAwakeOnStop;
            _pushCaptureBox.Checked = _settings.Features.PushCaptureToDevice;

            _deviceMonitorIntervalBox.Value = Clamp(_settings.Timing.DeviceMonitorIntervalMs, _deviceMonitorIntervalBox);
            _disconnectMonitorIntervalBox.Value = Clamp(_settings.Timing.DisconnectMonitorIntervalMs, _disconnectMonitorIntervalBox);
            _connectedStartDelayBox.Value = Clamp(_settings.Timing.ConnectedStartDelayMs, _connectedStartDelayBox);
            _adbWakeUpDelayBox.Value = Clamp(_settings.Timing.AdbWakeUpDelayMs, _adbWakeUpDelayBox);
            _autoHideSecondsBox.Value = Clamp(_settings.Timing.AutoHideIdleSeconds, _autoHideSecondsBox);
            _captureWaitSecondsBox.Value = Clamp(_settings.Timing.CaptureWaitSeconds, _captureWaitSecondsBox);
            _processTimeoutBox.Value = Clamp(_settings.Timing.ProcessTimeoutMs, _processTimeoutBox);

            _captureHotkeyBox.Text = _settings.KeyMappings.CaptureHotkey;
            _exitHotkeyBox.Text = _settings.KeyMappings.ExitHotkey;
            _lowLevelHotkeyBox.Checked = _settings.KeyMappings.UseLowLevelHotkeys;
            _keyboardDiagnosticsBox.Checked = _settings.KeyMappings.LogKeyboardDiagnostics;
            _convertHangulBox.Checked = _settings.KeyMappings.ConvertKoreanEnglishKey;
            _hangulInputModeBox.SelectedItem = _settings.KeyMappings.KoreanEnglishInputMode;
            _rightWindowsBox.Checked = _settings.KeyMappings.HandleRightWindowsKey;
            _convertEnterBox.Checked = _settings.KeyMappings.ConvertEnterToShiftEnter;
            _enterInputModeBox.SelectedItem = _settings.KeyMappings.EnterInputMode;
            _ignoreShiftSpaceBox.Checked = _settings.KeyMappings.IgnoreShiftSpace;
            UpdateManualAdbControls();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            try
            {
                SaveValues();
                _settingsService.Save(_settings);
                MessageBox.Show(
                    this,
                    "설정을 저장했습니다. ADB 및 Scrcpy 경로 변경은 프로그램을 다시 시작한 뒤 적용됩니다.",
                    "DEX Manager",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "설정을 저장하지 못했습니다.\r\n\r\n" + ex.Message, "DEX Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveValues()
        {
            _settings.Paths.AdbSelectionMode = _manualAdbBox.Checked
                ? AdbSelectionMode.Manual
                : AdbSelectionMode.Auto;
            _settings.Paths.AdbPath = _manualAdbPathBox.Text.Trim();
            _settings.Paths.ScrcpyPath = _scrcpyPathBox.Text.Trim();
            _settings.Paths.ScreenshotFolder = _screenshotFolderBox.Text.Trim();
            _settings.Paths.DeviceScreenshotFolder = _deviceScreenshotFolderBox.Text.Trim();
            _settings.Paths.LogFolder = _logFolderBox.Text.Trim();

            _settings.Features.StartWithWindows = _startWithWindowsBox.Checked;
            _settings.Features.StartMinimizedToTray = _startMinimizedBox.Checked;
            _settings.Features.ScrcpyWakeUpMode = (ScrcpyWakeUpMode)_wakeUpModeBox.SelectedItem;
            _settings.Features.AutoHideEnabled = _autoHideBox.Checked;
            _settings.Features.AutoStartDexOnDeviceConnected = _autoStartDexBox.Checked;
            _settings.Features.ResetVirtualDisplayOnStop = _resetDisplayOnStopBox.Checked;
            _settings.Features.DisableStayAwakeOnStop = _disableStayAwakeBox.Checked;
            _settings.Features.PushCaptureToDevice = _pushCaptureBox.Checked;

            _settings.Timing.DeviceMonitorIntervalMs = (int)_deviceMonitorIntervalBox.Value;
            _settings.Timing.DisconnectMonitorIntervalMs = (int)_disconnectMonitorIntervalBox.Value;
            _settings.Timing.ConnectedStartDelayMs = (int)_connectedStartDelayBox.Value;
            _settings.Timing.AdbWakeUpDelayMs = (int)_adbWakeUpDelayBox.Value;
            _settings.Timing.AutoHideIdleSeconds = (int)_autoHideSecondsBox.Value;
            _settings.Timing.CaptureWaitSeconds = (int)_captureWaitSecondsBox.Value;
            _settings.Timing.ProcessTimeoutMs = (int)_processTimeoutBox.Value;

            _settings.KeyMappings.CaptureHotkey = _captureHotkeyBox.Text.Trim();
            _settings.KeyMappings.ExitHotkey = _exitHotkeyBox.Text.Trim();
            _settings.KeyMappings.UseLowLevelHotkeys = _lowLevelHotkeyBox.Checked;
            _settings.KeyMappings.LogKeyboardDiagnostics = _keyboardDiagnosticsBox.Checked;
            _settings.KeyMappings.ConvertKoreanEnglishKey = _convertHangulBox.Checked;
            _settings.KeyMappings.KoreanEnglishInputMode = (KeyInputMode)_hangulInputModeBox.SelectedItem;
            _settings.KeyMappings.HandleRightWindowsKey = _rightWindowsBox.Checked;
            _settings.KeyMappings.ConvertEnterToShiftEnter = _convertEnterBox.Checked;
            _settings.KeyMappings.EnterInputMode = (KeyInputMode)_enterInputModeBox.SelectedItem;
            _settings.KeyMappings.IgnoreShiftSpace = _ignoreShiftSpaceBox.Checked;
        }

        private void UpdateManualAdbControls()
        {
            if (_manualAdbPanel != null)
                _manualAdbPanel.Enabled = _manualAdbBox != null && _manualAdbBox.Checked;
        }

        private string GetAdbVersionText()
        {
            try
            {
                var result = _adbService.GetVersion();
                if (!result.IsSuccess)
                    return "확인 실패: " + result.StandardError;
                var lines = result.StandardOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                return lines.Length == 0 ? "버전 출력 없음" : lines[0];
            }
            catch (Exception ex)
            {
                return "확인 실패: " + ex.Message;
            }
        }

        private static TableLayoutPanel CreateTable()
        {
            return new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Padding = new Padding(12)
            };
        }

        private static Control Wrap(Control content)
        {
            return new Panel { Dock = DockStyle.Fill, AutoScroll = true, Controls = { content } };
        }

        private static TextBox AddText(TableLayoutPanel table, string label)
        {
            var box = new TextBox { Dock = DockStyle.Fill };
            AddRow(table, label, box);
            return box;
        }

        private static TextBox AddPath(TableLayoutPanel table, string label, bool file)
        {
            TextBox box;
            var panel = CreatePathPanel(out box, file);
            AddRow(table, label, panel);
            return box;
        }

        private static Panel CreatePathPanel(out TextBox box, bool file)
        {
            var textBox = new TextBox { Dock = DockStyle.Fill };
            box = textBox;
            var button = new Button { Text = "찾아보기", Dock = DockStyle.Right, Width = 86 };
            button.Click += delegate
            {
                if (file)
                {
                    using (var dialog = new OpenFileDialog { Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*" })
                    {
                        if (dialog.ShowDialog() == DialogResult.OK) textBox.Text = dialog.FileName;
                    }
                }
                else
                {
                    using (var dialog = new FolderBrowserDialog())
                    {
                        if (dialog.ShowDialog() == DialogResult.OK) textBox.Text = dialog.SelectedPath;
                    }
                }
            };
            var panel = new Panel { Dock = DockStyle.Fill, Height = 28 };
            panel.Controls.Add(textBox);
            panel.Controls.Add(button);
            return panel;
        }

        private static NumericUpDown AddNumber(TableLayoutPanel table, string label, int min, int max)
        {
            var box = new NumericUpDown { Minimum = min, Maximum = max, Dock = DockStyle.Left, Width = 120 };
            AddRow(table, label, box);
            return box;
        }

        private static CheckBox AddCheck(TableLayoutPanel table, string label)
        {
            var box = new CheckBox { Text = label, AutoSize = true };
            AddRow(table, string.Empty, box);
            return box;
        }

        private static ComboBox AddCombo<T>(TableLayoutPanel table, string label)
        {
            var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Left, Width = 210 };
            foreach (var value in Enum.GetValues(typeof(T))) box.Items.Add(value);
            AddRow(table, label, box);
            return box;
        }

        private static void AddReadOnly(TableLayoutPanel table, string label, string value)
        {
            AddRow(table, label, new Label { AutoSize = true, MaximumSize = new Size(570, 0), Text = value });
        }

        private static void AddRow(TableLayoutPanel table, string label, Control control)
        {
            var row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 12, 8) }, 0, row);
            table.Controls.Add(control, 1, row);
            control.Margin = new Padding(3, 5, 3, 5);
        }

        private static decimal Clamp(int value, NumericUpDown box)
        {
            if (value < box.Minimum) return box.Minimum;
            if (value > box.Maximum) return box.Maximum;
            return value;
        }
    }
}

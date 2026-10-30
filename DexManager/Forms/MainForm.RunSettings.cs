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
        private void ResolutionBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingRunSettings) return;

            if (_resolutionSelectionInitialized && _resolutionWasCustom)
                StoreCurrentCustomResolution();

            if(IsResolutionEditorCommandSelected()){_resolutionBeforeDialogWidth=(int)_widthBox.Value;_resolutionBeforeDialogHeight=(int)_heightBox.Value;}
            ApplyResolutionSelection();
        }

        private void ApplyResolutionSelection()
        {
            var preset = _resolutionBox.SelectedItem as ResolutionPreset;
            var command = preset == null || preset.Width == 0;
            var custom = command || !AppSettings.IsPresetResolution(preset.Width,preset.Height);
            LayoutResolutionControls(custom);
            _widthBox.Enabled = custom;
            _heightBox.Enabled = custom;
            _widthBox.Visible = false;
            _heightBox.Visible = false;
            _widthLabel.Visible = false;
            _heightLabel.Visible = false;
            if (command)
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
            if (_displayCard == null) return;
            var width = (int)(_displayCard.Width / DesktopScale);
            var fieldWidth = (width - 64) / 2;
            var right = fieldWidth + 40;
            Place(_resolutionLabel, 24, 89, fieldWidth, 20);
            Place(_resolutionBox, 24, 115, fieldWidth, 40);
            Place(_dpiLabel, right, 89, fieldWidth, 20);
            Place(_dpiBox, right, 115, fieldWidth, 40);
            Place(_widthLabel, 24, 155, 35, 24);
            Place(_widthBox, 62, 151, Math.Max(54, fieldWidth - 38), 32);
            Place(_heightLabel, right, 155, 35, 24);
            Place(_heightBox, right + 38, 151, Math.Max(54, fieldWidth - 38), 32);
            var labelTop = 170;
            var fieldTop = 196;
            Place(_bitRateLabel, 24, labelTop, fieldWidth, 21);
            Place(_bitRateBox, 24, fieldTop, fieldWidth, 40);
            Place(_maxFpsLabel, right, labelTop, fieldWidth, 21);
            Place(_maxFpsBox, right, fieldTop, fieldWidth, 40);
        }
        private static int MeasureInlineLabel(Label label)
        {
            return UiText.MeasureText(
                label.Text,
                label.Font,
                Size.Empty,
                TextFormatFlags.NoPadding).Width;
        }

        private static int GetInlineLabelTop(
            Label label,
            int fieldTop)
        {
            var labelHeight = UiText.MeasureText(
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
            bool? previousScreenOff;
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
            var runSettings = GetSelectedDeviceRunSettings();
            if (_selectedMode == 0)
            {
                width = runSettings.VirtualDisplay.Width;
                height = runSettings.VirtualDisplay.Height;
                dpi = runSettings.VirtualDisplay.Dpi;
                bitRate = runSettings.Scrcpy.BitRate;
                maxFps = runSettings.Scrcpy.MaxFps;
                turnScreenOff = runSettings.Scrcpy.TurnScreenOff;
                previousScreenOff = runSettings.Scrcpy.TurnScreenOffBeforeHighFps;
                stayAwake = runSettings.Scrcpy.StayAwake;
                useHidKeyboard = runSettings.Scrcpy.UseHidKeyboard;
                useHidMouse = runSettings.Scrcpy.UseHidMouse;
                forceStopStartApp = runSettings.Scrcpy.ForceStopStartApp;
                flexDisplay = false;
                startAppPackage = runSettings.Scrcpy.StartAppPackage;
                startAppName = runSettings.Scrcpy.StartAppName;
                additionalArguments =
                    runSettings.Scrcpy.AdditionalArguments;
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
                previousScreenOff = slot.TurnScreenOffBeforeHighFps;
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
            _maxFpsBox.SelectedItem = maxFps == 120 ? _maxFpsBox.Items[2] : (object)(maxFps == 30 ? 30 : 60);
            _displayedMaxFps = maxFps;
            _turnScreenOffBeforeHighFps = previousScreenOff;
            _turnScreenOffBox.Enabled = !FrameRateRules.RequiresScreenOn(maxFps);
            _turnScreenOffBox.Checked = turnScreenOff;
            _stayAwakeBox.Checked = stayAwake;
            _useHidKeyboardBox.Checked = useHidKeyboard;
            _useHidMouseBox.Checked = useHidMouse;
            _forceStopAppBox.Checked = forceStopStartApp;
            _flexDisplayBox.Checked = flexDisplay;
            _hidePhonePreviewBox.Checked =
                runSettings.VirtualDisplay.HidePhonePreview;
            _additionalArgumentsBox.Text = additionalArguments;
            RefreshAppItems(startAppPackage, startAppName);
            RebuildResolutionOptions(width, height);
            _resolutionBox.SelectedIndex = FindResolutionPresetIndex(
                width,
                height);
            ApplyResolutionSelection();
            _loadingRunSettings = false;
            if (!_appProfileApplying && _selectedMode > 0)
                ApplySelectedAppProfileIfAvailable();
            UpdatePhonePreviewControls();
            UpdateApplySettingsLink();
            UpdateAppProfileControls();
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

        private void RebuildResolutionOptions(
            int activeWidth,
            int activeHeight)
        {
            _resolutionBox.Items.Clear();
            _resolutionBox.Items.Add(new ResolutionPreset(
                "1600 x 900", 1600, 900));
            _resolutionBox.Items.Add(new ResolutionPreset(
                "1920 x 1080", 1920, 1080));
            _resolutionBox.Items.Add(new ResolutionPreset(
                "3840 x 2160 (4K)", 3840, 2160));

            _resolutionBox.Items.Add(new ResolutionPreset(LocalizationService.IsKorean?"사용자 지정 해상도":"Custom resolutions",-1,-1));
            var recent = _settings.RecentCustomResolutions ??
                new List<RecentCustomResolutionSettings>();
            var activeIsAvailable = AppSettings.IsPresetResolution(
                activeWidth,
                activeHeight);
            foreach (var resolution in recent)
            {
                if (resolution == null) continue;
                _resolutionBox.Items.Add(new ResolutionPreset(
                    FormatResolution(resolution.Width, resolution.Height),
                    resolution.Width,
                    resolution.Height));
                if (resolution.Width == activeWidth &&
                    resolution.Height == activeHeight)
                {
                    activeIsAvailable = true;
                }
            }

            if (!activeIsAvailable &&
                activeWidth >= 320 && activeWidth <= 4096 &&
                activeHeight >= 240 && activeHeight <= 4096)
            {
                // Keep an active app-profile resolution selectable even when
                // it has aged out of the persisted five-entry recent list.
                _resolutionBox.Items.Add(new ResolutionPreset(
                    FormatResolution(activeWidth, activeHeight),
                    activeWidth,
                    activeHeight));
            }

            _resolutionBox.Items.Add(new ResolutionPreset(LocalizationService.Get("Main.Custom")+"...",0,0));
        }

        private static string FormatResolution(int width, int height)
        {
            return width.ToString() + " x " + height.ToString();
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
                var launchOrchestrator = _orchestrator;
                var launchSettings = CaptureSelectedRunSettings();
                var serial = launchOrchestrator.CurrentSession == null ? GetSelectedDeviceSerial() : launchOrchestrator.CurrentSession.Serial;
                if (launchOrchestrator.IsRunning && !await ConfirmExperimentalFrameRateAsync(launchSettings.Scrcpy.MaxFps)) return;
                if (string.IsNullOrWhiteSpace(serial) ||
                    !_adbService.IsAuthorizedDeviceConnected(serial))
                {
                    _logService.Info(LocalizationService.Get(
                        "Log.Main.SettingsDeferredNoDevice"));
                    DesktopDialogs.Show(
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
                    applied = await launchOrchestrator.ApplyRuntimeSettingsAsync(launchSettings, true);
                }
                finally
                {
                    EndPhoneScreenWakeSuppression();
                }
                if (!applied)
                {
                    DesktopDialogs.Show(
                        this,
                        LocalizationService.Get("Main.ApplyDeferred"),
                        LocalizationService.Get("App.Name"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                UpdateRunningState();
                _modeSettingsDirty[0] = false;
                DesktopDialogs.Show(
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
                DesktopDialogs.Show(
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
            var serial = GetSelectedDeviceSerial();
            if (string.IsNullOrWhiteSpace(serial) ||
                !_adbService.IsAuthorizedDeviceConnected(serial))
            {
                DesktopDialogs.Show(
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
                DesktopDialogs.Show(
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
            var settings = AppSettings.CloneSingleWindowSlot(GetSingleWindowSettings(slot));
            var launchService = _singleWindowService;
            if (!await ConfirmExperimentalFrameRateAsync(settings.MaxFps)) return;
            BeginPhoneScreenWakeSuppression();
            try
            {
                await Task.Run(delegate
                {
                    launchService.Restart(slot, settings, true);
                });
            }
            finally
            {
                EndPhoneScreenWakeSuppression();
            }
            UpdateRunningState();
            _modeSettingsDirty[slot] = false;
            DesktopDialogs.Show(
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
                var serial = GetSelectedDeviceSerial();
                var deviceIdentity = _selectedDeviceIdentity ?? string.Empty;
                var sourceDevice = _selectedDeviceContext == null
                    ? string.Empty : GetContextDisplayName(_selectedDeviceContext);
                var scrcpyService = _scrcpyService;
                if (string.IsNullOrWhiteSpace(serial))
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Dex.NoAuthorizedDevice"));
                var apps = await Task.Run(delegate
                {
                    return scrcpyService.ListApps(serial);
                });
                if (_exitInProgress || IsDisposed || Disposing) return;
                var count = _deviceAppCatalog.Replace(deviceIdentity, serial, apps);
                if (!AppListResultMatchesSelection(
                    deviceIdentity,
                    serial,
                    _selectedDeviceIdentity,
                    GetSelectedDeviceSerial()))
                {
                    _logService.Info(LocalizationService.Get(
                        "Log.Main.StaleAppListDiscarded"));
                    ShowAppsLoadedNotice(count, sourceDevice);
                    return;
                }

                var selectedPackage = GetSelectedAppPackage();
                var selectedName = GetSelectedAppName(selectedPackage);

                RefreshAppItems(selectedPackage, selectedName);
                if (!ApplySelectedAppProfileIfAvailable())
                    SaveSelectedAppIdentity();
                _logService.Info(LocalizationService.Get(
                    "Log.Main.AppListDisplayed"));
                ShowAppsLoadedNotice(count, null);
            }
            catch (Exception ex)
            {
                if (_exitInProgress || IsDisposed || Disposing) return;
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.AppListLoadFailed"),
                    ex);
                DesktopDialogs.Show(
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
                if (!_exitInProgress && !IsDisposed && !Disposing)
                {
                    _loadAppsButton.Text = LocalizationService.Get("Main.LoadApps");
                    _loadAppsButton.Enabled = true;
                }
            }
        }

        private static bool AppListResultMatchesSelection(
            string requestedIdentity,
            string requestedSerial,
            string currentIdentity,
            string currentSerial)
        {
            var requested = (requestedIdentity ?? string.Empty).Trim();
            var current = (currentIdentity ?? string.Empty).Trim();
            if (requested.Length > 0 && current.Length > 0)
            {
                return string.Equals(
                    requested,
                    current,
                    StringComparison.OrdinalIgnoreCase);
            }
            return !string.IsNullOrWhiteSpace(requestedSerial) &&
                string.Equals(
                    requestedSerial.Trim(),
                    (currentSerial ?? string.Empty).Trim(),
                    StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyRunSettings(bool showMessage)
        {
            var rememberCustomResolution = IsCustomResolutionSelected();
            var customWidth = (int)_widthBox.Value;
            var customHeight = (int)_heightBox.Value;
            var bitRate = ((int)_bitRateBox.Value).ToString() + "M";
            ScrcpyService.ValidateAdditionalArguments(
                _additionalArgumentsBox.Text);
            _settingsService.UpdateAndSave(_settings, delegate(
                AppSettings candidate)
            {
                if (rememberCustomResolution)
                {
                    candidate.RememberCustomResolution(
                        customWidth,
                        customHeight);
                }
                var runSettings = GetDeviceRunSettings(
                    candidate,
                    _selectedDeviceIdentity);
                if (_selectedMode == 0)
                {
                    runSettings.VirtualDisplay.Width = (int)_widthBox.Value;
                    runSettings.VirtualDisplay.Height = (int)_heightBox.Value;
                    if (IsCustomResolutionSelected())
                    {
                        runSettings.VirtualDisplay.CustomWidth =
                            (int)_widthBox.Value;
                        runSettings.VirtualDisplay.CustomHeight =
                            (int)_heightBox.Value;
                    }
                    runSettings.VirtualDisplay.Dpi = (int)_dpiBox.Value;
                    runSettings.VirtualDisplay.ReuseExistingDisplay = true;
                    runSettings.VirtualDisplay.HidePhonePreview =
                        _hidePhonePreviewBox.Checked;
                    runSettings.Scrcpy.BitRate = bitRate;
                    runSettings.Scrcpy.MaxFps = GetSelectedMaxFps();
                    runSettings.Scrcpy.TurnScreenOff =
                        _turnScreenOffBox.Checked;
                    runSettings.Scrcpy.TurnScreenOffBeforeHighFps = _turnScreenOffBeforeHighFps;
                    runSettings.Scrcpy.StayAwake = _stayAwakeBox.Checked;
                    runSettings.Scrcpy.UseHidKeyboard =
                        _useHidKeyboardBox.Checked;
                    runSettings.Scrcpy.UseHidMouse =
                        _useHidMouseBox.Checked;
                    runSettings.Scrcpy.ForceStopStartApp =
                        _forceStopAppBox.Checked;
                    var selectedPackage = GetSelectedAppPackage();
                    var selectedName = GetSelectedAppName(selectedPackage);
                    runSettings.Scrcpy.StartAppPackage = selectedPackage;
                    runSettings.Scrcpy.StartAppName = selectedName;
                    runSettings.Scrcpy.AdditionalArguments =
                        _additionalArgumentsBox.Text.Trim();
                }
                else
                {
                    var slot = GetSingleWindowSettings(
                        runSettings,
                        _selectedMode);
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
                    slot.TurnScreenOffBeforeHighFps = _turnScreenOffBeforeHighFps;
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
            });
            if (rememberCustomResolution)
                RefreshRecentResolutionOptions();
            _logService.Info(
                _selectedMode == 0
                    ? LocalizationService.Get(
                        "Log.Main.DexSettingsSaved")
                    : LocalizationService.Format(
                        "Log.Main.SingleWindowSettingsSaved",
                        _selectedMode));
            if (showMessage)
            {
                DesktopDialogs.Show(
                    this,
                    LocalizationService.Get("Main.ApplyDeferred"),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void RefreshRecentResolutionOptions()
        {
            var wasLoading = _loadingRunSettings;
            _loadingRunSettings = true;
            try
            {
                RebuildResolutionOptions(
                    (int)_widthBox.Value,
                    (int)_heightBox.Value);
                _resolutionBox.SelectedIndex = FindResolutionPresetIndex((int)_widthBox.Value,(int)_heightBox.Value);
                _resolutionBox.Invalidate();
                _resolutionWasCustom = IsCustomResolutionSelected();
                _resolutionSelectionInitialized = true;
            }
            finally
            {
                _loadingRunSettings = wasLoading;
            }
        }

        private string GetSelectedAppPackage()
        {
            var app = _startAppBox.SelectedItem as ScrcpyAppInfo;
            if (app != null) return app.PackageName ?? string.Empty;
            return string.Empty;
        }

        private void DpiBox_MinimumValueRejected(
            object sender,
            EventArgs e)
        {
            if (_exitInProgress || IsDisposed || Disposing) return;
            DesktopDialogs.Show(
                this,
                LocalizationService.Format(
                    "Main.DpiMinimum",
                    (int)_dpiBox.Minimum),
                LocalizationService.Get("App.Name"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ResolutionBox_MaximumValueRejected(
            object sender,
            EventArgs e)
        {
            if (_exitInProgress || IsDisposed || Disposing) return;
            var control = sender as ThemedNumberControl;
            var fieldName = ReferenceEquals(control, _heightBox)
                ? LocalizationService.Get("Main.Height")
                : LocalizationService.Get("Main.Width");
            DesktopDialogs.Show(
                this,
                LocalizationService.Format(
                    "Main.ResolutionMaximum",
                    fieldName,
                    control == null ? 4096 : (int)control.Maximum),
                LocalizationService.Get("App.Name"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private string GetSelectedAppName(string packageName)
        {
            var app = _startAppBox.SelectedItem as ScrcpyAppInfo;
            if (app != null && !string.IsNullOrWhiteSpace(app.PackageName))
                return app.Name ?? app.PackageName;

            if (_selectedMode == 0)
            {
                var scrcpy = GetSelectedDeviceRunSettings().Scrcpy;
                return string.Equals(
                    scrcpy.StartAppPackage,
                    packageName,
                    StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(scrcpy.StartAppName)
                    ? scrcpy.StartAppName
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
                : (_maxFpsBox.SelectedItem is ExperimentalFpsOption ? 120 : 60);
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
        private sealed class ResolutionPreset : IDropDownGroupItem
        {
            public ResolutionPreset(string text, int width, int height)
            {
                Text = text;
                Width = width;
                Height = height;
            }

            public bool StartsGroup {get {return Width<0;}}
            public bool IsSelectable {get {return Width>=0;}}
            public string Text { get; private set; }
            public int Width { get; private set; }
            public int Height { get; private set; }
            public override string ToString() { return Text; }
        }
    }
}

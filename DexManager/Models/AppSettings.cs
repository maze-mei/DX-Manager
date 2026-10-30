using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;

namespace DexManager.Models
{
    [DataContract]
    public sealed class AppSettings
    {
        public const int CurrentSchemaVersion = 28;
        public const int MaximumRecentCustomResolutions = 5;

        [DataMember(Order = 1)] public int SchemaVersion { get; set; }
        [DataMember(Order = 2)] public PathSettings Paths { get; set; }
        [DataMember(Order = 3)] public VirtualDisplaySettings VirtualDisplay { get; set; }
        [DataMember(Order = 4)] public ScrcpySettings Scrcpy { get; set; }
        [DataMember(Order = 5)] public TimingSettings Timing { get; set; }
        [DataMember(Order = 6)] public FeatureSettings Features { get; set; }
        [DataMember(Order = 7)] public KeyMappingSettings KeyMappings { get; set; }
        [DataMember(Order = 8)] public LastSuccessSettings LastSuccess { get; set; }
        [DataMember(Order = 9)] public List<SingleWindowSlotSettings> SingleWindowSlots { get; set; }
        [DataMember(Order = 10)] public ConnectionSettings Connection { get; set; }
        [DataMember(Order = 11)] public AppLanguage Language { get; set; }
        [DataMember(Order = 12)] public AppTheme Theme { get; set; }
        [DataMember(Order = 13)]
        public List<RememberedAppSettings> RememberedApps { get; set; }
        [DataMember(Order = 14)]
        public List<SingleWindowAppProfile> SingleWindowAppProfiles { get; set; }
        [DataMember(Order = 15)]
        public List<DeviceRunSettingsProfile> DeviceRunSettingsProfiles
        {
            get;
            set;
        }
        [DataMember(Order = 16)]
        public List<DeviceWirelessConnectionProfile>
            DeviceWirelessConnectionProfiles { get; set; }
        [DataMember(Order = 17)]
        public List<RecentCustomResolutionSettings> RecentCustomResolutions
        {
            get;
            set;
        }

        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                SchemaVersion = CurrentSchemaVersion,
                Language = AppLanguage.Auto,
                Theme = AppTheme.Auto,
                RememberedApps = new List<RememberedAppSettings>(),
                SingleWindowAppProfiles =
                    new List<SingleWindowAppProfile>(),
                DeviceRunSettingsProfiles =
                    new List<DeviceRunSettingsProfile>(),
                DeviceWirelessConnectionProfiles =
                    new List<DeviceWirelessConnectionProfile>(),
                RecentCustomResolutions =
                    new List<RecentCustomResolutionSettings>(),
                Paths = new PathSettings
                {
                    AdbPath = string.Empty,
                    AdbSelectionMode = AdbSelectionMode.Auto,
                    Win7AdbPath = @"tools\adb\legacy\adb.exe",
                    ScrcpyPath = @"tools\scrcpy\scrcpy.exe",
                    ScreenshotFolder = "screenshot",
                    DeviceScreenshotFolder = "/sdcard/DCIM/DeX Screenshots",
                    LogFolder = "logs",
                    FileTransferTargetFolder = "/sdcard/Download/",
                    PhoneToPcReceiveFolder = System.IO.Path.Combine(
                        System.Environment.GetFolderPath(
                            System.Environment.SpecialFolder.UserProfile),
                        "Downloads",
                        "DX Manager")
                },
                VirtualDisplay = new VirtualDisplaySettings
                {
                    Width = 1600,
                    Height = 900,
                    Dpi = 150,
                    Suffix = "hdmi",
                    ReuseExistingDisplay = true,
                    CustomWidth = 1600,
                    CustomHeight = 900,
                    HidePhonePreview = false
                },
                Scrcpy = new ScrcpySettings
                {
                    BitRate = "8M",
                    MaxFps = 60,
                    WindowTitle = "DX Manager - DeX Station",
                    TurnScreenOff = true,
                    UseHidKeyboard = true,
                    UseHidMouse = true,
                    ForceStopStartApp = false,
                    StartAppPackage = string.Empty,
                    StartAppName = string.Empty,
                    AdditionalArguments = string.Empty,
                    StayAwake = true
                },
                Timing = new TimingSettings
                {
                    DeviceMonitorIntervalMs = 1000,
                    DisconnectMonitorIntervalMs = 2000,
                    ConnectedStartDelayMs = 1000,
                    AdbWakeUpDelayMs = 3000,
                    AutoHideIdleSeconds = 30,
                    CaptureWaitSeconds = 5,
                    ProcessTimeoutMs = 15000,
                    VirtualDisplayDetectionTimeoutMs = 3000
                },
                Features = new FeatureSettings
                {
                    StartWithWindows = false,
                    StartMinimizedToTray = false,
                    RegisterAdbPathAutomatically = false,
                    ScrcpyWakeUpMode = ScrcpyWakeUpMode.OnAdbFailure,
                    AutoHideEnabled = false,
                    PushCaptureToDevice = true,
                    ResetVirtualDisplayOnStop = true,
                    DisableStayAwakeOnStop = true,
                    AutoStartDexOnDeviceConnected = false,
                    ShowConnectedDeviceInfo = true,
                    ManagedFileTransferEnabled = true,
                    MiniControlBarEnabled = true,
                    MiniControlBarSide = MiniControlBarSide.Right,
                    PhoneToPcTransferEnabled = true
                },
                KeyMappings = new KeyMappingSettings
                {
                    CaptureHotkey = "F8",
                    ExitHotkey = "LeftAlt+F8",
                    UseLowLevelHotkeys = true,
                    LogKeyboardDiagnostics = false,
                    ConvertKoreanEnglishKey = true,
                    KoreanEnglishInputMode = KeyInputMode.SendInputScanCode,
                    HandleRightWindowsKey = true,
                    ConvertEnterToShiftEnter = false,
                    EnterInputMode = KeyInputMode.SendInputScanCode,
                    IgnoreShiftSpace = false
                },
                LastSuccess = new LastSuccessSettings(),
                SingleWindowSlots = CreateDefaultSingleWindowSlots(),
                Connection = new ConnectionSettings
                {
                    Mode = AdbConnectionMode.Usb,
                    WirelessHost = string.Empty,
                    WirelessPort = 5555,
                    AutoReconnect = true
                }
            };
        }

        public void EnsureDefaults()
        {
            var defaults = CreateDefault();

            if (Paths == null) Paths = defaults.Paths;
            if (VirtualDisplay == null) VirtualDisplay = defaults.VirtualDisplay;
            if (Scrcpy == null) Scrcpy = defaults.Scrcpy;
            if (Timing == null) Timing = defaults.Timing;
            if (Features == null) Features = defaults.Features;
            if (KeyMappings == null) KeyMappings = defaults.KeyMappings;
            if (LastSuccess == null) LastSuccess = defaults.LastSuccess;
            if (Connection == null) Connection = defaults.Connection;
            if (RememberedApps == null)
                RememberedApps = new List<RememberedAppSettings>();
            if (SingleWindowAppProfiles == null)
                SingleWindowAppProfiles =
                    new List<SingleWindowAppProfile>();
            if (DeviceRunSettingsProfiles == null)
                DeviceRunSettingsProfiles =
                    new List<DeviceRunSettingsProfile>();
            if (DeviceWirelessConnectionProfiles == null)
                DeviceWirelessConnectionProfiles =
                    new List<DeviceWirelessConnectionProfile>();
            if (RecentCustomResolutions == null)
                RecentCustomResolutions =
                    new List<RecentCustomResolutionSettings>();
            if (SingleWindowSlots == null)
                SingleWindowSlots = new List<SingleWindowSlotSettings>();
            while (SingleWindowSlots.Count < 3)
            {
                SingleWindowSlots.Add(CreateDefaultSingleWindowSlot(
                    SingleWindowSlots.Count + 1));
            }
            var oldSchemaVersion = SchemaVersion;
            if (SchemaVersion <= 0) SchemaVersion = defaults.SchemaVersion;

            if (string.IsNullOrWhiteSpace(KeyMappings.CaptureHotkey))
                KeyMappings.CaptureHotkey = defaults.KeyMappings.CaptureHotkey;
            if (string.IsNullOrWhiteSpace(KeyMappings.ExitHotkey))
                KeyMappings.ExitHotkey = defaults.KeyMappings.ExitHotkey;
            if (oldSchemaVersion < 2)
            {
                KeyMappings.UseLowLevelHotkeys = defaults.KeyMappings.UseLowLevelHotkeys;
                KeyMappings.LogKeyboardDiagnostics = defaults.KeyMappings.LogKeyboardDiagnostics;
                KeyMappings.KoreanEnglishInputMode = defaults.KeyMappings.KoreanEnglishInputMode;
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 3)
            {
                KeyMappings.EnterInputMode = defaults.KeyMappings.EnterInputMode;
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 4)
            {
                KeyMappings.ConvertEnterToShiftEnter =
                    defaults.KeyMappings.ConvertEnterToShiftEnter;
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 5)
            {
                KeyMappings.ConvertEnterToShiftEnter =
                    defaults.KeyMappings.ConvertEnterToShiftEnter;
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 6)
            {
                Paths.AdbSelectionMode = AdbSelectionMode.Auto;
                Paths.Win7AdbPath = defaults.Paths.Win7AdbPath;
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 7)
            {
                Scrcpy.StayAwake = HasStayAwakeArgument(
                    Scrcpy.AdditionalArguments) || defaults.Scrcpy.StayAwake;
                Scrcpy.AdditionalArguments = RemoveStayAwakeArgument(
                    Scrcpy.AdditionalArguments);
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 8)
            {
                Scrcpy.ForceStopStartApp = defaults.Scrcpy.ForceStopStartApp;
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 9)
            {
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 10)
            {
                VirtualDisplay.CustomWidth = VirtualDisplay.Width;
                VirtualDisplay.CustomHeight = VirtualDisplay.Height;
                foreach (var slot in SingleWindowSlots)
                {
                    if (slot == null) continue;
                    slot.CustomWidth = slot.Width;
                    slot.CustomHeight = slot.Height;
                }
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 11)
            {
                if (string.IsNullOrWhiteSpace(Scrcpy.StartAppName))
                    Scrcpy.StartAppName = Scrcpy.StartAppPackage;
                foreach (var slot in SingleWindowSlots)
                {
                    if (slot != null &&
                        string.IsNullOrWhiteSpace(slot.StartAppName))
                    {
                        slot.StartAppName = slot.StartAppPackage;
                    }
                }
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 12)
            {
                Scrcpy.StayAwake = HasStayAwakeArgument(
                    Scrcpy.AdditionalArguments) || Scrcpy.StayAwake;
                Scrcpy.AdditionalArguments = RemoveStayAwakeArgument(
                    Scrcpy.AdditionalArguments);
                foreach (var slot in SingleWindowSlots)
                {
                    if (slot == null) continue;
                    slot.StayAwake = HasStayAwakeArgument(
                        slot.AdditionalArguments) || slot.StayAwake;
                    slot.FlexDisplay = HasFlexDisplayArgument(
                        slot.AdditionalArguments);
                    slot.AdditionalArguments = RemoveStayAwakeArgument(
                        slot.AdditionalArguments);
                    slot.AdditionalArguments = RemoveFlexDisplayArgument(
                        slot.AdditionalArguments);
                }
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 13)
            {
                Connection = defaults.Connection;
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 14)
            {
                Language = defaults.Language;
                if (string.Equals(
                    Scrcpy.WindowTitle,
                    "DEX Manager - Scrcpy",
                    System.StringComparison.OrdinalIgnoreCase))
                {
                    Scrcpy.WindowTitle = defaults.Scrcpy.WindowTitle;
                }
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 15)
            {
                Theme = defaults.Theme;
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 16)
            {
                AddRememberedApp(
                    RememberedApps,
                    Scrcpy.StartAppPackage,
                    Scrcpy.StartAppName);
                foreach (var slot in SingleWindowSlots)
                {
                    if (slot == null) continue;
                    AddRememberedApp(
                        RememberedApps,
                        slot.StartAppPackage,
                        slot.StartAppName);
                }
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 17)
            {
                Features.ShowConnectedDeviceInfo =
                    defaults.Features.ShowConnectedDeviceInfo;
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 18)
            {
                Timing.VirtualDisplayDetectionTimeoutMs = NormalizeRange(
                    Timing.ConnectedStartDelayMs,
                    1000,
                    60000,
                    defaults.Timing.VirtualDisplayDetectionTimeoutMs);
                Timing.ConnectedStartDelayMs =
                    defaults.Timing.ConnectedStartDelayMs;
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 19)
            {
                Features.ManagedFileTransferEnabled =
                    defaults.Features.ManagedFileTransferEnabled;
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 20)
            {
                Paths.FileTransferTargetFolder =
                    defaults.Paths.FileTransferTargetFolder;
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 21)
            {
                Features.MiniControlBarEnabled =
                    defaults.Features.MiniControlBarEnabled;
                Features.MiniControlBarSide =
                    defaults.Features.MiniControlBarSide;
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 22)
            {
                SingleWindowAppProfiles =
                    new List<SingleWindowAppProfile>();
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 23)
            {
                Paths.PhoneToPcReceiveFolder =
                    defaults.Paths.PhoneToPcReceiveFolder;
                Features.PhoneToPcTransferEnabled =
                    defaults.Features.PhoneToPcTransferEnabled;
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 24)
            {
                DeviceRunSettingsProfiles =
                    new List<DeviceRunSettingsProfile>();
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < 25)
            {
                DeviceWirelessConnectionProfiles =
                    new List<DeviceWirelessConnectionProfile>();
                SchemaVersion = defaults.SchemaVersion;
            }
            if (oldSchemaVersion < CurrentSchemaVersion)
                SchemaVersion = defaults.SchemaVersion;
            VirtualDisplay.Width = NormalizeRange(
                VirtualDisplay.Width,
                320,
                4096,
                defaults.VirtualDisplay.Width);
            VirtualDisplay.Height = NormalizeRange(
                VirtualDisplay.Height,
                240,
                4096,
                defaults.VirtualDisplay.Height);
            VirtualDisplay.CustomWidth = NormalizeRange(
                VirtualDisplay.CustomWidth,
                320,
                4096,
                VirtualDisplay.Width);
            VirtualDisplay.CustomHeight = NormalizeRange(
                VirtualDisplay.CustomHeight,
                240,
                4096,
                VirtualDisplay.Height);
            VirtualDisplay.Dpi = System.Math.Max(
                120,
                System.Math.Min(640, VirtualDisplay.Dpi));
            VirtualDisplay.ReuseExistingDisplay = true;
            Features.ResetVirtualDisplayOnStop = true;
            for (var slotIndex = 0;
                slotIndex < SingleWindowSlots.Count;
                slotIndex++)
            {
                var slot = SingleWindowSlots[slotIndex];
                if (slot == null)
                {
                    slot = CreateDefaultSingleWindowSlot(slotIndex + 1);
                    SingleWindowSlots[slotIndex] = slot;
                }
                var defaultSlot = slotIndex < defaults.SingleWindowSlots.Count
                    ? defaults.SingleWindowSlots[slotIndex]
                    : CreateDefaultSingleWindowSlot(slotIndex + 1);
                slot.Width = NormalizeRange(
                    slot.Width,
                    320,
                    4096,
                    defaultSlot.Width);
                slot.Height = NormalizeRange(
                    slot.Height,
                    240,
                    4096,
                    defaultSlot.Height);
                slot.CustomWidth = NormalizeRange(
                    slot.CustomWidth,
                    320,
                    4096,
                    slot.Width);
                slot.CustomHeight = NormalizeRange(
                    slot.CustomHeight,
                    240,
                    4096,
                    slot.Height);
                slot.Dpi = System.Math.Max(
                    120,
                    System.Math.Min(640, slot.Dpi));
            }
            NormalizeSingleWindowAppProfiles(
                SingleWindowAppProfiles,
                defaults.SingleWindowSlots[0]);
            NormalizeRecentCustomResolutions();
            NormalizeDeviceRunSettingsProfiles(defaults);
            NormalizeDeviceWirelessConnectionProfiles(defaults);
            FrameRateRules.Normalize(Scrcpy);
            foreach (var slot in SingleWindowSlots) FrameRateRules.Normalize(slot);
            foreach (var profile in SingleWindowAppProfiles) FrameRateRules.Normalize(profile);
            foreach (var device in DeviceRunSettingsProfiles)
            {
                FrameRateRules.Normalize(device.Scrcpy);
                foreach (var slot in device.SingleWindowSlots) FrameRateRules.Normalize(slot);
                foreach (var profile in device.SingleWindowAppProfiles) FrameRateRules.Normalize(profile);
            }
            if (string.IsNullOrWhiteSpace(Paths.Win7AdbPath))
                Paths.Win7AdbPath = defaults.Paths.Win7AdbPath;
            if (string.IsNullOrWhiteSpace(Paths.ScrcpyPath))
                Paths.ScrcpyPath = defaults.Paths.ScrcpyPath;
            if (string.IsNullOrWhiteSpace(Paths.ScreenshotFolder))
                Paths.ScreenshotFolder = defaults.Paths.ScreenshotFolder;
            if (string.IsNullOrWhiteSpace(Paths.DeviceScreenshotFolder))
                Paths.DeviceScreenshotFolder =
                    defaults.Paths.DeviceScreenshotFolder;
            if (string.IsNullOrWhiteSpace(Paths.LogFolder))
                Paths.LogFolder = defaults.Paths.LogFolder;
            Paths.FileTransferTargetFolder = NormalizeSharedStorageFolder(
                Paths.FileTransferTargetFolder,
                defaults.Paths.FileTransferTargetFolder);
            if (string.IsNullOrWhiteSpace(Paths.PhoneToPcReceiveFolder))
            {
                Paths.PhoneToPcReceiveFolder =
                    defaults.Paths.PhoneToPcReceiveFolder;
            }
            if (!System.Enum.IsDefined(
                typeof(AdbSelectionMode),
                Paths.AdbSelectionMode))
            {
                Paths.AdbSelectionMode = AdbSelectionMode.Auto;
            }
            if (Paths.AdbSelectionMode == AdbSelectionMode.Manual &&
                string.IsNullOrWhiteSpace(Paths.AdbPath))
            {
                Paths.AdbSelectionMode = AdbSelectionMode.Auto;
            }
            if (!System.Enum.IsDefined(
                typeof(AdbConnectionMode),
                Connection.Mode))
            {
                Connection.Mode = AdbConnectionMode.Usb;
            }
            if (!System.Enum.IsDefined(typeof(AppLanguage), Language))
                Language = defaults.Language;
            if (!System.Enum.IsDefined(typeof(AppTheme), Theme))
                Theme = defaults.Theme;
            if (!System.Enum.IsDefined(
                typeof(MiniControlBarSide),
                Features.MiniControlBarSide))
            {
                Features.MiniControlBarSide =
                    defaults.Features.MiniControlBarSide;
            }
            if (!System.Enum.IsDefined(
                typeof(ScrcpyWakeUpMode),
                Features.ScrcpyWakeUpMode))
            {
                Features.ScrcpyWakeUpMode =
                    defaults.Features.ScrcpyWakeUpMode;
            }
            if (Connection.WirelessPort < 1 ||
                Connection.WirelessPort > 65535)
            {
                Connection.WirelessPort = 5555;
            }
            if (Connection.Mode == AdbConnectionMode.Wireless &&
                !IsValidWirelessHost(Connection.WirelessHost))
            {
                Connection.Mode = AdbConnectionMode.Usb;
            }
            if (!System.Enum.IsDefined(typeof(KeyInputMode), KeyMappings.KoreanEnglishInputMode))
                KeyMappings.KoreanEnglishInputMode = defaults.KeyMappings.KoreanEnglishInputMode;
            if (!System.Enum.IsDefined(typeof(KeyInputMode), KeyMappings.EnterInputMode))
                KeyMappings.EnterInputMode = defaults.KeyMappings.EnterInputMode;
            if (string.Equals(
                KeyMappings.CaptureHotkey,
                KeyMappings.ExitHotkey,
                System.StringComparison.OrdinalIgnoreCase))
            {
                KeyMappings.ExitHotkey = defaults.KeyMappings.ExitHotkey;
            }

            Timing.DeviceMonitorIntervalMs = NormalizeRange(
                Timing.DeviceMonitorIntervalMs,
                1000,
                60000,
                defaults.Timing.DeviceMonitorIntervalMs);
            Timing.DisconnectMonitorIntervalMs = NormalizeRange(
                Timing.DisconnectMonitorIntervalMs,
                1000,
                60000,
                defaults.Timing.DisconnectMonitorIntervalMs);
            Timing.ConnectedStartDelayMs = NormalizeRange(
                Timing.ConnectedStartDelayMs,
                0,
                60000,
                defaults.Timing.ConnectedStartDelayMs);
            Timing.AdbWakeUpDelayMs = NormalizeRange(
                Timing.AdbWakeUpDelayMs,
                0,
                60000,
                defaults.Timing.AdbWakeUpDelayMs);
            Timing.AutoHideIdleSeconds = NormalizeRange(
                Timing.AutoHideIdleSeconds,
                1,
                3600,
                defaults.Timing.AutoHideIdleSeconds);
            Timing.CaptureWaitSeconds = NormalizeRange(
                Timing.CaptureWaitSeconds,
                1,
                60,
                defaults.Timing.CaptureWaitSeconds);
            Timing.ProcessTimeoutMs = NormalizeRange(
                Timing.ProcessTimeoutMs,
                1000,
                120000,
                defaults.Timing.ProcessTimeoutMs);
            Timing.VirtualDisplayDetectionTimeoutMs = NormalizeRange(
                Timing.VirtualDisplayDetectionTimeoutMs,
                1000,
                60000,
                defaults.Timing.VirtualDisplayDetectionTimeoutMs);
        }

        internal bool RememberCustomResolution(int width, int height)
        {
            if (width < 320 || width > 4096 ||
                height < 240 || height > 4096 ||
                IsPresetResolution(width, height))
            {
                return false;
            }
            if (RecentCustomResolutions == null)
                RecentCustomResolutions =
                    new List<RecentCustomResolutionSettings>();

            var existingIndex = RecentCustomResolutions.FindIndex(item =>
                item != null && item.Width == width && item.Height == height);
            if (existingIndex == 0) return false;
            if (existingIndex > 0)
                RecentCustomResolutions.RemoveAt(existingIndex);

            RecentCustomResolutions.Insert(
                0,
                new RecentCustomResolutionSettings
                {
                    Width = width,
                    Height = height
                });
            if (RecentCustomResolutions.Count > MaximumRecentCustomResolutions)
            {
                RecentCustomResolutions.RemoveRange(
                    MaximumRecentCustomResolutions,
                    RecentCustomResolutions.Count -
                        MaximumRecentCustomResolutions);
            }
            return true;
        }

        internal static bool IsPresetResolution(int width, int height)
        {
            return (width == 1600 && height == 900) ||
                (width == 1920 && height == 1080) ||
                (width == 3840 && height == 2160);
        }

        internal static bool SingleWindowAppProfileMatchesSlot(
            SingleWindowAppProfile profile,
            SingleWindowSlotSettings slot,
            string packageName,
            string appName)
        {
            if (profile == null || slot == null) return false;
            var resolvedAppName = string.IsNullOrWhiteSpace(appName)
                ? profile.AppName ?? string.Empty
                : appName;
            return slot.Width == profile.Width &&
                slot.Height == profile.Height &&
                slot.CustomWidth == profile.CustomWidth &&
                slot.CustomHeight == profile.CustomHeight &&
                slot.Dpi == profile.Dpi &&
                string.Equals(slot.BitRate, profile.BitRate,
                    System.StringComparison.OrdinalIgnoreCase) &&
                slot.MaxFps == profile.MaxFps &&
                slot.TurnScreenOff == profile.TurnScreenOff &&
                slot.TurnScreenOffBeforeHighFps == profile.TurnScreenOffBeforeHighFps &&
                slot.StayAwake == profile.StayAwake &&
                slot.UseHidKeyboard == profile.UseHidKeyboard &&
                slot.UseHidMouse == profile.UseHidMouse &&
                slot.ForceStopStartApp == profile.ForceStopStartApp &&
                slot.FlexDisplay == profile.FlexDisplay &&
                string.Equals(slot.AdditionalArguments,
                    profile.AdditionalArguments,
                    System.StringComparison.Ordinal) &&
                string.Equals(slot.StartAppPackage, packageName,
                    System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(slot.StartAppName, resolvedAppName,
                    System.StringComparison.Ordinal);
        }

        internal static void ApplySingleWindowAppProfile(
            SingleWindowAppProfile profile,
            SingleWindowSlotSettings slot,
            string packageName,
            string appName)
        {
            if (profile == null)
                throw new System.ArgumentNullException("profile");
            if (slot == null)
                throw new System.ArgumentNullException("slot");

            slot.Width = profile.Width;
            slot.Height = profile.Height;
            slot.CustomWidth = profile.CustomWidth;
            slot.CustomHeight = profile.CustomHeight;
            slot.Dpi = profile.Dpi;
            slot.BitRate = profile.BitRate;
            slot.MaxFps = profile.MaxFps;
            slot.TurnScreenOff = profile.TurnScreenOff;
            slot.TurnScreenOffBeforeHighFps = profile.TurnScreenOffBeforeHighFps;
            slot.StayAwake = profile.StayAwake;
            slot.UseHidKeyboard = profile.UseHidKeyboard;
            slot.UseHidMouse = profile.UseHidMouse;
            slot.ForceStopStartApp = profile.ForceStopStartApp;
            slot.StartAppPackage = packageName;
            slot.StartAppName = string.IsNullOrWhiteSpace(appName)
                ? profile.AppName
                : appName;
            slot.AdditionalArguments = profile.AdditionalArguments;
            slot.FlexDisplay = profile.FlexDisplay;
        }

        private void NormalizeRecentCustomResolutions()
        {
            var normalized = new List<RecentCustomResolutionSettings>(
                MaximumRecentCustomResolutions);
            var dimensions = new HashSet<long>();
            foreach (var item in RecentCustomResolutions)
            {
                if (item == null ||
                    item.Width < 320 || item.Width > 4096 ||
                    item.Height < 240 || item.Height > 4096 ||
                    IsPresetResolution(item.Width, item.Height))
                {
                    continue;
                }

                var key = ((long)item.Width << 32) | (uint)item.Height;
                if (!dimensions.Add(key)) continue;
                normalized.Add(new RecentCustomResolutionSettings
                {
                    Width = item.Width,
                    Height = item.Height
                });
                if (normalized.Count == MaximumRecentCustomResolutions)
                    break;
            }

            RecentCustomResolutions = normalized;
        }

        public DeviceRunSettingsProfile GetOrCreateDeviceRunSettings(
            string deviceIdentity)
        {
            var identity = (deviceIdentity ?? string.Empty).Trim();
            if (identity.Length == 0)
                throw new System.ArgumentException(
                    "Device identity is empty.",
                    "deviceIdentity");
            if (DeviceRunSettingsProfiles == null)
            {
                DeviceRunSettingsProfiles =
                    new List<DeviceRunSettingsProfile>();
            }

            foreach (var profile in DeviceRunSettingsProfiles)
            {
                if (profile != null && string.Equals(
                        profile.DeviceIdentity,
                        identity,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }

            var created = DeviceRunSettingsProfile.Create(
                identity,
                VirtualDisplay,
                Scrcpy,
                LastSuccess,
                SingleWindowSlots,
                SingleWindowAppProfiles);
            DeviceRunSettingsProfiles.Add(created);
            return created;
        }

        public DeviceWirelessConnectionProfile
            GetOrCreateDeviceWirelessConnection(
                string deviceIdentity,
                bool seedFromLegacyConnection)
        {
            var identity = (deviceIdentity ?? string.Empty).Trim();
            if (identity.Length == 0)
                throw new System.ArgumentException(
                    "Device identity is empty.",
                    "deviceIdentity");
            if (DeviceWirelessConnectionProfiles == null)
            {
                DeviceWirelessConnectionProfiles =
                    new List<DeviceWirelessConnectionProfile>();
            }

            foreach (var profile in DeviceWirelessConnectionProfiles)
            {
                if (profile != null && string.Equals(
                        profile.DeviceIdentity,
                        identity,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }

            var source = seedFromLegacyConnection
                ? Connection
                : null;
            var created = DeviceWirelessConnectionProfile.Create(
                identity,
                source);
            DeviceWirelessConnectionProfiles.Add(created);
            return created;
        }

        public DeviceWirelessConnectionProfile
            FindDeviceWirelessConnection(string deviceIdentity)
        {
            var identity = (deviceIdentity ?? string.Empty).Trim();
            if (identity.Length == 0 ||
                DeviceWirelessConnectionProfiles == null)
            {
                return null;
            }

            foreach (var profile in DeviceWirelessConnectionProfiles)
            {
                if (profile != null && string.Equals(
                        profile.DeviceIdentity,
                        identity,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }
            return null;
        }

        private void NormalizeDeviceRunSettingsProfiles(
            AppSettings defaults)
        {
            var identities = new HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase);
            for (var index = DeviceRunSettingsProfiles.Count - 1;
                index >= 0;
                index--)
            {
                var profile = DeviceRunSettingsProfiles[index];
                if (profile == null)
                {
                    DeviceRunSettingsProfiles.RemoveAt(index);
                    continue;
                }

                profile.DeviceIdentity =
                    (profile.DeviceIdentity ?? string.Empty).Trim();
                if (profile.DeviceIdentity.Length == 0 ||
                    !identities.Add(profile.DeviceIdentity))
                {
                    DeviceRunSettingsProfiles.RemoveAt(index);
                    continue;
                }

                if (profile.VirtualDisplay == null)
                    profile.VirtualDisplay = CloneVirtualDisplay(
                        VirtualDisplay ?? defaults.VirtualDisplay);
                if (profile.Scrcpy == null)
                    profile.Scrcpy = CloneScrcpy(
                        Scrcpy ?? defaults.Scrcpy);
                if (profile.LastSuccess == null)
                    profile.LastSuccess = new LastSuccessSettings();
                if (profile.SingleWindowSlots == null)
                {
                    profile.SingleWindowSlots = CloneSingleWindowSlots(
                        SingleWindowSlots ?? defaults.SingleWindowSlots);
                }
                while (profile.SingleWindowSlots.Count < 3)
                {
                    profile.SingleWindowSlots.Add(
                        CloneSingleWindowSlot(
                            defaults.SingleWindowSlots[
                                profile.SingleWindowSlots.Count]));
                }
                if (profile.SingleWindowAppProfiles == null)
                {
                    profile.SingleWindowAppProfiles =
                        new List<SingleWindowAppProfile>();
                }
                if (profile.RememberedApps == null)
                    profile.RememberedApps = new List<RememberedAppSettings>();

                profile.VirtualDisplay.Width = NormalizeRange(
                    profile.VirtualDisplay.Width,
                    320,
                    4096,
                    defaults.VirtualDisplay.Width);
                profile.VirtualDisplay.Height = NormalizeRange(
                    profile.VirtualDisplay.Height,
                    240,
                    4096,
                    defaults.VirtualDisplay.Height);
                profile.VirtualDisplay.CustomWidth = NormalizeRange(
                    profile.VirtualDisplay.CustomWidth,
                    320,
                    4096,
                    profile.VirtualDisplay.Width);
                profile.VirtualDisplay.CustomHeight = NormalizeRange(
                    profile.VirtualDisplay.CustomHeight,
                    240,
                    4096,
                    profile.VirtualDisplay.Height);
                profile.VirtualDisplay.Dpi = System.Math.Max(
                    120,
                    System.Math.Min(640, profile.VirtualDisplay.Dpi));
                profile.VirtualDisplay.ReuseExistingDisplay = true;

                for (var slotIndex = 0;
                    slotIndex < profile.SingleWindowSlots.Count;
                    slotIndex++)
                {
                    var slot = profile.SingleWindowSlots[slotIndex];
                    var defaultSlot = slotIndex <
                        defaults.SingleWindowSlots.Count
                        ? defaults.SingleWindowSlots[slotIndex]
                        : defaults.SingleWindowSlots[0];
                    if (slot == null)
                    {
                        slot = CloneSingleWindowSlot(defaultSlot);
                        profile.SingleWindowSlots[slotIndex] = slot;
                    }
                    slot.Slot = slotIndex + 1;
                    slot.Width = NormalizeRange(
                        slot.Width, 320, 4096, defaultSlot.Width);
                    slot.Height = NormalizeRange(
                        slot.Height, 240, 4096, defaultSlot.Height);
                    slot.CustomWidth = NormalizeRange(
                        slot.CustomWidth, 320, 4096, slot.Width);
                    slot.CustomHeight = NormalizeRange(
                        slot.CustomHeight, 240, 4096, slot.Height);
                    slot.Dpi = System.Math.Max(
                        120,
                        System.Math.Min(640, slot.Dpi));
                }
                NormalizeSingleWindowAppProfiles(
                    profile.SingleWindowAppProfiles,
                    defaults.SingleWindowSlots[0]);
            }
        }

        private void NormalizeDeviceWirelessConnectionProfiles(
            AppSettings defaults)
        {
            var identities = new HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase);
            for (var index = DeviceWirelessConnectionProfiles.Count - 1;
                index >= 0;
                index--)
            {
                var profile = DeviceWirelessConnectionProfiles[index];
                if (profile == null)
                {
                    DeviceWirelessConnectionProfiles.RemoveAt(index);
                    continue;
                }

                profile.DeviceIdentity =
                    (profile.DeviceIdentity ?? string.Empty).Trim();
                if (profile.DeviceIdentity.Length == 0 ||
                    !identities.Add(profile.DeviceIdentity))
                {
                    DeviceWirelessConnectionProfiles.RemoveAt(index);
                    continue;
                }

                if (!System.Enum.IsDefined(
                    typeof(AdbConnectionMode),
                    profile.Mode))
                {
                    profile.Mode = AdbConnectionMode.Usb;
                }
                profile.WirelessHost =
                    (profile.WirelessHost ?? string.Empty).Trim();
                if (profile.WirelessPort < 1 ||
                    profile.WirelessPort > 65535)
                {
                    profile.WirelessPort =
                        defaults.Connection.WirelessPort;
                }
                if (profile.Mode == AdbConnectionMode.Wireless &&
                    !IsValidWirelessHost(profile.WirelessHost))
                {
                    profile.Mode = AdbConnectionMode.Usb;
                }
            }
        }

        private static int NormalizeRange(
            int value,
            int minimum,
            int maximum,
            int fallback)
        {
            return value < minimum || value > maximum
                ? fallback
                : value;
        }

        private static string NormalizeSharedStorageFolder(
            string value,
            string fallback)
        {
            var normalized = (value ?? string.Empty)
                .Trim()
                .Replace('\\', '/');
            while (normalized.Contains("//"))
                normalized = normalized.Replace("//", "/");
            normalized = normalized.TrimEnd('/');

            var sharedStoragePath =
                string.Equals(
                    normalized,
                    "/sdcard",
                    System.StringComparison.Ordinal) ||
                normalized.StartsWith(
                    "/sdcard/",
                    System.StringComparison.Ordinal) ||
                string.Equals(
                    normalized,
                    "/storage/emulated/0",
                    System.StringComparison.Ordinal) ||
                normalized.StartsWith(
                    "/storage/emulated/0/",
                    System.StringComparison.Ordinal);
            if (!sharedStoragePath ||
                normalized.Any(character => char.IsControl(character)) ||
                normalized.IndexOf('"') >= 0 ||
                normalized.Split('/').Any(part =>
                    string.Equals(part, ".", System.StringComparison.Ordinal) ||
                    string.Equals(part, "..", System.StringComparison.Ordinal) ||
                    Encoding.UTF8.GetByteCount(part) > 255))
            {
                normalized = (fallback ?? "/sdcard/Download/")
                    .Trim()
                    .Replace('\\', '/')
                    .TrimEnd('/');
            }
            return normalized + "/";
        }

        private static bool IsValidWirelessHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return false;
            var value = host.Trim();
            if (value.StartsWith("[", System.StringComparison.Ordinal) &&
                value.EndsWith("]", System.StringComparison.Ordinal))
            {
                value = value.Substring(1, value.Length - 2);
            }
            return value.Length > 0 && Regex.IsMatch(
                value,
                @"^[A-Za-z0-9._:%-]+$");
        }

        private static void AddRememberedApp(
            IList<RememberedAppSettings> apps,
            string packageName,
            string appName)
        {
            if (apps == null || string.IsNullOrWhiteSpace(packageName))
                return;

            foreach (var app in apps)
            {
                if (app != null &&
                    string.Equals(
                        app.PackageName,
                        packageName,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(appName))
                        app.Name = appName;
                    return;
                }
            }

            apps.Add(new RememberedAppSettings
            {
                PackageName = packageName,
                Name = string.IsNullOrWhiteSpace(appName)
                    ? packageName
                    : appName
            });
        }

        private static bool HasStayAwakeArgument(string value)
        {
            return Regex.IsMatch(
                value ?? string.Empty,
                @"(?<!\S)(?:-w|--stay-awake|--keep-active)(?!\S)",
                RegexOptions.IgnoreCase);
        }

        private static string RemoveStayAwakeArgument(string value)
        {
            return Regex.Replace(
                value ?? string.Empty,
                @"(?<!\S)(?:-w|--stay-awake|--keep-active)(?!\S)",
                string.Empty,
                RegexOptions.IgnoreCase).Trim();
        }

        private static bool HasFlexDisplayArgument(string value)
        {
            return Regex.IsMatch(
                value ?? string.Empty,
                @"(?<!\S)(?:-x|--flex-display)(?!\S)",
                RegexOptions.IgnoreCase);
        }

        private static string RemoveFlexDisplayArgument(string value)
        {
            return Regex.Replace(
                value ?? string.Empty,
                @"(?<!\S)(?:-x|--flex-display)(?!\S)",
                string.Empty,
                RegexOptions.IgnoreCase).Trim();
        }

        private static List<SingleWindowSlotSettings> CreateDefaultSingleWindowSlots()
        {
            return new List<SingleWindowSlotSettings>
            {
                CreateDefaultSingleWindowSlot(1),
                CreateDefaultSingleWindowSlot(2),
                CreateDefaultSingleWindowSlot(3)
            };
        }

        private static void NormalizeSingleWindowAppProfiles(
            List<SingleWindowAppProfile> profiles,
            SingleWindowSlotSettings defaults)
        {
            var packages = new HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase);
            for (var index = profiles.Count - 1; index >= 0; index--)
            {
                var profile = profiles[index];
                if (profile == null)
                {
                    profiles.RemoveAt(index);
                    continue;
                }

                profile.PackageName =
                    (profile.PackageName ?? string.Empty).Trim();
                if (profile.PackageName.Length == 0 ||
                    !packages.Add(profile.PackageName))
                {
                    profiles.RemoveAt(index);
                    continue;
                }

                profile.AppName = string.IsNullOrWhiteSpace(profile.AppName)
                    ? profile.PackageName
                    : profile.AppName.Trim();
                profile.Width = NormalizeRange(
                    profile.Width, 320, 4096, defaults.Width);
                profile.Height = NormalizeRange(
                    profile.Height, 240, 4096, defaults.Height);
                profile.CustomWidth = NormalizeRange(
                    profile.CustomWidth, 320, 4096, profile.Width);
                profile.CustomHeight = NormalizeRange(
                    profile.CustomHeight, 240, 4096, profile.Height);
                profile.Dpi = System.Math.Max(
                    120,
                    System.Math.Min(640, profile.Dpi));
                if (string.IsNullOrWhiteSpace(profile.BitRate))
                    profile.BitRate = defaults.BitRate;
                if (!FrameRateRules.IsSupported(profile.MaxFps))
                    profile.MaxFps = defaults.MaxFps;
                profile.AdditionalArguments =
                    (profile.AdditionalArguments ?? string.Empty).Trim();
            }
        }

        private static SingleWindowSlotSettings CreateDefaultSingleWindowSlot(
            int slot)
        {
            return new SingleWindowSlotSettings
            {
                Slot = slot,
                Width = 1600,
                Height = 900,
                Dpi = 150,
                BitRate = "8M",
                MaxFps = 60,
                TurnScreenOff = true,
                StayAwake = true,
                UseHidKeyboard = true,
                UseHidMouse = true,
                ForceStopStartApp = false,
                StartAppPackage = string.Empty,
                StartAppName = string.Empty,
                AdditionalArguments = string.Empty,
                CustomWidth = 1600,
                CustomHeight = 900,
                FlexDisplay = false
            };
        }

        internal static VirtualDisplaySettings CloneVirtualDisplay(
            VirtualDisplaySettings source)
        {
            source = source ?? new VirtualDisplaySettings();
            return new VirtualDisplaySettings
            {
                Width = source.Width,
                Height = source.Height,
                Dpi = source.Dpi,
                Suffix = source.Suffix,
                ReuseExistingDisplay = source.ReuseExistingDisplay,
                CustomWidth = source.CustomWidth,
                CustomHeight = source.CustomHeight,
                HidePhonePreview = source.HidePhonePreview
            };
        }

        internal static ScrcpySettings CloneScrcpy(ScrcpySettings source)
        {
            source = source ?? new ScrcpySettings();
            return new ScrcpySettings
            {
                BitRate = source.BitRate,
                MaxFps = source.MaxFps,
                WindowTitle = source.WindowTitle,
                TurnScreenOff = source.TurnScreenOff,
                TurnScreenOffBeforeHighFps = source.TurnScreenOffBeforeHighFps,
                UseHidKeyboard = source.UseHidKeyboard,
                UseHidMouse = source.UseHidMouse,
                ForceStopStartApp = source.ForceStopStartApp,
                StartAppPackage = source.StartAppPackage,
                StartAppName = source.StartAppName,
                AdditionalArguments = source.AdditionalArguments,
                StayAwake = source.StayAwake
            };
        }

        internal static List<SingleWindowSlotSettings>
            CloneSingleWindowSlots(
                IList<SingleWindowSlotSettings> source)
        {
            var result = new List<SingleWindowSlotSettings>();
            if (source == null) return result;
            foreach (var slot in source)
                result.Add(CloneSingleWindowSlot(slot));
            return result;
        }

        internal static SingleWindowSlotSettings CloneSingleWindowSlot(
            SingleWindowSlotSettings source)
        {
            source = source ?? new SingleWindowSlotSettings();
            return new SingleWindowSlotSettings
            {
                Slot = source.Slot,
                Width = source.Width,
                Height = source.Height,
                Dpi = source.Dpi,
                BitRate = source.BitRate,
                MaxFps = source.MaxFps,
                TurnScreenOff = source.TurnScreenOff,
                TurnScreenOffBeforeHighFps = source.TurnScreenOffBeforeHighFps,
                StayAwake = source.StayAwake,
                UseHidKeyboard = source.UseHidKeyboard,
                UseHidMouse = source.UseHidMouse,
                ForceStopStartApp = source.ForceStopStartApp,
                StartAppPackage = source.StartAppPackage,
                StartAppName = source.StartAppName,
                AdditionalArguments = source.AdditionalArguments,
                CustomWidth = source.CustomWidth,
                CustomHeight = source.CustomHeight,
                FlexDisplay = source.FlexDisplay
            };
        }

        internal static List<SingleWindowAppProfile>
            CloneSingleWindowAppProfiles(
                IList<SingleWindowAppProfile> source)
        {
            var result = new List<SingleWindowAppProfile>();
            if (source == null) return result;
            foreach (var profile in source)
            {
                if (profile == null) continue;
                result.Add(new SingleWindowAppProfile
                {
                    PackageName = profile.PackageName,
                    AppName = profile.AppName,
                    Width = profile.Width,
                    Height = profile.Height,
                    Dpi = profile.Dpi,
                    BitRate = profile.BitRate,
                    MaxFps = profile.MaxFps,
                    TurnScreenOff = profile.TurnScreenOff,
                    TurnScreenOffBeforeHighFps = profile.TurnScreenOffBeforeHighFps,
                    StayAwake = profile.StayAwake,
                    UseHidKeyboard = profile.UseHidKeyboard,
                    UseHidMouse = profile.UseHidMouse,
                    ForceStopStartApp = profile.ForceStopStartApp,
                    AdditionalArguments = profile.AdditionalArguments,
                    CustomWidth = profile.CustomWidth,
                    CustomHeight = profile.CustomHeight,
                    FlexDisplay = profile.FlexDisplay
                });
            }
            return result;
        }
    }
}

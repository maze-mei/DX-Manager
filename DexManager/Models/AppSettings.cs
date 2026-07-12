using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace DexManager.Models
{
    [DataContract]
    public sealed class AppSettings
    {
        public const int CurrentSchemaVersion = 17;

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

        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                SchemaVersion = CurrentSchemaVersion,
                Language = AppLanguage.Auto,
                Theme = AppTheme.Auto,
                RememberedApps = new List<RememberedAppSettings>(),
                Paths = new PathSettings
                {
                    AdbPath = string.Empty,
                    AdbSelectionMode = AdbSelectionMode.Auto,
                    Win7AdbPath = @"tools\adb\legacy\adb.exe",
                    ModernAdbPath = @"tools\adb\modern\adb.exe",
                    ScrcpyPath = @"tools\scrcpy\scrcpy.exe",
                    ScreenshotFolder = "screenshot",
                    DeviceScreenshotFolder = "/sdcard/DCIM/DeX Screenshots",
                    LogFolder = "logs"
                },
                VirtualDisplay = new VirtualDisplaySettings
                {
                    Width = 1600,
                    Height = 900,
                    Dpi = 150,
                    Suffix = "hdmi",
                    ReuseExistingDisplay = true,
                    CustomWidth = 1600,
                    CustomHeight = 900
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
                    ConnectedStartDelayMs = 3000,
                    AdbWakeUpDelayMs = 3000,
                    AutoHideIdleSeconds = 30,
                    CaptureWaitSeconds = 5,
                    ProcessTimeoutMs = 15000
                },
                Features = new FeatureSettings
                {
                    StartWithWindows = false,
                    StartMinimizedToTray = true,
                    RegisterAdbPathAutomatically = false,
                    ScrcpyWakeUpMode = ScrcpyWakeUpMode.OnAdbFailure,
                    AutoHideEnabled = true,
                    PushCaptureToDevice = true,
                    ResetVirtualDisplayOnStop = true,
                    DisableStayAwakeOnStop = true,
                    AutoStartDexOnDeviceConnected = true,
                    ShowConnectedDeviceInfo = true
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
                    ConvertEnterToShiftEnter = true,
                    EnterInputMode = KeyInputMode.SendInputScanCode,
                    IgnoreShiftSpace = true
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
                Paths.ModernAdbPath = defaults.Paths.ModernAdbPath;
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
            if (VirtualDisplay.CustomWidth <= 0)
                VirtualDisplay.CustomWidth = VirtualDisplay.Width;
            if (VirtualDisplay.CustomHeight <= 0)
                VirtualDisplay.CustomHeight = VirtualDisplay.Height;
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
                if (slot.CustomWidth <= 0) slot.CustomWidth = slot.Width;
                if (slot.CustomHeight <= 0) slot.CustomHeight = slot.Height;
            }
            if (string.IsNullOrWhiteSpace(Paths.Win7AdbPath))
                Paths.Win7AdbPath = defaults.Paths.Win7AdbPath;
            if (string.IsNullOrWhiteSpace(Paths.ModernAdbPath))
                Paths.ModernAdbPath = defaults.Paths.ModernAdbPath;
            if (string.IsNullOrWhiteSpace(Paths.ScrcpyPath))
                Paths.ScrcpyPath = defaults.Paths.ScrcpyPath;
            if (string.IsNullOrWhiteSpace(Paths.ScreenshotFolder))
                Paths.ScreenshotFolder = defaults.Paths.ScreenshotFolder;
            if (string.IsNullOrWhiteSpace(Paths.DeviceScreenshotFolder))
                Paths.DeviceScreenshotFolder =
                    defaults.Paths.DeviceScreenshotFolder;
            if (string.IsNullOrWhiteSpace(Paths.LogFolder))
                Paths.LogFolder = defaults.Paths.LogFolder;
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
    }

    [DataContract]
    public sealed class RememberedAppSettings
    {
        [DataMember(Order = 1)] public string Name { get; set; }
        [DataMember(Order = 2)] public string PackageName { get; set; }
    }

    [DataContract]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class PathSettings
    {
        [DataMember(Order = 1)] public string AdbPath { get; set; }
        [DataMember(Order = 2)] public AdbSelectionMode AdbSelectionMode { get; set; }
        [DataMember(Order = 3)] public string Win7AdbPath { get; set; }
        [DataMember(Order = 4)] public string ModernAdbPath { get; set; }
        [DataMember(Order = 5)] public string ScrcpyPath { get; set; }
        [DataMember(Order = 6)] public string ScreenshotFolder { get; set; }
        [DataMember(Order = 7)] public string DeviceScreenshotFolder { get; set; }
        [DataMember(Order = 8)] public string LogFolder { get; set; }
    }

    public enum AdbSelectionMode
    {
        Auto = 0,
        Manual = 1
    }

    [DataContract]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class ConnectionSettings
    {
        [DataMember(Order = 1)] public AdbConnectionMode Mode { get; set; }
        [DataMember(Order = 2)] public string WirelessHost { get; set; }
        [DataMember(Order = 3)] public int WirelessPort { get; set; }
        [DataMember(Order = 4)] public bool AutoReconnect { get; set; }
    }

    public enum AdbConnectionMode
    {
        Usb = 0,
        Wireless = 1
    }

    public enum AppLanguage
    {
        Auto = 0,
        Korean = 1,
        English = 2
    }

    public enum AppTheme
    {
        Auto = 0,
        Light = 1,
        Dark = 2
    }

    [DataContract]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class VirtualDisplaySettings
    {
        [DataMember(Order = 1)] public int Width { get; set; }
        [DataMember(Order = 2)] public int Height { get; set; }
        [DataMember(Order = 3)] public int Dpi { get; set; }
        [DataMember(Order = 4)] public string Suffix { get; set; }
        [DataMember(Order = 5)] public bool ReuseExistingDisplay { get; set; }
        [DataMember(Order = 6)] public int CustomWidth { get; set; }
        [DataMember(Order = 7)] public int CustomHeight { get; set; }
    }

    [DataContract]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class ScrcpySettings
    {
        [DataMember(Order = 1)] public string BitRate { get; set; }
        [DataMember(Order = 2)] public int MaxFps { get; set; }
        [DataMember(Order = 3)] public string WindowTitle { get; set; }
        [DataMember(Order = 4)] public bool TurnScreenOff { get; set; }
        [DataMember(Order = 5)] public bool UseHidKeyboard { get; set; }
        [DataMember(Order = 6)] public bool UseHidMouse { get; set; }
        [DataMember(Order = 7)] public bool ForceStopStartApp { get; set; }
        [DataMember(Order = 8)] public string StartAppPackage { get; set; }
        [DataMember(Order = 9)] public string AdditionalArguments { get; set; }
        [DataMember(Order = 10)] public bool StayAwake { get; set; }
        [DataMember(Order = 11)] public string StartAppName { get; set; }
    }

    [DataContract]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class SingleWindowSlotSettings
    {
        [DataMember(Order = 1)] public int Slot { get; set; }
        [DataMember(Order = 2)] public int Width { get; set; }
        [DataMember(Order = 3)] public int Height { get; set; }
        [DataMember(Order = 4)] public int Dpi { get; set; }
        [DataMember(Order = 5)] public string BitRate { get; set; }
        [DataMember(Order = 6)] public int MaxFps { get; set; }
        [DataMember(Order = 7)] public bool TurnScreenOff { get; set; }
        [DataMember(Order = 8)] public bool StayAwake { get; set; }
        [DataMember(Order = 9)] public bool UseHidKeyboard { get; set; }
        [DataMember(Order = 10)] public bool UseHidMouse { get; set; }
        [DataMember(Order = 11)] public bool ForceStopStartApp { get; set; }
        [DataMember(Order = 12)] public string StartAppPackage { get; set; }
        [DataMember(Order = 13)] public string StartAppName { get; set; }
        [DataMember(Order = 14)] public string AdditionalArguments { get; set; }
        [DataMember(Order = 15)] public int CustomWidth { get; set; }
        [DataMember(Order = 16)] public int CustomHeight { get; set; }
        [DataMember(Order = 17)] public bool FlexDisplay { get; set; }
    }

    [DataContract]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class TimingSettings
    {
        [DataMember(Order = 1)] public int DeviceMonitorIntervalMs { get; set; }
        [DataMember(Order = 2)] public int DisconnectMonitorIntervalMs { get; set; }
        [DataMember(Order = 3)] public int ConnectedStartDelayMs { get; set; }
        [DataMember(Order = 4)] public int AdbWakeUpDelayMs { get; set; }
        [DataMember(Order = 5)] public int AutoHideIdleSeconds { get; set; }
        [DataMember(Order = 6)] public int CaptureWaitSeconds { get; set; }
        [DataMember(Order = 7)] public int ProcessTimeoutMs { get; set; }
    }

    [DataContract]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class FeatureSettings
    {
        [DataMember(Order = 1)] public bool StartWithWindows { get; set; }
        [DataMember(Order = 2)] public bool StartMinimizedToTray { get; set; }
        [DataMember(Order = 3)] public bool RegisterAdbPathAutomatically { get; set; }
        [DataMember(Order = 4)] public ScrcpyWakeUpMode ScrcpyWakeUpMode { get; set; }
        [DataMember(Order = 5)] public bool AutoHideEnabled { get; set; }
        [DataMember(Order = 6)] public bool PushCaptureToDevice { get; set; }
        [DataMember(Order = 7)] public bool ResetVirtualDisplayOnStop { get; set; }
        [DataMember(Order = 8)] public bool DisableStayAwakeOnStop { get; set; }
        [DataMember(Order = 9)] public bool AutoStartDexOnDeviceConnected { get; set; }
        [DataMember(Order = 10)] public bool ShowConnectedDeviceInfo { get; set; }
    }

    [DataContract]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class KeyMappingSettings
    {
        [DataMember(Order = 1)] public string CaptureHotkey { get; set; }
        [DataMember(Order = 2)] public string ExitHotkey { get; set; }
        [DataMember(Order = 3)] public bool UseLowLevelHotkeys { get; set; }
        [DataMember(Order = 4)] public bool LogKeyboardDiagnostics { get; set; }
        [DataMember(Order = 5)] public bool ConvertKoreanEnglishKey { get; set; }
        [DataMember(Order = 6)] public KeyInputMode KoreanEnglishInputMode { get; set; }
        [DataMember(Order = 7)] public bool HandleRightWindowsKey { get; set; }
        [DataMember(Order = 8)] public bool ConvertEnterToShiftEnter { get; set; }
        [DataMember(Order = 9)] public KeyInputMode EnterInputMode { get; set; }
        [DataMember(Order = 10)] public bool IgnoreShiftSpace { get; set; }
    }

    public enum KeyInputMode
    {
        SendInputVirtualKey = 0,
        SendInputScanCode = 1,
        Adb = 2
    }

    public enum ScrcpyWakeUpMode
    {
        Disabled = 0,
        OnAdbFailure = 1,
        AlwaysOnStartup = 2
    }
}

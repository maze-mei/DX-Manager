using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DexManager.Models
{
    [DataContract]
    public sealed class DeviceRunSettingsProfile
    {
        [DataMember(Order = 1)] public string DeviceIdentity { get; set; }
        [DataMember(Order = 2)]
        public VirtualDisplaySettings VirtualDisplay { get; set; }
        [DataMember(Order = 3)] public ScrcpySettings Scrcpy { get; set; }
        [DataMember(Order = 4)]
        public LastSuccessSettings LastSuccess { get; set; }
        [DataMember(Order = 5)]
        public List<SingleWindowSlotSettings> SingleWindowSlots { get; set; }
        [DataMember(Order = 6)]
        public List<SingleWindowAppProfile> SingleWindowAppProfiles
        {
            get;
            set;
        }
        [DataMember(Order = 7)]
        public List<RememberedAppSettings> RememberedApps { get; set; }

        public static DeviceRunSettingsProfile Create(
            string deviceIdentity,
            VirtualDisplaySettings virtualDisplay,
            ScrcpySettings scrcpy,
            LastSuccessSettings lastSuccess,
            IList<SingleWindowSlotSettings> slots,
            IList<SingleWindowAppProfile> appProfiles)
        {
            return new DeviceRunSettingsProfile
            {
                DeviceIdentity = deviceIdentity,
                VirtualDisplay = AppSettings.CloneVirtualDisplay(
                    virtualDisplay),
                Scrcpy = AppSettings.CloneScrcpy(scrcpy),
                LastSuccess = CloneLastSuccess(lastSuccess),
                SingleWindowSlots = AppSettings.CloneSingleWindowSlots(
                    slots),
                SingleWindowAppProfiles =
                    AppSettings.CloneSingleWindowAppProfiles(appProfiles),
                RememberedApps = new List<RememberedAppSettings>()
            };
        }

        private static LastSuccessSettings CloneLastSuccess(
            LastSuccessSettings source)
        {
            source = source ?? new LastSuccessSettings();
            return new LastSuccessSettings
            {
                Width = source.Width,
                Height = source.Height,
                Dpi = source.Dpi,
                AdbPath = source.AdbPath,
                ScrcpyPath = source.ScrcpyPath,
                ScrcpyArguments = source.ScrcpyArguments,
                DisplayId = source.DisplayId,
                SavedAtUtc = source.SavedAtUtc
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
    public sealed class RecentCustomResolutionSettings
    {
        [DataMember(Order = 1)] public int Width { get; set; }
        [DataMember(Order = 2)] public int Height { get; set; }
    }

    [DataContract]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class PathSettings
    {
        [DataMember(Order = 1)] public string AdbPath { get; set; }
        [DataMember(Order = 2)] public AdbSelectionMode AdbSelectionMode { get; set; }
        [DataMember(Order = 3)] public string Win7AdbPath { get; set; }
        [DataMember(Order = 5)] public string ScrcpyPath { get; set; }
        [DataMember(Order = 6)] public string ScreenshotFolder { get; set; }
        [DataMember(Order = 7)] public string DeviceScreenshotFolder { get; set; }
        [DataMember(Order = 8)] public string LogFolder { get; set; }
        [DataMember(Order = 9)] public string FileTransferTargetFolder { get; set; }
        [DataMember(Order = 10)] public string PhoneToPcReceiveFolder { get; set; }
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

    [DataContract]
    public sealed class DeviceWirelessConnectionProfile
    {
        [DataMember(Order = 1)] public string DeviceIdentity { get; set; }
        [DataMember(Order = 2)] public AdbConnectionMode Mode { get; set; }
        [DataMember(Order = 3)] public string WirelessHost { get; set; }
        [DataMember(Order = 4)] public int WirelessPort { get; set; }
        [DataMember(Order = 5)] public bool AutoReconnect { get; set; }

        public static DeviceWirelessConnectionProfile Create(
            string deviceIdentity,
            ConnectionSettings source)
        {
            return new DeviceWirelessConnectionProfile
            {
                DeviceIdentity = (deviceIdentity ?? string.Empty).Trim(),
                Mode = source == null
                    ? AdbConnectionMode.Usb
                    : source.Mode,
                WirelessHost = source == null
                    ? string.Empty
                    : source.WirelessHost ?? string.Empty,
                WirelessPort = source == null ||
                    source.WirelessPort < 1 ||
                    source.WirelessPort > 65535
                    ? 5555
                    : source.WirelessPort,
                AutoReconnect = source == null || source.AutoReconnect
            };
        }
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

    public enum MiniControlBarSide
    {
        Right = 0,
        Left = 1
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
        [DataMember(Order = 8)] public bool HidePhonePreview { get; set; }
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
        [DataMember(Order = 12)] public bool? TurnScreenOffBeforeHighFps { get; set; }
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
        [DataMember(Order = 18)] public bool? TurnScreenOffBeforeHighFps { get; set; }
    }

    [DataContract]
    public sealed class SingleWindowAppProfile
    {
        [DataMember(Order = 1)] public string PackageName { get; set; }
        [DataMember(Order = 2)] public string AppName { get; set; }
        [DataMember(Order = 3)] public int Width { get; set; }
        [DataMember(Order = 4)] public int Height { get; set; }
        [DataMember(Order = 5)] public int Dpi { get; set; }
        [DataMember(Order = 6)] public string BitRate { get; set; }
        [DataMember(Order = 7)] public int MaxFps { get; set; }
        [DataMember(Order = 8)] public bool TurnScreenOff { get; set; }
        [DataMember(Order = 9)] public bool StayAwake { get; set; }
        [DataMember(Order = 10)] public bool UseHidKeyboard { get; set; }
        [DataMember(Order = 11)] public bool UseHidMouse { get; set; }
        [DataMember(Order = 12)] public bool ForceStopStartApp { get; set; }
        [DataMember(Order = 13)] public string AdditionalArguments { get; set; }
        [DataMember(Order = 14)] public int CustomWidth { get; set; }
        [DataMember(Order = 15)] public int CustomHeight { get; set; }
        [DataMember(Order = 16)] public bool FlexDisplay { get; set; }
        [DataMember(Order = 17)] public bool? TurnScreenOffBeforeHighFps { get; set; }
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
        [DataMember(Order = 8)]
        public int VirtualDisplayDetectionTimeoutMs { get; set; }
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
        [DataMember(Order = 11)] public bool ManagedFileTransferEnabled { get; set; }
        [DataMember(Order = 12)] public bool MiniControlBarEnabled { get; set; }
        [DataMember(Order = 13)] public MiniControlBarSide MiniControlBarSide { get; set; }
        [DataMember(Order = 14)] public bool PhoneToPcTransferEnabled { get; set; }
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

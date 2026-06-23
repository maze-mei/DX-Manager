namespace DexManager.Models
{
    public sealed class AdbDeviceInfo
    {
        public string Serial { get; set; }
        public AdbDeviceStatus Status { get; set; }
        public string RawStatus { get; set; }

        public bool IsAuthorized
        {
            get { return Status == AdbDeviceStatus.Device; }
        }
    }

    public enum AdbDeviceStatus
    {
        Unknown = 0,
        Device = 1,
        Unauthorized = 2,
        Offline = 3,
        NoPermissions = 4
    }
}

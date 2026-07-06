namespace DexManager.Models
{
    public sealed class DeviceState
    {
        public DeviceState()
        {
            Status = AdbDeviceStatus.Unknown;
        }

        public bool IsConnected { get; set; }
        public string Serial { get; set; }
        public string DisplayName { get; set; }
        public AdbDeviceStatus Status { get; set; }

        public static DeviceState Disconnected()
        {
            return new DeviceState
            {
                IsConnected = false,
                Serial = string.Empty,
                DisplayName = string.Empty,
                Status = AdbDeviceStatus.Unknown
            };
        }
    }
}

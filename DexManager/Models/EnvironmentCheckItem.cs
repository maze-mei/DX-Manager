namespace DexManager.Models
{
    public sealed class EnvironmentCheckItem
    {
        public string Name { get; set; }
        public EnvironmentCheckStatus Status { get; set; }
        public string Message { get; set; }
    }

    public enum EnvironmentCheckStatus
    {
        Passed = 0,
        Warning = 1,
        Failed = 2
    }
}

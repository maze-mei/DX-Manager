namespace DexManager.Models
{
    public sealed class ManagedDisplaySession
    {
        public string Mode { get; set; }
        public string AppPackage { get; set; }
        public int DisplayId { get; set; }
        public int ScrcpyProcessId { get; set; }
        public string CreatedAtUtc { get; set; }

        public override string ToString()
        {
            return "mode=" + Mode +
                ", displayId=" + DisplayId +
                ", scrcpyPid=" + ScrcpyProcessId +
                (string.IsNullOrWhiteSpace(AppPackage)
                    ? string.Empty
                    : ", app=" + AppPackage);
        }
    }
}

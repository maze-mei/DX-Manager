namespace DexManager.Models
{
    public sealed class VirtualDisplayLease
    {
        public string Serial { get; set; }
        public int DisplayId { get; set; }
        public string PreviousOverlaySetting { get; set; }
        public string AppliedOverlaySetting { get; set; }
        public bool OwnsOverlaySetting { get; set; }
        public bool ReusedExistingDisplay { get; set; }
    }
}

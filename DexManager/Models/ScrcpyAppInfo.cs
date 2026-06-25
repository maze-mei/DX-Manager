namespace DexManager.Models
{
    public sealed class ScrcpyAppInfo
    {
        public string Name { get; set; }
        public string PackageName { get; set; }
        public bool IsSystemApp { get; set; }

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(PackageName)) return Name;
            return Name + " (" + PackageName + ")";
        }
    }
}

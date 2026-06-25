namespace DexManager.Models
{
    public sealed class DisplayInfo
    {
        public int Id { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Dpi { get; set; }
        public string Name { get; set; }
        public string Flags { get; set; }
        public string RawText { get; set; }

        public override string ToString()
        {
            var size = Width > 0 && Height > 0
                ? Width + "x" + Height
                : "unknown-size";
            var dpi = Dpi > 0 ? "/" + Dpi + "dpi" : string.Empty;
            var name = string.IsNullOrWhiteSpace(Name) ? string.Empty : ", name=" + Name;
            var flags = string.IsNullOrWhiteSpace(Flags) ? string.Empty : ", flags=" + Flags;
            return "id=" + Id + ", " + size + dpi + name + flags;
        }
    }
}

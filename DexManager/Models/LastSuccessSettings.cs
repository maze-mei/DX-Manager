using System.Runtime.Serialization;
using System.ComponentModel;

namespace DexManager.Models
{
    [DataContract]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class LastSuccessSettings
    {
        [DataMember(Order = 1)] public int Width { get; set; }
        [DataMember(Order = 2)] public int Height { get; set; }
        [DataMember(Order = 3)] public int Dpi { get; set; }
        [DataMember(Order = 4)] public string AdbPath { get; set; }
        [DataMember(Order = 5)] public string ScrcpyPath { get; set; }
        [DataMember(Order = 6)] public string ScrcpyArguments { get; set; }
        [DataMember(Order = 7)] public int DisplayId { get; set; }
        [DataMember(Order = 8)] public string SavedAtUtc { get; set; }
    }
}

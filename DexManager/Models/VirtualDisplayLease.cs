using System;
using System.Runtime.Serialization;

namespace DexManager.Models
{
    [Serializable]
    [DataContract]
    public sealed class VirtualDisplayLease
    {
        [DataMember] public bool NativeCompanionConfigured { get; set; }
        [DataMember] public bool NativeCompanionPolicyEnabled { get; set; }
        [DataMember] public bool NativeBrowserCleanupEnabled { get; set; }
        [DataMember] public bool NativeInitialMetricsPrepared { get; set; }
        [DataMember] public int NativeCompanionVersion { get; set; }
        public bool NativeViewerActive { get; set; }
        [DataMember] public int NativeProtocolVersion { get; set; }
        [DataMember] public bool NativeKeepBluetoothAudioOnStart { get; set; }
        [DataMember] public bool NativeResumed { get; set; }
        [DataMember] public bool NativeStayAwakeRequested { get; set; }
        [DataMember] public bool NativeRestoreStayAwake { get; set; }
        [DataMember] public string NativeOriginalStayAwake { get; set; }
        [DataMember]
        public bool NativeStartRequested { get; set; }
        [DataMember]
        public bool IsNativeDex { get; set; }
        [DataMember]
        public string NativeSessionToken { get; set; }
        [DataMember]
        public string NativePeerAddress { get; set; }
        [DataMember]
        public string NativeDeviceIdentity { get; set; }
        [DataMember]
        public string NativeRemoteJar { get; set; }
        [DataMember]
        public int NativeHelperPid { get; set; }
        [DataMember]
        public int NativeGuardPid { get; set; }
        [DataMember]
        public int NativeWidth { get; set; }
        [DataMember]
        public int NativeHeight { get; set; }
        [DataMember]
        public int NativeDpi { get; set; }
        [DataMember]
        public string Serial { get; set; }
        [DataMember]
        public int DisplayId { get; set; }
        [DataMember]
        public string PreviousOverlaySetting { get; set; }
        [DataMember]
        public string AppliedOverlaySetting { get; set; }
        [DataMember]
        public bool OwnsOverlaySetting { get; set; }
        [DataMember]
        public bool ReusedExistingDisplay { get; set; }
    }
}

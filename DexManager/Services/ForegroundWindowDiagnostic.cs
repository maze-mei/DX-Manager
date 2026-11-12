using System;
using System.IO;

namespace DexManager.Services
{
    // Focused-window metadata and diagnostic source fields. Event-source fields are not app identity.
    internal sealed class ForegroundWindowDiagnostic
    {
        internal const int MaxDetailBytes = 2048;
        public int DisplayId, WindowId, WindowType, Layer;
        public bool Focused, Active;
        public int EventType, EventDisplayId, EventWindowId;
        public string Package, EventPackage, StateClass, EventClass, RootClass, Title;

        public static ForegroundWindowDiagnostic Read(Stream stream)
        {
            var result = new ForegroundWindowDiagnostic
            {
                DisplayId = CompanionGuardianProtocol.ReadInt32(stream),
                WindowId = CompanionGuardianProtocol.ReadInt32(stream),
                WindowType = CompanionGuardianProtocol.ReadInt32(stream),
                Layer = CompanionGuardianProtocol.ReadInt32(stream),
                Focused = ReadFlag(stream), Active = ReadFlag(stream),
                EventType = CompanionGuardianProtocol.ReadInt32(stream),
                EventDisplayId = CompanionGuardianProtocol.ReadInt32(stream),
                EventWindowId = CompanionGuardianProtocol.ReadInt32(stream),
                Package = Detail(stream), EventPackage = Detail(stream),
                StateClass = Detail(stream), EventClass = Detail(stream),
                RootClass = Detail(stream), Title = Detail(stream)
            };
            if (result.DisplayId < 0 || result.WindowId < 0 || result.WindowType < 1 ||
                result.WindowType > 7 || result.EventType < 0 || result.EventDisplayId < -1 ||
                result.EventWindowId < -1 || !IsBrowserHost(result.Package))
                throw new InvalidDataException("Invalid browser window diagnostic.");
            return result;
        }

        public string FormatLog()
        {
            var eventName = EventType == 32 ? "WINDOW_STATE_CHANGED" :
                EventType == 4194304 ? "WINDOWS_CHANGED" :
                EventType == 8 ? "VIEW_FOCUSED" : EventType == 0 ? "SNAPSHOT" :
                "0x" + EventType.ToString("X");
            return "AI web: display=" + DisplayId + " window=" + WindowId +
                " package=" + Package + " title=\"" + Quoted(Title) +
                "\" stateClass=\"" + Quoted(StateClass) + "\" rootClass=\"" +
                Quoted(RootClass) + "\" event=" + eventName +
                " eventDisplay=" + EventDisplayId + " eventWindow=" + EventWindowId +
                " eventPackage=\"" + Quoted(EventPackage) + "\" eventClass=\"" +
                Quoted(EventClass) + "\" type=" + WindowType + " layer=" + Layer +
                " focused=" + Focused + " active=" + Active;
        }

        private static string Detail(Stream stream)
        {
            var value = CompanionGuardianProtocol.ReadString(stream, MaxDetailBytes);
            if (value.Length > 512) throw new InvalidDataException("Window detail is too long.");
            return value;
        }

        private static bool ReadFlag(Stream stream)
        {
            var value = stream.ReadByte();
            if (value < 0) throw new EndOfStreamException();
            if (value > 1) throw new InvalidDataException("Invalid window flag.");
            return value == 1;
        }

        private static bool IsBrowserHost(string package)
        {
            return package == "com.android.chrome" || package == "com.chrome.beta" ||
                package == "com.chrome.dev" || package == "com.chrome.canary" ||
                package == "com.sec.android.app.sbrowser" ||
                package != null && package.StartsWith("org.chromium.webapk.", StringComparison.Ordinal) &&
                AppKeyRuleResolver.IsPackageName(package);
        }

        private static string Quoted(string value)
        {
            if (string.IsNullOrEmpty(value)) return "<none>";
            var cleaned = value.ToCharArray();
            for (var i = 0; i < cleaned.Length; i++)
                if (char.IsControl(cleaned[i]) || cleaned[i] == '\u2028' || cleaned[i] == '\u2029')
                    cleaned[i] = ' ';
            return new string(cleaned).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}

using System;
using System.Globalization;
using System.Text;

namespace DexManager.Services
{
    internal sealed class NativeDexReady
    {
        internal int DisplayId, Width, Height, Dpi, HelperPid, GuardPid;
    }

    internal static class NativeDexProtocol
    {
        internal const int Version = 7;
        internal const int DisconnectGraceSeconds = 30;
        internal static string NewToken() { return Guid.NewGuid().ToString("N"); }

        internal static string PeerAddress(string token)
        {
            ValidateToken(token);
            return "02:" + token.Substring(0, 2) + ":" + token.Substring(2, 2) + ":" +
                token.Substring(4, 2) + ":" + token.Substring(6, 2) + ":" + token.Substring(8, 2);
        }

        internal static void ValidateToken(string token)
        {
            if (token == null || token.Length != 32) throw new ArgumentException("Invalid native session token.");
            foreach (var c in token)
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f'))
                    throw new ArgumentException("Invalid native session token.");
        }

        internal static bool TryRead(string expectedToken, string line, out string kind, out string[] data)
        {
            kind = null;
            data = null;
            if (line == null || line.Length > 8192) return false;
            var fields = line.Split('|');
            if (fields.Length < 4 || fields[0] != "DXND1" ||
                !string.Equals(fields[1], expectedToken, StringComparison.Ordinal)) return false;
            kind = fields[2];
            data = new string[fields.Length - 3];
            Array.Copy(fields, 3, data, 0, data.Length);
            return true;
        }

        internal static NativeDexReady Ready(string[] data)
        {
            if (data == null || data.Length != 6) throw new InvalidOperationException("Invalid native READY frame.");
            var values = new int[6];
            for (var i = 0; i < values.Length; i++)
                if (!int.TryParse(data[i], NumberStyles.None, CultureInfo.InvariantCulture, out values[i]) || values[i] <= 0)
                    throw new InvalidOperationException("Invalid native display/process identity.");
            if (values[1] > 16384 || values[2] > 16384 || values[3] > 2400 || values[4] == values[5])
                throw new InvalidOperationException("Invalid native display/process bounds.");
            return new NativeDexReady { DisplayId = values[0], Width = values[1], Height = values[2],
                Dpi = values[3], HelperPid = values[4], GuardPid = values[5] };
        }

        internal static string Error(string[] data)
        {
            if (data == null || data.Length != 1) return "Invalid native error frame.";
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(data[0])); }
            catch (FormatException) { return "Invalid native error encoding."; }
        }

        internal static bool Recovered(string token, string output)
        {
            foreach (var line in (output ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string kind; string[] data;
                if (TryRead(token, line, out kind, out data) && kind == "RECOVERED" &&
                    data.Length == 1 && data[0] == "1") return true;
            }
            return false;
        }
    }
}

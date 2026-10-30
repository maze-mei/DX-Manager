// DX Manager hidden-preview integration: Copyright 2026 asyouplz.
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using DexManager.Models;

namespace DexManager.Services
{
    // Only generated session tokens and parsed IDs cross the shell boundary.
    internal static class LoopbackDexProtocol
    {
        internal static string CreateRemotePath(string token)
        {
            if (token == null || !Regex.IsMatch(token, @"\A[a-f0-9]{32}\z"))
                throw new ArgumentException("Invalid loopback session token.", "token");
            return "/data/local/tmp/dxm-loopback-" + token + ".jar";
        }

        internal static string BuildShellCommand(string token)
        {
            return "CLASSPATH=" + CreateRemotePath(token) +
                " app_process / com.dxmanager.loopback.Main " + token;
        }

        internal static bool TryParseReady(string line, out int displayId)
        {
            displayId = 0;
            if (line == null) return false;
            var match = Regex.Match(line, @"\ADXM_LOOPBACK_READY ([1-9][0-9]*)\z");
            return match.Success && int.TryParse(match.Groups[1].Value,
                NumberStyles.None, CultureInfo.InvariantCulture, out displayId);
        }

        internal static ScrcpySettings CreateScrcpySettings(ScrcpySettings source)
        {
            if (source == null) throw new ArgumentNullException("source");
            ValidateAdditionalArguments(source.AdditionalArguments);
            var effective = AppSettings.CloneScrcpy(source);
            effective.TurnScreenOff = false;
            return effective;
        }

        internal static void ValidateAdditionalArguments(string arguments)
        {
            // Reject power-control overrides, including getopt abbreviations
            // and combined short flags. The saved normal-mode preference stays intact.
            foreach (Match match in Regex.Matches(arguments ?? string.Empty,
                @"(?:[^\s""]|""[^""]*"")+"))
            {
                var token = match.Value.Replace("\"", string.Empty);
                var option = token.Split('=')[0];
                var reserved = new[] { "--turn-screen-off", "--power-off-on-close",
                    "--no-power-on" };
                var blocked = option.StartsWith("-", StringComparison.Ordinal) &&
                    !option.StartsWith("--", StringComparison.Ordinal) &&
                    option.IndexOf('S') >= 1;
                foreach (var name in reserved)
                    blocked |= option.Length > 2 &&
                        option.StartsWith("--", StringComparison.Ordinal) &&
                        name.StartsWith(option, StringComparison.Ordinal);
                if (blocked)
                    throw new InvalidOperationException(LocalizationService.Format(
                        "Error.Loopback.PowerArgument", token));
            }
        }
    }
}

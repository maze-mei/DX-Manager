using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using DexManager.Models;

namespace DexManager.Services
{
    public sealed class VirtualDisplayService
    {
        private static readonly Regex DisplayIdRegex = new Regex(
            @"mDisplayId\s*=\s*(\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex NameRegex = new Regex(
            @"mName\s*=\s*""?([^"",\r\n}]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex FlagsRegex = new Regex(
            @"mFlags\s*=\s*([^,\r\n}]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SizeRegex = new Regex(
            @"(?<width>\d{3,5})\s*x\s*(?<height>\d{3,5})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex DpiRegex = new Regex(
            @"(?<dpi>\d{2,4})\s*dpi|density\s+(?<density>\d{2,4})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly AdbService _adbService;
        private readonly LogService _logService;

        public VirtualDisplayService(AdbService adbService, LogService logService)
        {
            _adbService = adbService;
            _logService = logService;
        }

        public string GetOverlaySetting()
        {
            var result = _adbService.Shell(
                "settings get global overlay_display_devices");
            return result.IsSuccess ? result.StandardOutput.Trim() : string.Empty;
        }

        public int EnsureVirtualDisplay(
            VirtualDisplaySettings settings,
            int creationWaitMs)
        {
            if (settings == null) throw new ArgumentNullException("settings");

            var current = GetOverlaySetting();
            var hasSetting = !string.IsNullOrWhiteSpace(current) &&
                !string.Equals(current, "none", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(current, "null", StringComparison.OrdinalIgnoreCase);
            var before = GetVirtualDisplays();
            LogDisplaySnapshot("before", before);

            if (hasSetting && settings.ReuseExistingDisplay)
            {
                _logService.Info(LocalizationService.Format(
                    "Log.Display.ReusingSetting",
                    current));
                return SelectExistingDisplay(settings, before);
            }

            var value = string.Format(
                "{0}x{1}/{2},{3}",
                settings.Width,
                settings.Height,
                settings.Dpi,
                string.IsNullOrWhiteSpace(settings.Suffix) ? "hdmi" : settings.Suffix);

            var result = _adbService.Shell(
                "settings put global overlay_display_devices " + Quote(value));
            if (!result.IsSuccess)
                throw new InvalidOperationException(
                    LocalizationService.Format(
                        "Error.Display.ApplyFailed",
                        result.StandardError));

            _logService.Info(LocalizationService.Format(
                "Log.Display.SettingApplied",
                value));
            return WaitForCreatedDisplay(settings, before, creationWaitMs);
        }

        public IList<int> GetVirtualDisplayIds()
        {
            return GetVirtualDisplays().Select(display => display.Id).ToList();
        }

        public IList<DisplayInfo> GetVirtualDisplays()
        {
            var result = _adbService.Shell("dumpsys display");
            if (!result.IsSuccess) return new List<DisplayInfo>();

            return ParseDisplays(result.StandardOutput)
                .Where(display => display.Id > 0)
                .GroupBy(display => display.Id)
                .Select(group => group.First())
                .OrderBy(display => display.Id)
                .ToList();
        }

        public bool Reset()
        {
            var result = _adbService.Shell(
                "settings put global overlay_display_devices none");
            if (result.IsSuccess)
                _logService.Info(LocalizationService.Get(
                    "Log.Display.Reset"));
            return result.IsSuccess;
        }

        private int WaitForCreatedDisplay(
            VirtualDisplaySettings settings,
            IList<DisplayInfo> before,
            int creationWaitMs)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(creationWaitMs, 1000));
            IList<DisplayInfo> after = new List<DisplayInfo>();
            IList<DisplayInfo> candidates = new List<DisplayInfo>();

            do
            {
                Thread.Sleep(250);
                after = GetVirtualDisplays();
                candidates = GetNewDisplayCandidates(before, after);
                if (candidates.Count > 0) break;
            }
            while (DateTime.UtcNow < deadline);

            LogDisplaySnapshot("after", after);
            LogDisplaySnapshot("new candidates", candidates);
            return SelectCandidate(settings, before, after, candidates);
        }

        private int SelectExistingDisplay(
            VirtualDisplaySettings settings,
            IList<DisplayInfo> displays)
        {
            var matches = MatchDisplaySettings(settings, displays).ToList();
            LogDisplaySnapshot("existing candidates", matches);

            if (matches.Count == 1)
            {
                _logService.Info(LocalizationService.Format(
                    "Log.Display.ExistingSelected",
                    matches[0].Id));
                return matches[0].Id;
            }

            if (matches.Count == 0)
            {
                if (displays.Count == 1)
                {
                    _logService.Warning(LocalizationService.Format(
                        "Log.Display.SingleFallbackSelected",
                        displays[0]));
                    return displays[0].Id;
                }

                throw new InvalidOperationException(
                    LocalizationService.Format(
                        "Error.Display.ReusableNotFound",
                        FormatDisplays(displays)));
            }

            throw new InvalidOperationException(
                LocalizationService.Format(
                    "Error.Display.ReusableAmbiguous",
                    FormatDisplays(matches)));
        }

        private int SelectCandidate(
            VirtualDisplaySettings settings,
            IList<DisplayInfo> before,
            IList<DisplayInfo> after,
            IList<DisplayInfo> candidates)
        {
            if (candidates.Count == 1)
            {
                _logService.Info(LocalizationService.Format(
                    "Log.Display.NewSelected",
                    candidates[0].Id));
                return candidates[0].Id;
            }

            if (candidates.Count > 1)
            {
                var matches = MatchDisplaySettings(settings, candidates).ToList();
                LogDisplaySnapshot("matched candidates", matches);
                if (matches.Count == 1)
                {
                    _logService.Info(LocalizationService.Format(
                        "Log.Display.NewSelected",
                        matches[0].Id));
                    return matches[0].Id;
                }
            }

            throw new InvalidOperationException(
                LocalizationService.Format(
                    "Error.Display.NewAmbiguous",
                    FormatDisplays(before),
                    FormatDisplays(after),
                    FormatDisplays(candidates)));
        }

        private static IList<DisplayInfo> GetNewDisplayCandidates(
            IList<DisplayInfo> before,
            IList<DisplayInfo> after)
        {
            var beforeIds = new HashSet<int>(before.Select(display => display.Id));
            return after
                .Where(display => !beforeIds.Contains(display.Id))
                .OrderBy(display => display.Id)
                .ToList();
        }

        private static IEnumerable<DisplayInfo> MatchDisplaySettings(
            VirtualDisplaySettings settings,
            IEnumerable<DisplayInfo> displays)
        {
            return displays.Where(display =>
                display.Width == settings.Width &&
                display.Height == settings.Height &&
                (display.Dpi == 0 || display.Dpi == settings.Dpi));
        }

        private static IList<DisplayInfo> ParseDisplays(string output)
        {
            var lines = (output ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            var displays = new List<DisplayInfo>();

            for (var index = 0; index < lines.Length; index++)
            {
                var matches = DisplayIdRegex.Matches(lines[index]);
                if (matches.Count == 0) continue;

                foreach (Match match in matches)
                {
                    int id;
                    if (!int.TryParse(match.Groups[1].Value, out id) || id <= 0)
                        continue;

                    var raw = BuildRawBlock(lines, index);
                    displays.Add(ParseDisplayInfo(id, raw));
                }
            }

            return displays;
        }

        private static DisplayInfo ParseDisplayInfo(int id, string raw)
        {
            var display = new DisplayInfo
            {
                Id = id,
                RawText = raw
            };

            var name = NameRegex.Match(raw);
            if (name.Success) display.Name = name.Groups[1].Value.Trim();

            var flags = FlagsRegex.Match(raw);
            if (flags.Success) display.Flags = flags.Groups[1].Value.Trim();

            var size = SizeRegex.Match(raw);
            int width;
            int height;
            if (size.Success &&
                int.TryParse(size.Groups["width"].Value, out width) &&
                int.TryParse(size.Groups["height"].Value, out height))
            {
                display.Width = width;
                display.Height = height;
            }

            var dpi = DpiRegex.Match(raw);
            int dpiValue;
            if (dpi.Success)
            {
                var value = dpi.Groups["dpi"].Success
                    ? dpi.Groups["dpi"].Value
                    : dpi.Groups["density"].Value;
                if (int.TryParse(value, out dpiValue)) display.Dpi = dpiValue;
            }

            return display;
        }

        private static string BuildRawBlock(string[] lines, int center)
        {
            var start = Math.Max(0, center - 8);
            var end = Math.Min(lines.Length - 1, center + 16);
            var builder = new StringBuilder();
            for (var index = start; index <= end; index++)
                builder.AppendLine(lines[index]);
            return builder.ToString();
        }

        private void LogDisplaySnapshot(string label, IList<DisplayInfo> displays)
        {
            _logService.Info(LocalizationService.Format(
                "Log.Display.Snapshot",
                label,
                FormatDisplays(displays)));
        }

        private static string FormatDisplays(IEnumerable<DisplayInfo> displays)
        {
            var list = displays == null
                ? new List<DisplayInfo>()
                : displays.ToList();
            if (list.Count == 0) return "none";
            return string.Join("; ", list.Select(display => display.ToString()).ToArray());
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}

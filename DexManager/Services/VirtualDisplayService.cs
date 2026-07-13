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
        internal const string RetainedLeaseDataKey =
            "DexManager.VirtualDisplayLease";
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
        private static readonly Regex OverlaySizeRegex = new Regex(
            @"(?<width>\d{3,5})x(?<height>\d{3,5})/(?<dpi>\d{2,4})",
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
            return GetOverlaySetting(_adbService.TargetSerial);
        }

        public string GetOverlaySetting(string serial)
        {
            var result = ShellForSerial(
                serial,
                "settings get global overlay_display_devices");
            if (!result.IsSuccess)
                throw new InvalidOperationException(
                    LocalizationService.Format(
                        "Error.Display.QuerySettingFailed",
                        result.StandardError));
            return result.StandardOutput.Trim();
        }

        public VirtualDisplayLease EnsureVirtualDisplay(
            string serial,
            VirtualDisplaySettings settings,
            int creationWaitMs,
            Func<bool> cancellationRequested)
        {
            if (string.IsNullOrWhiteSpace(serial))
                throw new ArgumentException("Device serial is empty.", "serial");
            if (settings == null) throw new ArgumentNullException("settings");

            var current = GetOverlaySetting(serial);
            var hasSetting = HasOverlaySetting(current);
            var before = GetVirtualDisplays(serial);
            LogDisplaySnapshot("before", before);
            var value = BuildOverlaySetting(settings);

            DisplayInfo existingOverlay = null;
            if (hasSetting)
            {
                existingOverlay = FindExistingOverlayDisplay(current, before);
                if (existingOverlay != null)
                {
                    _logService.Info(LocalizationService.Format(
                        "Log.Display.ExistingFound",
                        existingOverlay.Id,
                        existingOverlay.Width,
                        existingOverlay.Height,
                        existingOverlay.Dpi));
                    _logService.Info(LocalizationService.Format(
                        "Log.Display.RequestedValues",
                        settings.Width,
                        settings.Height,
                        settings.Dpi));

                    if (MatchesDisplaySettings(settings, existingOverlay))
                    {
                        _logService.Info(LocalizationService.Get(
                            "Log.Display.ReuseMatched"));
                        _logService.Info(LocalizationService.Format(
                            "Log.Display.ExistingSelected",
                            existingOverlay.Id));
                        return new VirtualDisplayLease
                        {
                            Serial = serial,
                            DisplayId = existingOverlay.Id,
                            PreviousOverlaySetting = "none",
                            AppliedOverlaySetting = current,
                            OwnsOverlaySetting = true,
                            ReusedExistingDisplay = true
                        };
                    }

                    _logService.Info(LocalizationService.Format(
                        "Log.Display.RecreateMismatch",
                        GetMismatchDescription(settings, existingOverlay)));
                }
                else
                {
                    _logService.Warning(LocalizationService.Format(
                        "Log.Display.ExistingNotResolved",
                        current));
                }
                _logService.Info(LocalizationService.Format(
                    "Log.Display.ReplacingSetting",
                    current,
                    value));
                before = RemoveExistingOverlay(
                    serial,
                    before,
                    creationWaitMs,
                    cancellationRequested,
                    existingOverlay == null ? 0 : existingOverlay.Id);
            }

            var result = ShellForSerial(
                serial,
                "settings put global overlay_display_devices " + Quote(value));
            if (!result.IsSuccess)
                throw new InvalidOperationException(
                    LocalizationService.Format(
                        "Error.Display.ApplyFailed",
                        result.StandardError));

            _logService.Info(LocalizationService.Format(
                "Log.Display.SettingApplied",
                value));
            var lease = new VirtualDisplayLease
            {
                Serial = serial,
                PreviousOverlaySetting = "none",
                AppliedOverlaySetting = value,
                OwnsOverlaySetting = true,
                ReusedExistingDisplay = false
            };
            try
            {
                lease.DisplayId = WaitForCreatedDisplay(
                    serial,
                    settings,
                    before,
                    creationWaitMs,
                    cancellationRequested);
                return lease;
            }
            catch (Exception ex)
            {
                if (!Release(lease))
                    ex.Data[RetainedLeaseDataKey] = lease;
                throw;
            }
        }

        public IList<int> GetVirtualDisplayIds()
        {
            return GetVirtualDisplays().Select(display => display.Id).ToList();
        }

        public IList<DisplayInfo> GetVirtualDisplays()
        {
            return GetVirtualDisplays(_adbService.TargetSerial);
        }

        public IList<DisplayInfo> GetVirtualDisplays(string serial)
        {
            var result = ShellForSerial(serial, "dumpsys display");
            if (!result.IsSuccess)
                throw new InvalidOperationException(
                    LocalizationService.Format(
                        "Error.Display.QueryFailed",
                        result.StandardError));

            return ParseDisplays(result.StandardOutput)
                .Where(display => display.Id > 0)
                .GroupBy(display => display.Id)
                .Select(group => group
                    .OrderByDescending(GetDisplayCompleteness)
                    .First())
                .OrderBy(display => display.Id)
                .ToList();
        }

        public bool Reset()
        {
            return Reset(_adbService.TargetSerial);
        }

        public bool Reset(string serial)
        {
            var result = ShellForSerial(
                serial,
                "settings put global overlay_display_devices none");
            if (result.IsSuccess)
            {
                _logService.Info(LocalizationService.Get(
                    "Log.Display.Reset"));
            }
            else
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.Display.RestoreFailed",
                    result.StandardError));
            }
            return result.IsSuccess;
        }

        public bool Release(VirtualDisplayLease lease)
        {
            if (lease == null) return true;
            try
            {
                // Normal DeX cleanup is intentionally unconditional. The
                // overlay belongs to the phone setting, not to a process, so
                // restoring a previous value can leave a stale DeX screen.
                var result = ShellForSerial(
                    lease.Serial,
                    "settings put global overlay_display_devices none");
                if (!result.IsSuccess)
                {
                    _logService.Warning(LocalizationService.Format(
                        "Log.Display.RestoreFailed",
                        result.StandardError));
                    return false;
                }

                _logService.Info(LocalizationService.Get(
                    "Log.Display.Restored"));
                lease.OwnsOverlaySetting = false;
                return true;
            }
            catch (InvalidOperationException ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.Display.RestoreDeferred",
                    ex.Message));
                return false;
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Display.RestoreException"),
                    ex);
                return false;
            }
        }

        private ProcessResult ShellForSerial(string serial, string command)
        {
            return string.IsNullOrWhiteSpace(serial)
                ? _adbService.Shell(command)
                : _adbService.ShellForSerial(serial, command, true);
        }

        private static string BuildOverlaySetting(
            VirtualDisplaySettings settings)
        {
            return string.Format(
                "{0}x{1}/{2},{3}",
                settings.Width,
                settings.Height,
                settings.Dpi,
                string.IsNullOrWhiteSpace(settings.Suffix)
                    ? "hdmi"
                    : settings.Suffix);
        }

        private static bool HasOverlaySetting(string value)
        {
            return !IsMissingOverlaySetting(value) &&
                !string.Equals(
                    value,
                    "none",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMissingOverlaySetting(string value)
        {
            return string.IsNullOrWhiteSpace(value) ||
                string.Equals(
                    value,
                    "null",
                    StringComparison.OrdinalIgnoreCase);
        }

        private IList<DisplayInfo> RemoveExistingOverlay(
            string serial,
            IList<DisplayInfo> before,
            int waitMs,
            Func<bool> cancellationRequested,
            int existingDisplayId)
        {
            var result = ShellForSerial(
                serial,
                "settings put global overlay_display_devices none");
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(
                    LocalizationService.Format(
                        "Error.Display.ResetFailed",
                        result.StandardError));
            }

            _logService.Info(LocalizationService.Get("Log.Display.Reset"));
            var deadline = DateTime.UtcNow.AddMilliseconds(
                Math.Max(1000, Math.Min(waitMs, 3000)));
            IList<DisplayInfo> after = before;

            do
            {
                if (cancellationRequested != null && cancellationRequested())
                    throw new OperationCanceledException();

                Thread.Sleep(150);
                after = GetVirtualDisplays(serial);
                var setting = GetOverlaySetting(serial);
                if (!HasOverlaySetting(setting) &&
                    (existingDisplayId <= 0 ||
                        !after.Any(display =>
                            display.Id == existingDisplayId)))
                {
                    LogDisplaySnapshot("after reset", after);
                    return after;
                }
            }
            while (DateTime.UtcNow < deadline);

            throw new InvalidOperationException(
                LocalizationService.Get("Error.Display.ResetTimedOut"));
        }

        private DisplayInfo FindExistingOverlayDisplay(
            string setting,
            IList<DisplayInfo> displays)
        {
            int width;
            int height;
            int dpi;
            if (!TryParseOverlaySize(setting, out width, out height, out dpi))
            {
                return null;
            }

            var numericMatches = (displays ?? new List<DisplayInfo>())
                .Where(display => display.Width == width &&
                    display.Height == height &&
                    display.Dpi == dpi)
                .ToList();
            var overlayMatches = numericMatches
                .Where(IsOverlayDisplay)
                .ToList();

            if (overlayMatches.Count == 1) return overlayMatches[0];
            if (overlayMatches.Count > 1)
            {
                throw new InvalidOperationException(
                    LocalizationService.Format(
                        "Error.Display.ReusableAmbiguous",
                        FormatDisplays(overlayMatches)));
            }

            // Older Android dumps do not always expose the adapter/name in
            // the mDisplayId block. A single numeric match is still safe.
            if (numericMatches.Count == 1) return numericMatches[0];
            if (numericMatches.Count == 0) return null;

            throw new InvalidOperationException(
                LocalizationService.Format(
                    "Error.Display.ReusableAmbiguous",
                    FormatDisplays(numericMatches)));
        }

        private static bool TryParseOverlaySize(
            string setting,
            out int width,
            out int height,
            out int dpi)
        {
            width = 0;
            height = 0;
            dpi = 0;
            var match = OverlaySizeRegex.Match(setting ?? string.Empty);
            return match.Success &&
                int.TryParse(match.Groups["width"].Value, out width) &&
                int.TryParse(match.Groups["height"].Value, out height) &&
                int.TryParse(match.Groups["dpi"].Value, out dpi);
        }

        private static bool IsOverlayDisplay(DisplayInfo display)
        {
            if (display == null) return false;
            var marker = (display.Name ?? string.Empty) + " " +
                (display.RawText ?? string.Empty);
            return marker.IndexOf(
                    "overlay",
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool MatchesDisplaySettings(
            VirtualDisplaySettings settings,
            DisplayInfo display)
        {
            return display != null &&
                display.Width == settings.Width &&
                display.Height == settings.Height &&
                display.Dpi == settings.Dpi;
        }

        private static string GetMismatchDescription(
            VirtualDisplaySettings settings,
            DisplayInfo display)
        {
            var resolutionMismatch = display.Width != settings.Width ||
                display.Height != settings.Height;
            var dpiMismatch = display.Dpi != settings.Dpi;
            if (resolutionMismatch && dpiMismatch)
                return LocalizationService.Get(
                    "Display.Mismatch.ResolutionAndDpi");
            return resolutionMismatch
                ? LocalizationService.Get("Display.Mismatch.Resolution")
                : LocalizationService.Get("Display.Mismatch.Dpi");
        }

        private int WaitForCreatedDisplay(
            string serial,
            VirtualDisplaySettings settings,
            IList<DisplayInfo> before,
            int creationWaitMs,
            Func<bool> cancellationRequested)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(creationWaitMs, 1000));
            IList<DisplayInfo> after = new List<DisplayInfo>();
            IList<DisplayInfo> candidates = new List<DisplayInfo>();

            do
            {
                if (cancellationRequested != null &&
                    cancellationRequested())
                {
                    throw new OperationCanceledException();
                }
                Thread.Sleep(250);
                if (cancellationRequested != null &&
                    cancellationRequested())
                {
                    throw new OperationCanceledException();
                }
                after = GetVirtualDisplays(serial);
                candidates = GetNewDisplayCandidates(before, after);
                var matches = MatchDisplaySettings(settings, candidates).ToList();
                if (matches.Count == 1)
                {
                    LogDisplaySnapshot("after", after);
                    LogDisplaySnapshot("new candidates", candidates);
                    _logService.Info(LocalizationService.Format(
                        "Log.Display.NewSelected",
                        matches[0].Id));
                    return matches[0].Id;
                }
            }
            while (DateTime.UtcNow < deadline);

            LogDisplaySnapshot("after", after);
            LogDisplaySnapshot("new candidates", candidates);
            return SelectCandidate(settings, before, after, candidates);
        }

        private int SelectCandidate(
            VirtualDisplaySettings settings,
            IList<DisplayInfo> before,
            IList<DisplayInfo> after,
            IList<DisplayInfo> candidates)
        {
            if (candidates.Count > 0)
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
                display.Dpi == settings.Dpi);
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
            var start = center;
            var end = Math.Min(lines.Length - 1, center + 24);
            for (var index = center + 1; index <= end; index++)
            {
                if (!DisplayIdRegex.IsMatch(lines[index])) continue;
                end = index - 1;
                break;
            }
            var builder = new StringBuilder();
            for (var index = start; index <= end; index++)
                builder.AppendLine(lines[index]);
            return builder.ToString();
        }

        private static int GetDisplayCompleteness(DisplayInfo display)
        {
            var score = 0;
            if (display.Width > 0 && display.Height > 0) score += 4;
            if (display.Dpi > 0) score += 2;
            if (!string.IsNullOrWhiteSpace(display.Name)) score++;
            if (!string.IsNullOrWhiteSpace(display.Flags)) score++;
            return score;
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

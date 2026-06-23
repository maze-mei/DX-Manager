using System;
using System.Collections.Generic;
using System.Linq;
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

        public bool EnsureVirtualDisplay(
            VirtualDisplaySettings settings,
            int creationWaitMs)
        {
            if (settings == null) throw new ArgumentNullException("settings");

            var current = GetOverlaySetting();
            var hasSetting = !string.IsNullOrWhiteSpace(current) &&
                !string.Equals(current, "none", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(current, "null", StringComparison.OrdinalIgnoreCase);

            if (hasSetting && settings.ReuseExistingDisplay)
            {
                _logService.Info("기존 가상 디스플레이 설정을 재사용합니다: " + current);
                return true;
            }

            var value = string.Format(
                "{0}x{1}/{2},{3}",
                settings.Width,
                settings.Height,
                settings.Dpi,
                string.IsNullOrWhiteSpace(settings.Suffix) ? "hdmi" : settings.Suffix);

            var result = _adbService.Shell(
                "settings put global overlay_display_devices " + Quote(value));
            if (!result.IsSuccess) return false;

            _logService.Info("가상 디스플레이 설정을 적용했습니다: " + value);
            Thread.Sleep(Math.Max(creationWaitMs, 0));
            return true;
        }

        public IList<int> GetVirtualDisplayIds()
        {
            var result = _adbService.Shell("dumpsys display");
            if (!result.IsSuccess) return new List<int>();

            return DisplayIdRegex.Matches(result.StandardOutput)
                .Cast<Match>()
                .Select(match => int.Parse(match.Groups[1].Value))
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
        }

        public int GetPreferredVirtualDisplayId()
        {
            var ids = GetVirtualDisplayIds();
            if (ids.Count == 0)
                throw new InvalidOperationException("가상 디스플레이 ID를 찾을 수 없습니다.");

            var selected = ids[ids.Count - 1];
            _logService.Info("가상 디스플레이 ID 선택: " + selected);
            return selected;
        }

        public bool Reset()
        {
            var result = _adbService.Shell(
                "settings put global overlay_display_devices none");
            if (result.IsSuccess)
                _logService.Info("가상 디스플레이 설정을 초기화했습니다.");
            return result.IsSuccess;
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}

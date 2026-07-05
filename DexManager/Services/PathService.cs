using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DexManager.Models;
using DexManager.Utils;
using Microsoft.Win32;

namespace DexManager.Services
{
    public sealed class PathService
    {
        private readonly SettingsService _settingsService;
        private readonly LogService _logService;
        private readonly ProcessRunner _processRunner;

        public PathService(
            SettingsService settingsService,
            LogService logService,
            ProcessRunner processRunner)
        {
            _settingsService = settingsService;
            _logService = logService;
            _processRunner = processRunner;
        }

        public string SelectAdbPath(AppSettings settings, int timeoutMs)
        {
            if (settings == null) throw new ArgumentNullException("settings");

            _logService.Info(LocalizationService.Format(
                "Log.Path.WindowsDetected",
                WindowsVersionHelper.GetDisplayName(),
                WindowsVersionHelper.CurrentVersion));

            if (settings.Paths.AdbSelectionMode == AdbSelectionMode.Manual)
            {
                return SelectRequired(
                    settings.Paths.AdbPath,
                    LocalizationService.Get(
                        "Path.Description.ManualAdb"),
                    timeoutMs);
            }

            if (WindowsVersionHelper.RequiresLegacyAdb)
            {
                return SelectRequired(
                    settings.Paths.Win7AdbPath,
                    LocalizationService.Get(
                        "Path.Description.LegacyAdb"),
                    timeoutMs);
            }

            return SelectModernAdb(settings, timeoutMs);
        }

        public bool IsAdbDirectoryInProcessPath(string adbPath)
        {
            if (string.IsNullOrWhiteSpace(adbPath)) return false;
            var adbDirectory = NormalizeDirectory(Path.GetDirectoryName(adbPath));
            var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

            return ContainsDirectory(pathValue, adbDirectory);
        }

        public bool IsAdbDirectoryInSystemPath(string adbPath)
        {
            if (string.IsNullOrWhiteSpace(adbPath)) return false;
            var adbDirectory = NormalizeDirectory(Path.GetDirectoryName(adbPath));

            using (var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment",
                false))
            {
                var pathValue = key == null
                    ? string.Empty
                    : key.GetValue("Path", string.Empty).ToString();
                return ContainsDirectory(pathValue, adbDirectory);
            }
        }

        public bool TryRegisterAdbDirectoryInSystemPath(string adbPath)
        {
            if (string.IsNullOrWhiteSpace(adbPath)) return false;
            if (!AdminHelper.IsAdministrator())
            {
                _logService.Warning(LocalizationService.Get(
                    "Log.Path.AdminRequired"));
                return false;
            }

            var adbDirectory = NormalizeDirectory(Path.GetDirectoryName(adbPath));
            using (var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment",
                true))
            {
                if (key == null)
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Path.SystemEnvironmentUnavailable"));

                var current = key.GetValue(
                    "Path",
                    string.Empty,
                    RegistryValueOptions.DoNotExpandEnvironmentNames).ToString();
                if (ContainsDirectory(current, adbDirectory)) return true;

                var updated = current.TrimEnd(';') + ";" + adbDirectory;
                key.SetValue("Path", updated, RegistryValueKind.ExpandString);
            }

            _logService.Info(LocalizationService.Format(
                "Log.Path.SystemPathRegistered",
                adbDirectory));
            return true;
        }

        private string SelectModernAdb(AppSettings settings, int timeoutMs)
        {
            var bundled = GetRunnableCandidate(
                settings.Paths.ModernAdbPath,
                LocalizationService.Get(
                    "Path.Description.BundledModernAdb"),
                timeoutMs);
            var external = GetExternalScrcpyAdb(settings, timeoutMs);

            AdbPathCandidate selected = bundled;
            if (external != null &&
                (bundled == null || IsVersionAtLeast(external.Version, bundled.Version)))
            {
                selected = external;
            }

            if (selected == null)
            {
                throw new FileNotFoundException(
                    LocalizationService.Get(
                        "Error.Path.ModernAdbNotFound"));
            }

            LogSelection(
                selected,
                LocalizationService.Get("Path.Mode.Automatic"));
            return selected.Path;
        }

        private string SelectRequired(
            string configuredPath,
            string description,
            int timeoutMs)
        {
            var candidate = GetRunnableCandidate(
                configuredPath,
                description,
                timeoutMs);
            if (candidate == null)
            {
                throw new FileNotFoundException(
                    LocalizationService.Format(
                        "Error.Path.AdbUnavailable",
                        description));
            }

            LogSelection(
                candidate,
                LocalizationService.Get("Path.Mode.Selected"));
            return candidate.Path;
        }

        private AdbPathCandidate GetExternalScrcpyAdb(
            AppSettings settings,
            int timeoutMs)
        {
            var configuredScrcpy = _settingsService.ResolvePath(
                settings.Paths.ScrcpyPath);
            var bundledScrcpy = _settingsService.ResolvePath(
                @"tools\scrcpy\scrcpy.exe");

            if (string.IsNullOrWhiteSpace(configuredScrcpy) ||
                string.Equals(
                    configuredScrcpy,
                    bundledScrcpy,
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var adbPath = Path.Combine(
                Path.GetDirectoryName(configuredScrcpy),
                "adb.exe");
            return GetRunnableCandidate(
                adbPath,
                LocalizationService.Get(
                    "Path.Description.ExternalScrcpyAdb"),
                timeoutMs,
                true);
        }

        private AdbPathCandidate GetRunnableCandidate(
            string configuredPath,
            string description,
            int timeoutMs,
            bool pathIsAbsolute = false)
        {
            if (string.IsNullOrWhiteSpace(configuredPath)) return null;

            var path = pathIsAbsolute
                ? Path.GetFullPath(configuredPath)
                : _settingsService.ResolvePath(configuredPath);
            if (!File.Exists(path))
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.Path.CandidateMissing",
                    description,
                    path));
                return null;
            }

            try
            {
                var result = _processRunner.Run(
                    path,
                    "version",
                    Path.GetDirectoryName(path),
                    Math.Max(timeoutMs, 3000),
                    false);
                if (!result.IsSuccess)
                {
                    _logService.Warning(LocalizationService.Format(
                        "Log.Path.CandidateExecutionFailed",
                        description,
                        result.StandardError));
                    return null;
                }

                return new AdbPathCandidate(
                    path,
                    description,
                    GetVersionText(result.StandardOutput),
                    GetVersionNumber(result.StandardOutput));
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.Path.CandidateUnavailable",
                    description,
                    ex.Message));
                return null;
            }
        }

        private void LogSelection(AdbPathCandidate candidate, string settingsModeLabel)
        {
            _logService.Info(LocalizationService.Format(
                "Log.Path.Selection",
                settingsModeLabel,
                candidate.Description));
            _logService.Info(LocalizationService.Format(
                "Log.Path.SelectedAdbPath",
                candidate.Path));
            _logService.Info(LocalizationService.Format(
                "Log.Path.AdbVersion",
                candidate.VersionText));
        }

        private static bool IsVersionAtLeast(Version left, Version right)
        {
            if (left == null) return false;
            if (right == null) return true;
            return left.CompareTo(right) >= 0;
        }

        private static Version GetVersionNumber(string output)
        {
            var match = Regex.Match(
                output ?? string.Empty,
                @"^\s*Version\s+(\d+(?:\.\d+){1,3})",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            Version version;
            return match.Success && Version.TryParse(match.Groups[1].Value, out version)
                ? version
                : null;
        }

        private static string GetVersionText(string output)
        {
            var line = (output ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            return string.IsNullOrWhiteSpace(line)
                ? LocalizationService.Get("Path.VersionUnavailable")
                : line.Trim();
        }

        private static string NormalizeDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) return string.Empty;
            return Path.GetFullPath(directory.Trim().Trim('"'))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool ContainsDirectory(string pathValue, string directory)
        {
            foreach (var entry in (pathValue ?? string.Empty).Split(
                new[] { ';' },
                StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(
                    NormalizeDirectory(entry),
                    directory,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class AdbPathCandidate
        {
            public AdbPathCandidate(
                string path,
                string description,
                string versionText,
                Version version)
            {
                Path = path;
                Description = description;
                VersionText = versionText;
                Version = version;
            }

            public string Path { get; private set; }
            public string Description { get; private set; }
            public string VersionText { get; private set; }
            public Version Version { get; private set; }
        }
    }
}

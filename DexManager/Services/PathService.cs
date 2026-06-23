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

            _logService.Info(
                "감지된 Windows 버전: " + WindowsVersionHelper.GetDisplayName() +
                " (" + WindowsVersionHelper.CurrentVersion + ")");

            if (settings.Paths.AdbSelectionMode == AdbSelectionMode.Manual)
            {
                return SelectRequired(
                    settings.Paths.AdbPath,
                    "수동 지정 ADB",
                    timeoutMs);
            }

            if (WindowsVersionHelper.RequiresLegacyAdb)
            {
                return SelectRequired(
                    settings.Paths.Win7AdbPath,
                    "Windows 7/8.1 레거시 ADB",
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
                _logService.Warning(
                    "시스템 PATH 등록에는 관리자 권한이 필요합니다.");
                return false;
            }

            var adbDirectory = NormalizeDirectory(Path.GetDirectoryName(adbPath));
            using (var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment",
                true))
            {
                if (key == null)
                    throw new InvalidOperationException(
                        "시스템 환경 변수를 열 수 없습니다.");

                var current = key.GetValue(
                    "Path",
                    string.Empty,
                    RegistryValueOptions.DoNotExpandEnvironmentNames).ToString();
                if (ContainsDirectory(current, adbDirectory)) return true;

                var updated = current.TrimEnd(';') + ";" + adbDirectory;
                key.SetValue("Path", updated, RegistryValueKind.ExpandString);
            }

            _logService.Info("ADB 폴더를 시스템 PATH에 등록했습니다: " + adbDirectory);
            return true;
        }

        private string SelectModernAdb(AppSettings settings, int timeoutMs)
        {
            var bundled = GetRunnableCandidate(
                settings.Paths.ModernAdbPath,
                "동봉 modern ADB",
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
                    "실행 가능한 modern adb.exe를 찾을 수 없습니다. " +
                    "동봉 ADB 또는 지정한 Scrcpy 경로를 확인하세요.");
            }

            LogSelection(selected, "자동 선택");
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
                    description + " adb.exe를 실행할 수 없습니다. 경로를 확인하세요.");
            }

            LogSelection(candidate, settingsModeLabel: "선택");
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
                "지정한 Scrcpy 폴더의 ADB",
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
                _logService.Warning(description + " 파일이 없습니다: " + path);
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
                    _logService.Warning(
                        description + " 실행 실패: " + result.StandardError);
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
                _logService.Warning(description + " 실행 불가: " + ex.Message);
                return null;
            }
        }

        private void LogSelection(AdbPathCandidate candidate, string settingsModeLabel)
        {
            _logService.Info(
                "ADB " + settingsModeLabel + ": " + candidate.Description);
            _logService.Info("선택된 ADB 경로: " + candidate.Path);
            _logService.Info("ADB 버전: " + candidate.VersionText);
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
            return string.IsNullOrWhiteSpace(line) ? "버전 출력 없음" : line.Trim();
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

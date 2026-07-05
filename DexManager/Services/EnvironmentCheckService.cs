using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DexManager.Models;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed class EnvironmentCheckService
    {
        private readonly AdbService _adbService;
        private readonly ScrcpyService _scrcpyService;
        private readonly PathService _pathService;
        private readonly LogService _logService;
        private readonly SettingsService _settingsService;
        private readonly AppSettings _settings;

        public EnvironmentCheckService(
            AdbService adbService,
            ScrcpyService scrcpyService,
            PathService pathService,
            LogService logService,
            SettingsService settingsService,
            AppSettings settings)
        {
            _adbService = adbService;
            _scrcpyService = scrcpyService;
            _pathService = pathService;
            _logService = logService;
            _settingsService = settingsService;
            _settings = settings;
        }

        public IList<EnvironmentCheckItem> Run()
        {
            var results = new List<EnvironmentCheckItem>();
            AddFileCheck(
                results,
                LocalizationService.Get("Environment.AdbFile"),
                _adbService.AdbPath);
            AddFileCheck(
                results,
                LocalizationService.Get("Environment.ScrcpyFile"),
                _scrcpyService.ScrcpyPath);
            AddAdbVersionCheck(results);

            results.Add(new EnvironmentCheckItem
            {
                Name = LocalizationService.Get(
                    "Environment.WindowsVersion"),
                Status = EnvironmentCheckStatus.Passed,
                Message = WindowsVersionHelper.GetDisplayName() +
                    " (" + WindowsVersionHelper.CurrentVersion + ")"
            });

            var inPath = _pathService.IsAdbDirectoryInProcessPath(
                _adbService.AdbPath);
            var inSystemPath = _pathService.IsAdbDirectoryInSystemPath(
                _adbService.AdbPath);
            results.Add(new EnvironmentCheckItem
            {
                Name = "ADB PATH",
                Status = inSystemPath
                    ? EnvironmentCheckStatus.Passed
                    : EnvironmentCheckStatus.Warning,
                Message = inSystemPath
                    ? LocalizationService.Get(
                        "Environment.PathSystem")
                    : inPath
                        ? LocalizationService.Get(
                            "Environment.PathProcess")
                        : LocalizationService.Get(
                            "Environment.PathAbsolute")
            });

            results.Add(new EnvironmentCheckItem
            {
                Name = LocalizationService.Get("Environment.Admin"),
                Status = AdminHelper.IsAdministrator()
                    ? EnvironmentCheckStatus.Passed
                    : EnvironmentCheckStatus.Warning,
                Message = AdminHelper.IsAdministrator()
                    ? LocalizationService.Get("Environment.AdminYes")
                    : LocalizationService.Get("Environment.AdminNo")
            });

            try
            {
                var devices = _adbService.GetDevices();
                AddDeviceResult(results, devices);
                AddDeviceScreenshotFolderCheck(results, devices);
            }
            catch (Exception ex)
            {
                results.Add(new EnvironmentCheckItem
                {
                    Name = LocalizationService.Get(
                        "Environment.AdbDevice"),
                    Status = EnvironmentCheckStatus.Failed,
                    Message = LocalizationService.Format(
                        "Environment.DeviceQueryFailed",
                        ex.Message)
                });
            }

            AddPcScreenshotFolderCheck(results);
            _logService.Info(LocalizationService.Get(
                "Log.Environment.Completed"));
            return results;
        }

        private void AddAdbVersionCheck(
            ICollection<EnvironmentCheckItem> results)
        {
            try
            {
                var version = _adbService.GetVersion();
                var firstLine = (version.StandardOutput ?? string.Empty)
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                    .FirstOrDefault();
                results.Add(new EnvironmentCheckItem
                {
                    Name = LocalizationService.Get(
                        "Environment.AdbVersion"),
                    Status = version.IsSuccess
                        ? EnvironmentCheckStatus.Passed
                        : EnvironmentCheckStatus.Failed,
                    Message = version.IsSuccess
                        ? firstLine
                        : version.StandardError
                });
            }
            catch (Exception ex)
            {
                results.Add(new EnvironmentCheckItem
                {
                    Name = LocalizationService.Get(
                        "Environment.AdbVersion"),
                    Status = EnvironmentCheckStatus.Failed,
                    Message = ex.Message
                });
            }
        }

        private void AddPcScreenshotFolderCheck(
            ICollection<EnvironmentCheckItem> results)
        {
            var path = _settingsService.ResolvePath(
                _settings.Paths.ScreenshotFolder);
            try
            {
                Directory.CreateDirectory(path);
                var testPath = Path.Combine(
                    path,
                    ".dx_manager_write_test");
                File.WriteAllText(testPath, "ok");
                File.Delete(testPath);
                results.Add(new EnvironmentCheckItem
                {
                    Name = LocalizationService.Get(
                        "Environment.PcCaptureFolder"),
                    Status = EnvironmentCheckStatus.Passed,
                    Message = path
                });
            }
            catch (Exception ex)
            {
                results.Add(new EnvironmentCheckItem
                {
                    Name = LocalizationService.Get(
                        "Environment.PcCaptureFolder"),
                    Status = EnvironmentCheckStatus.Failed,
                    Message = LocalizationService.Format(
                        "Environment.FolderWriteFailed",
                        path,
                        ex.Message)
                });
            }
        }

        private void AddDeviceScreenshotFolderCheck(
            ICollection<EnvironmentCheckItem> results,
            IList<AdbDeviceInfo> devices)
        {
            var authorized = devices.Any(
                device => device.Status == AdbDeviceStatus.Device);
            if (!authorized)
            {
                results.Add(new EnvironmentCheckItem
                {
                    Name = LocalizationService.Get(
                        "Environment.DeviceCaptureFolder"),
                    Status = EnvironmentCheckStatus.Warning,
                    Message = LocalizationService.Get(
                        "Environment.DeviceFolderSkipped")
                });
                return;
            }

            var folder = _settings.Paths.DeviceScreenshotFolder;
            var testFile = folder.TrimEnd('/') +
                "/.dx_manager_write_test";
            var command = "mkdir -p " + ShellQuote(folder) +
                " && touch " + ShellQuote(testFile) +
                " && rm -f " + ShellQuote(testFile);
            var result = _adbService.Shell(command, false);
            results.Add(new EnvironmentCheckItem
            {
                Name = LocalizationService.Get(
                    "Environment.DeviceCaptureFolder"),
                Status = result.IsSuccess
                    ? EnvironmentCheckStatus.Passed
                    : EnvironmentCheckStatus.Failed,
                Message = result.IsSuccess
                    ? folder
                    : LocalizationService.Format(
                        "Environment.DeviceFolderFailed",
                        result.StandardError)
            });
        }

        private static string ShellQuote(string value)
        {
            return "'" + (value ?? string.Empty)
                .Replace("'", "'\\''") + "'";
        }

        private static void AddFileCheck(
            ICollection<EnvironmentCheckItem> results,
            string name,
            string path)
        {
            var exists = File.Exists(path);
            results.Add(new EnvironmentCheckItem
            {
                Name = name,
                Status = exists
                    ? EnvironmentCheckStatus.Passed
                    : EnvironmentCheckStatus.Failed,
                Message = exists
                    ? path
                    : LocalizationService.Format(
                        "Environment.FileMissing",
                        path)
            });
        }

        private static void AddDeviceResult(
            ICollection<EnvironmentCheckItem> results,
            IList<AdbDeviceInfo> devices)
        {
            var authorized = devices.FirstOrDefault(
                device => device.Status == AdbDeviceStatus.Device);
            if (authorized != null)
            {
                results.Add(new EnvironmentCheckItem
                {
                    Name = LocalizationService.Get(
                        "Environment.AdbDevice"),
                    Status = EnvironmentCheckStatus.Passed,
                    Message = LocalizationService.Format(
                        "Environment.DeviceAuthorized",
                        authorized.Serial)
                });
                return;
            }

            var unauthorized = devices.FirstOrDefault(
                device => device.Status == AdbDeviceStatus.Unauthorized);
            if (unauthorized != null)
            {
                results.Add(new EnvironmentCheckItem
                {
                    Name = LocalizationService.Get(
                        "Environment.AdbDevice"),
                    Status = EnvironmentCheckStatus.Warning,
                    Message = LocalizationService.Format(
                        "Environment.DeviceUnauthorized",
                        unauthorized.Serial)
                });
                return;
            }

            var offline = devices.FirstOrDefault(
                device => device.Status == AdbDeviceStatus.Offline);
            if (offline != null)
            {
                results.Add(new EnvironmentCheckItem
                {
                    Name = LocalizationService.Get(
                        "Environment.AdbDevice"),
                    Status = EnvironmentCheckStatus.Warning,
                    Message = LocalizationService.Get(
                        "Environment.DeviceOfflineMessage")
                });
                return;
            }

            results.Add(new EnvironmentCheckItem
            {
                Name = LocalizationService.Get(
                    "Environment.AdbDevice"),
                Status = EnvironmentCheckStatus.Failed,
                Message = LocalizationService.Get(
                    "Environment.NoDevice")
            });
        }
    }
}

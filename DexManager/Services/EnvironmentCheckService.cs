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

        public EnvironmentCheckService(
            AdbService adbService,
            ScrcpyService scrcpyService,
            PathService pathService,
            LogService logService)
        {
            _adbService = adbService;
            _scrcpyService = scrcpyService;
            _pathService = pathService;
            _logService = logService;
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
            }
            catch (System.Exception ex)
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

            _logService.Info("환경 점검을 완료했습니다.");
            return results;
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

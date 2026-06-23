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
            AddFileCheck(results, "ADB 파일", _adbService.AdbPath);
            AddFileCheck(results, "Scrcpy 파일", _scrcpyService.ScrcpyPath);

            results.Add(new EnvironmentCheckItem
            {
                Name = "Windows 버전",
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
                    ? "시스템 PATH에 ADB 폴더가 등록되어 있습니다."
                    : inPath
                        ? "현재 프로세스 PATH에만 있습니다. 시스템 등록은 선택 사항입니다."
                        : "PATH에는 없지만 프로그램은 절대 경로로 ADB를 실행할 수 있습니다."
            });

            results.Add(new EnvironmentCheckItem
            {
                Name = "관리자 권한",
                Status = AdminHelper.IsAdministrator()
                    ? EnvironmentCheckStatus.Passed
                    : EnvironmentCheckStatus.Warning,
                Message = AdminHelper.IsAdministrator()
                    ? "관리자 권한으로 실행 중입니다."
                    : "일반 권한입니다. 시스템 PATH 등록 시 관리자 실행이 필요합니다."
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
                    Name = "ADB 장치",
                    Status = EnvironmentCheckStatus.Failed,
                    Message = "ADB 장치 조회 실패: " + ex.Message
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
                Message = exists ? path : "파일을 찾을 수 없습니다: " + path
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
                    Name = "ADB 장치",
                    Status = EnvironmentCheckStatus.Passed,
                    Message = "연결 및 승인됨: " + authorized.Serial
                });
                return;
            }

            var unauthorized = devices.FirstOrDefault(
                device => device.Status == AdbDeviceStatus.Unauthorized);
            if (unauthorized != null)
            {
                results.Add(new EnvironmentCheckItem
                {
                    Name = "ADB 장치",
                    Status = EnvironmentCheckStatus.Warning,
                    Message = "휴대폰 RSA 인증창을 허용하십시오: " + unauthorized.Serial
                });
                return;
            }

            var offline = devices.FirstOrDefault(
                device => device.Status == AdbDeviceStatus.Offline);
            if (offline != null)
            {
                results.Add(new EnvironmentCheckItem
                {
                    Name = "ADB 장치",
                    Status = EnvironmentCheckStatus.Warning,
                    Message = "장치가 offline 상태입니다. USB를 다시 연결하십시오."
                });
                return;
            }

            results.Add(new EnvironmentCheckItem
            {
                Name = "ADB 장치",
                Status = EnvironmentCheckStatus.Failed,
                Message =
                    "장치가 없습니다. USB 케이블, 개발자 옵션, USB 디버깅, " +
                    "RSA 승인, 파일 전송 모드, 삼성 USB 드라이버를 확인하십시오."
            });
        }
    }
}

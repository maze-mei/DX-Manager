using System;
using System.Threading;
using System.Threading.Tasks;
using DexManager.Models;

namespace DexManager.Services
{
    public sealed class DexOrchestrator
    {
        private readonly AdbService _adbService;
        private readonly VirtualDisplayService _virtualDisplayService;
        private readonly ScrcpyService _scrcpyService;
        private readonly SettingsService _settingsService;
        private readonly LogService _logService;
        private readonly AppSettings _settings;
        private int _operationRunning;

        public DexOrchestrator(
            AdbService adbService,
            VirtualDisplayService virtualDisplayService,
            ScrcpyService scrcpyService,
            SettingsService settingsService,
            LogService logService,
            AppSettings settings)
        {
            _adbService = adbService;
            _virtualDisplayService = virtualDisplayService;
            _scrcpyService = scrcpyService;
            _settingsService = settingsService;
            _logService = logService;
            _settings = settings;
        }

        public bool IsRunning
        {
            get { return _scrcpyService.IsRunning; }
        }

        public Task StartAsync()
        {
            return Task.Run((Action)StartCore);
        }

        public Task StopAsync()
        {
            return Task.Run((Action)StopCore);
        }

        public Task ShutdownAsync()
        {
            return Task.Run((Action)ShutdownCore);
        }

        public Task<bool> ApplyRuntimeSettingsAsync()
        {
            return Task.Run((Func<bool>)ApplyRuntimeSettingsCore);
        }

        private void StartCore()
        {
            if (Interlocked.Exchange(ref _operationRunning, 1) == 1)
            {
                _logService.Warning("DeX 작업이 이미 진행 중입니다.");
                return;
            }

            try
            {
                if (_scrcpyService.IsRunning)
                {
                    _logService.Warning("DeX가 이미 실행 중입니다.");
                    return;
                }

                if (!_adbService.IsAuthorizedDeviceConnected())
                    throw new InvalidOperationException(
                        "승인된 ADB 장치가 연결되어 있지 않습니다.");

                if (!_virtualDisplayService.EnsureVirtualDisplay(
                    _settings.VirtualDisplay,
                    _settings.Timing.ConnectedStartDelayMs))
                {
                    throw new InvalidOperationException(
                        "가상 디스플레이를 생성하지 못했습니다.");
                }

                var displayId = _virtualDisplayService.GetPreferredVirtualDisplayId();

                _scrcpyService.Start(_settings.Scrcpy, displayId);
                SaveLastSuccess(displayId);
                _logService.Info("DeX 실행 흐름을 완료했습니다.");
            }
            catch (Exception ex)
            {
                _logService.Error("DeX 시작에 실패했습니다.", ex);
                throw;
            }
            finally
            {
                Interlocked.Exchange(ref _operationRunning, 0);
            }
        }

        private void StopCore()
        {
            if (Interlocked.Exchange(ref _operationRunning, 1) == 1)
            {
                _logService.Warning("DeX 작업이 이미 진행 중입니다.");
                return;
            }

            try
            {
                _scrcpyService.Stop();

                if (_settings.Features.ResetVirtualDisplayOnStop)
                    _virtualDisplayService.Reset();

                if (_settings.Features.DisableStayAwakeOnStop)
                {
                    _adbService.Shell(
                        "settings put global stay_on_while_plugged_in 0");
                }

                _logService.Info("DeX 종료 정리를 완료했습니다.");
            }
            finally
            {
                Interlocked.Exchange(ref _operationRunning, 0);
            }
        }

        private bool ApplyRuntimeSettingsCore()
        {
            if (Interlocked.Exchange(ref _operationRunning, 1) == 1)
            {
                _logService.Warning("실행 설정 적용 작업이 이미 진행 중입니다.");
                return false;
            }

            try
            {
                if (!_adbService.IsAuthorizedDeviceConnected())
                {
                    _logService.Warning(
                        "실행 설정은 저장했지만 승인된 ADB 장치가 없어 즉시 적용하지 않았습니다.");
                    return false;
                }

                _logService.Info("새 실행 설정 적용을 위해 기존 가상화면을 제거합니다.");
                _scrcpyService.Stop();
                if (!_virtualDisplayService.Reset())
                    throw new InvalidOperationException("기존 가상화면을 제거하지 못했습니다.");

                Thread.Sleep(1000);

                if (!_virtualDisplayService.EnsureVirtualDisplay(
                    _settings.VirtualDisplay,
                    _settings.Timing.ConnectedStartDelayMs))
                {
                    throw new InvalidOperationException(
                        "새 가상 디스플레이를 생성하지 못했습니다.");
                }

                var displayId = _virtualDisplayService.GetPreferredVirtualDisplayId();
                _scrcpyService.Start(_settings.Scrcpy, displayId);
                SaveLastSuccess(displayId);
                _logService.Info("새 실행 설정을 즉시 적용했습니다.");
                return true;
            }
            catch (Exception ex)
            {
                _logService.Error("새 실행 설정을 즉시 적용하지 못했습니다.", ex);
                throw;
            }
            finally
            {
                Interlocked.Exchange(ref _operationRunning, 0);
            }
        }

        private void ShutdownCore()
        {
            if (Interlocked.Exchange(ref _operationRunning, 1) == 1)
            {
                _logService.Warning("종료 정리 작업이 이미 진행 중입니다.");
                return;
            }

            try
            {
                _scrcpyService.Stop();
                _virtualDisplayService.Reset();
                _adbService.Shell(
                    "settings put global stay_on_while_plugged_in 0");
                Thread.Sleep(500);
                _logService.Info("프로그램 종료 전 ADB 정리를 완료했습니다.");
            }
            catch (Exception ex)
            {
                _logService.Error("프로그램 종료 전 ADB 정리에 실패했습니다.", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _operationRunning, 0);
            }
        }

        private void SaveLastSuccess(int displayId)
        {
            _settings.LastSuccess.Width = _settings.VirtualDisplay.Width;
            _settings.LastSuccess.Height = _settings.VirtualDisplay.Height;
            _settings.LastSuccess.Dpi = _settings.VirtualDisplay.Dpi;
            _settings.LastSuccess.AdbPath = _adbService.AdbPath;
            _settings.LastSuccess.ScrcpyPath = _scrcpyService.ScrcpyPath;
            _settings.LastSuccess.ScrcpyArguments =
                _scrcpyService.BuildArguments(_settings.Scrcpy, displayId);
            _settings.LastSuccess.DisplayId = displayId;
            _settings.LastSuccess.SavedAtUtc = DateTime.UtcNow.ToString("o");
            _settingsService.Save(_settings);
        }
    }
}

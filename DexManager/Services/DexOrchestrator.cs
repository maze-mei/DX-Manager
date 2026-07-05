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
        private ManagedDisplaySession _currentSession;

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

        public ManagedDisplaySession CurrentSession
        {
            get { return _currentSession; }
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
                _logService.Warning(LocalizationService.Get(
                    "Log.Dex.OperationInProgress"));
                return;
            }

            try
            {
                if (_scrcpyService.IsRunning)
                {
                    _logService.Warning(LocalizationService.Get(
                        "Log.Dex.AlreadyRunning"));
                    return;
                }

                if (!_adbService.IsAuthorizedDeviceConnected())
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Dex.NoAuthorizedDevice"));

                var displayId = _virtualDisplayService.EnsureVirtualDisplay(
                    _settings.VirtualDisplay,
                    _settings.Timing.ConnectedStartDelayMs);
                _scrcpyService.Start(_settings.Scrcpy, displayId);
                TrackSession("DeX", displayId);
                SaveLastSuccess(displayId);
                _logService.Info(LocalizationService.Get(
                    "Log.Dex.StartCompleted"));
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get("Log.Dex.StartFailed"),
                    ex);
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
                _logService.Warning(LocalizationService.Get(
                    "Log.Dex.OperationInProgress"));
                return;
            }

            try
            {
                _scrcpyService.Stop();
                ClearSession();

                if (_settings.Features.ResetVirtualDisplayOnStop)
                    _virtualDisplayService.Reset();

                if (_settings.Features.DisableStayAwakeOnStop)
                {
                    _adbService.Shell(
                        "settings put global stay_on_while_plugged_in 0");
                }

                _logService.Info(LocalizationService.Get(
                    "Log.Dex.StopCleanupCompleted"));
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
                _logService.Warning(LocalizationService.Get(
                    "Log.Dex.ApplyInProgress"));
                return false;
            }

            try
            {
                if (!_adbService.IsAuthorizedDeviceConnected())
                {
                    _logService.Warning(LocalizationService.Get(
                        "Log.Dex.ApplyDeferredNoDevice"));
                    return false;
                }

                _logService.Info(LocalizationService.Get(
                    "Log.Dex.RemovingDisplayForApply"));
                _scrcpyService.Stop();
                ClearSession();
                if (!_virtualDisplayService.Reset())
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Dex.DisplayResetFailed"));

                Thread.Sleep(1000);

                var displayId = _virtualDisplayService.EnsureVirtualDisplay(
                    _settings.VirtualDisplay,
                    _settings.Timing.ConnectedStartDelayMs);
                _scrcpyService.Start(_settings.Scrcpy, displayId);
                TrackSession("DeX", displayId);
                SaveLastSuccess(displayId);
                _logService.Info(LocalizationService.Get(
                    "Log.Dex.ApplyCompleted"));
                return true;
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get("Log.Dex.ApplyFailed"),
                    ex);
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
                _logService.Warning(LocalizationService.Get(
                    "Log.Dex.ShutdownInProgress"));
                return;
            }

            try
            {
                _scrcpyService.Stop();
                ClearSession();
                _virtualDisplayService.Reset();
                _adbService.Shell(
                    "settings put global stay_on_while_plugged_in 0");
                Thread.Sleep(500);
                _logService.Info(LocalizationService.Get(
                    "Log.Dex.ShutdownCleanupCompleted"));
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Dex.ShutdownCleanupFailed"),
                    ex);
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

        private void TrackSession(string mode, int displayId)
        {
            _currentSession = new ManagedDisplaySession
            {
                Mode = mode,
                AppPackage = _settings.Scrcpy.StartAppPackage,
                DisplayId = displayId,
                ScrcpyProcessId = _scrcpyService.CurrentProcessId,
                CreatedAtUtc = DateTime.UtcNow.ToString("o")
            };
            _logService.Info(LocalizationService.Format(
                "Log.Dex.SessionStarted",
                _currentSession));
        }

        private void ClearSession()
        {
            if (_currentSession == null) return;

            _logService.Info(LocalizationService.Format(
                "Log.Dex.SessionEnded",
                _currentSession));
            _currentSession = null;
        }
    }
}

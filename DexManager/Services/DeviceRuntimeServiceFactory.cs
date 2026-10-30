using System;
using System.Collections.Generic;
using DexManager.Models;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed class DeviceRuntimeServiceFactory
    {
        private readonly string _scrcpyPath;
        private readonly string _adbPath;
        private readonly int _processTimeoutMs;
        private readonly ProcessRunner _processRunner;
        private readonly AdbService _adbService;
        private readonly ScrcpyLaunchCoordinator _launchCoordinator;
        private readonly SettingsService _settingsService;
        private readonly AppSettings _settings;
        private readonly LogService _logService;
        private readonly DeviceRuntimeSessionRegistry _runtimeSessions;
        private readonly object _sync = new object();
        private readonly Dictionary<Guid, DeviceRuntimeServiceSet> _created =
            new Dictionary<Guid, DeviceRuntimeServiceSet>();

        public DeviceRuntimeServiceFactory(
            string scrcpyPath,
            string adbPath,
            int processTimeoutMs,
            ProcessRunner processRunner,
            AdbService adbService,
            ScrcpyLaunchCoordinator launchCoordinator,
            SettingsService settingsService,
            AppSettings settings,
            LogService logService,
            DeviceRuntimeSessionRegistry runtimeSessions)
        {
            _scrcpyPath = scrcpyPath;
            _adbPath = adbPath;
            _processTimeoutMs = processTimeoutMs;
            _processRunner = processRunner ??
                throw new ArgumentNullException("processRunner");
            _adbService = adbService ??
                throw new ArgumentNullException("adbService");
            _launchCoordinator = launchCoordinator ??
                throw new ArgumentNullException("launchCoordinator");
            _settingsService = settingsService ??
                throw new ArgumentNullException("settingsService");
            _settings = settings ?? throw new ArgumentNullException("settings");
            _logService = logService ??
                throw new ArgumentNullException("logService");
            _runtimeSessions = runtimeSessions ??
                throw new ArgumentNullException("runtimeSessions");
        }

        public DeviceRuntimeServiceSet Create()
        {
            var fileTransfers = new FileTransferCoordinator(
                _adbPath,
                _settings,
                _logService,
                _runtimeSessions);
            var phoneTransfers = new PhoneTransferReceiver(
                _adbService,
                _settingsService,
                _settings,
                _logService,
                _runtimeSessions);
            var companionGuardian = new CompanionGuardianService(
                _adbService,
                _logService);
            var scrcpy = new ScrcpyService(
                _scrcpyPath,
                _processTimeoutMs,
                _processRunner,
                _adbService,
                _launchCoordinator,
                fileTransfers,
                _logService);
            var singleWindows = new SingleWindowService(
                _scrcpyPath,
                _processTimeoutMs,
                _adbService,
                _launchCoordinator,
                scrcpy.RuntimeInfo,
                fileTransfers,
                _logService,
                _runtimeSessions);
            var screenOff = new ScreenOffService(
                _scrcpyPath,
                _processTimeoutMs,
                _adbService,
                _launchCoordinator,
                scrcpy.RuntimeInfo,
                _logService);
            var virtualDisplay = new VirtualDisplayService(
                _adbService,
                _logService);
            var dex = new DexOrchestrator(
                _adbService,
                virtualDisplay,
                scrcpy,
                _launchCoordinator,
                _settingsService,
                _logService,
                _settings,
                _runtimeSessions);
            Func<bool> canTurnScreenOff = () => !dex.RequiresPhoneScreenOn &&
                !scrcpy.RequiresPhoneScreenOn && !singleWindows.RequiresPhoneScreenOn;
            scrcpy.CanTurnPhoneScreenOff = canTurnScreenOff;
            singleWindows.CanTurnPhoneScreenOff = canTurnScreenOff;
            screenOff.CanTurnPhoneScreenOff = canTurnScreenOff;
            var services = new DeviceRuntimeServiceSet(
                fileTransfers,
                phoneTransfers,
                companionGuardian,
                scrcpy,
                singleWindows,
                screenOff,
                virtualDisplay,
                dex);
            lock (_sync) _created.Add(services.InstanceId, services);
            return services;
        }

        public DeviceRuntimeServiceSet Find(Guid instanceId)
        {
            if (instanceId == Guid.Empty) return null;
            lock (_sync)
            {
                DeviceRuntimeServiceSet services;
                return _created.TryGetValue(instanceId, out services)
                    ? services
                    : null;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using DexManager.Models;

namespace DexManager.Services
{
    public sealed class DeviceMonitorService : IDisposable
    {
        private readonly AdbService _adbService;
        private readonly WirelessAdbService _wirelessAdbService;
        private readonly LogService _logService;
        private readonly int _intervalMs;
        private readonly int _disconnectConfirmationMs;
        private readonly object _stateLock = new object();
        private readonly object _lifecycleLock = new object();
        private readonly Dictionary<string, string> _deviceNameCache =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
        private Timer _timer;
        private int _polling;
        private int _stopping = 1;
        private int _disposed;
        private DateTime _missingSinceUtc = DateTime.MinValue;
        private DeviceState _currentState = DeviceState.Disconnected();

        public DeviceMonitorService(
            AdbService adbService,
            WirelessAdbService wirelessAdbService,
            LogService logService,
            int intervalMs,
            int disconnectConfirmationMs)
        {
            _adbService = adbService;
            _wirelessAdbService = wirelessAdbService;
            _logService = logService;
            _intervalMs = Math.Max(intervalMs, 500);
            _disconnectConfirmationMs = Math.Max(
                disconnectConfirmationMs,
                _intervalMs);
        }

        public event EventHandler<DeviceStateChangedEventArgs> StateChanged;
        public event EventHandler<DeviceStateChangedEventArgs> DeviceConnected;
        public event EventHandler<DeviceStateChangedEventArgs> DeviceDisconnected;

        public DeviceState CurrentState
        {
            get
            {
                lock (_stateLock)
                {
                    return CopyState(_currentState);
                }
            }
        }

        public void Start()
        {
            lock (_lifecycleLock)
            {
                if (Interlocked.CompareExchange(ref _disposed, 0, 0) != 0)
                    throw new ObjectDisposedException(
                        "DeviceMonitorService");
                if (_timer != null) return;
                Interlocked.Exchange(ref _stopping, 0);
                _missingSinceUtc = DateTime.MinValue;
                _timer = new Timer(Poll, null, 0, _intervalMs);
                _logService.Info(LocalizationService.Get(
                    "Log.DeviceMonitor.Started"));
            }
        }

        public void Stop()
        {
            lock (_lifecycleLock)
            {
                StopTimer();
            }
        }

        public void Dispose()
        {
            lock (_lifecycleLock)
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                StopTimer();
            }
        }

        private void StopTimer()
        {
            Interlocked.Exchange(ref _stopping, 1);
            var timer = Interlocked.Exchange(ref _timer, null);
            if (timer == null) return;

            using (var completed = new ManualResetEvent(false))
            {
                if (timer.Dispose(completed)) completed.WaitOne();
            }
            _logService.Info(LocalizationService.Get(
                "Log.DeviceMonitor.Stopped"));
        }

        private void Poll(object state)
        {
            if (IsStopping ||
                Interlocked.Exchange(ref _polling, 1) == 1) return;

            try
            {
                IList<AdbDeviceInfo> devices;
                if (!_adbService.TryGetDevices(false, out devices) ||
                    IsStopping)
                {
                    return;
                }

                var preferred = _wirelessAdbService.FindPreferredDevice(
                    devices,
                    _adbService.TargetSerial);
                if (preferred == null &&
                    _wirelessAdbService.TryReconnect(false))
                {
                    if (!_adbService.TryGetDevices(false, out devices) ||
                        IsStopping)
                    {
                        return;
                    }
                    preferred = _wirelessAdbService.FindPreferredDevice(
                        devices,
                        _adbService.TargetSerial);
                }

                if (IsStopping) return;
                var selection = _wirelessAdbService
                    .SelectPreferredDeviceWithGeneration(devices);
                preferred = selection.Device;

                if (preferred == null)
                {
                    if (_missingSinceUtc == DateTime.MinValue)
                        _missingSinceUtc = DateTime.UtcNow;
                    if ((DateTime.UtcNow - _missingSinceUtc).TotalMilliseconds <
                        _disconnectConfirmationMs)
                    {
                        return;
                    }
                }
                else
                {
                    _missingSinceUtc = DateTime.MinValue;
                }

                var next = preferred == null
                    ? DeviceState.Disconnected()
                    : new DeviceState
                    {
                        IsConnected =
                            preferred.Status == AdbDeviceStatus.Device,
                        Serial = preferred.Serial,
                        DisplayName =
                            preferred.Status == AdbDeviceStatus.Device
                                ? GetDeviceDisplayName(preferred.Serial)
                                : string.Empty,
                        Status = preferred.Status
                    };

                if (!IsStopping &&
                    _wirelessAdbService.IsTransitionGenerationCurrent(
                        selection.TransitionGeneration))
                {
                    PublishIfChanged(next);
                }
            }
            catch (Exception ex)
            {
                if (!IsStopping)
                {
                    _logService.Error(
                        LocalizationService.Get(
                            "Log.DeviceMonitor.Failed"),
                        ex);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _polling, 0);
            }
        }

        private bool IsStopping
        {
            get
            {
                return Interlocked.CompareExchange(
                    ref _stopping,
                    0,
                    0) != 0;
            }
        }

        private void PublishIfChanged(DeviceState next)
        {
            if (IsStopping) return;
            DeviceState previous;
            lock (_stateLock)
            {
                if (IsStopping) return;
                previous = _currentState;
                if (StatesEqual(previous, next)) return;
                _currentState = next;
            }

            _logService.Info(LocalizationService.Format(
                "Log.DeviceMonitor.StateChanged",
                previous.Status,
                next.Status,
                string.IsNullOrWhiteSpace(next.Serial)
                    ? string.Empty
                    : " (" + next.Serial + ")"));

            var args = new DeviceStateChangedEventArgs(
                CopyState(previous),
                CopyState(next));
            Raise(StateChanged, args);

            var serialChanged = previous.IsConnected &&
                next.IsConnected &&
                !string.Equals(
                    previous.Serial,
                    next.Serial,
                    StringComparison.OrdinalIgnoreCase);
            if (serialChanged)
            {
                Raise(DeviceDisconnected, args);
                Raise(DeviceConnected, args);
            }
            else if (!previous.IsConnected && next.IsConnected)
                Raise(DeviceConnected, args);
            else if (previous.IsConnected && !next.IsConnected)
                Raise(DeviceDisconnected, args);
        }

        private void Raise(
            EventHandler<DeviceStateChangedEventArgs> handler,
            DeviceStateChangedEventArgs args)
        {
            if (!IsStopping && handler != null) handler(this, args);
        }

        private static bool StatesEqual(DeviceState left, DeviceState right)
        {
            return left.IsConnected == right.IsConnected &&
                left.Status == right.Status &&
                string.Equals(
                    left.DisplayName,
                    right.DisplayName,
                    StringComparison.CurrentCulture) &&
                string.Equals(
                    left.Serial,
                    right.Serial,
                    StringComparison.OrdinalIgnoreCase);
        }

        private string GetDeviceDisplayName(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return string.Empty;

            string cached;
            if (_deviceNameCache.TryGetValue(serial, out cached))
                return cached;

            var displayName = _adbService.GetDeviceDisplayName(serial);
            _deviceNameCache[serial] = displayName ?? string.Empty;
            return displayName ?? string.Empty;
        }

        private static DeviceState CopyState(DeviceState state)
        {
            return new DeviceState
            {
                IsConnected = state.IsConnected,
                Serial = state.Serial,
                DisplayName = state.DisplayName,
                Status = state.Status
            };
        }
    }

    public sealed class DeviceStateChangedEventArgs : EventArgs
    {
        public DeviceStateChangedEventArgs(
            DeviceState previous,
            DeviceState current)
        {
            Previous = previous;
            Current = current;
        }

        public DeviceState Previous { get; private set; }
        public DeviceState Current { get; private set; }
    }
}

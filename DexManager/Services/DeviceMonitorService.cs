using System;
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
        private readonly object _stateLock = new object();
        private Timer _timer;
        private int _polling;
        private DateTime _wirelessMissingSinceUtc = DateTime.MinValue;
        private DeviceState _currentState = DeviceState.Disconnected();

        public DeviceMonitorService(
            AdbService adbService,
            WirelessAdbService wirelessAdbService,
            LogService logService,
            int intervalMs)
        {
            _adbService = adbService;
            _wirelessAdbService = wirelessAdbService;
            _logService = logService;
            _intervalMs = Math.Max(intervalMs, 500);
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
            if (_timer != null) return;
            _logService.Info(LocalizationService.Get(
                "Log.DeviceMonitor.Started"));
            _timer = new Timer(Poll, null, 0, _intervalMs);
        }

        public void Stop()
        {
            var timer = Interlocked.Exchange(ref _timer, null);
            if (timer == null) return;
            timer.Dispose();
            _logService.Info(LocalizationService.Get(
                "Log.DeviceMonitor.Stopped"));
        }

        public void Dispose()
        {
            Stop();
        }

        private void Poll(object state)
        {
            if (Interlocked.Exchange(ref _polling, 1) == 1) return;

            try
            {
                var devices = _adbService.GetDevices(false);
                var preferred =
                    _wirelessAdbService.SelectPreferredDevice(devices);
                if (preferred == null &&
                    _wirelessAdbService.TryReconnect(false))
                {
                    devices = _adbService.GetDevices(false);
                    preferred =
                        _wirelessAdbService.SelectPreferredDevice(devices);
                }
                if (_wirelessAdbService.IsWirelessMode &&
                    preferred == null)
                {
                    if (_wirelessMissingSinceUtc == DateTime.MinValue)
                        _wirelessMissingSinceUtc = DateTime.UtcNow;
                    if ((DateTime.UtcNow -
                        _wirelessMissingSinceUtc).TotalSeconds < 3)
                    {
                        return;
                    }
                }
                else
                {
                    _wirelessMissingSinceUtc = DateTime.MinValue;
                }

                var next = preferred == null
                    ? DeviceState.Disconnected()
                    : new DeviceState
                    {
                        IsConnected = preferred.Status == AdbDeviceStatus.Device,
                        Serial = preferred.Serial,
                        Status = preferred.Status
                    };

                PublishIfChanged(next);
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.DeviceMonitor.Failed"),
                    ex);
                PublishIfChanged(DeviceState.Disconnected());
            }
            finally
            {
                Interlocked.Exchange(ref _polling, 0);
            }
        }

        private void PublishIfChanged(DeviceState next)
        {
            DeviceState previous;
            lock (_stateLock)
            {
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

            if (!previous.IsConnected && next.IsConnected)
                Raise(DeviceConnected, args);
            else if (previous.IsConnected && !next.IsConnected)
                Raise(DeviceDisconnected, args);
        }

        private void Raise(
            EventHandler<DeviceStateChangedEventArgs> handler,
            DeviceStateChangedEventArgs args)
        {
            if (handler != null) handler(this, args);
        }

        private static bool StatesEqual(DeviceState left, DeviceState right)
        {
            return left.IsConnected == right.IsConnected &&
                left.Status == right.Status &&
                string.Equals(left.Serial, right.Serial, StringComparison.OrdinalIgnoreCase);
        }

        private static DeviceState CopyState(DeviceState state)
        {
            return new DeviceState
            {
                IsConnected = state.IsConnected,
                Serial = state.Serial,
                Status = state.Status
            };
        }
    }

    public sealed class DeviceStateChangedEventArgs : EventArgs
    {
        public DeviceStateChangedEventArgs(DeviceState previous, DeviceState current)
        {
            Previous = previous;
            Current = current;
        }

        public DeviceState Previous { get; private set; }
        public DeviceState Current { get; private set; }
    }
}

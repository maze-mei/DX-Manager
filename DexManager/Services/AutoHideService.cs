using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed class AutoHideService : IDisposable
    {
        private readonly ScrcpyService _scrcpyService;
        private readonly SingleWindowService _singleWindowService;
        private readonly LogService _logService;
        private readonly int _idleSeconds;
        private readonly Timer _timer;
        private bool _minimizedByService;
        private bool _hideRequested;

        public AutoHideService(
            ScrcpyService scrcpyService,
            SingleWindowService singleWindowService,
            LogService logService,
            int idleSeconds)
        {
            _scrcpyService = scrcpyService;
            _singleWindowService = singleWindowService;
            _logService = logService;
            _idleSeconds = Math.Max(idleSeconds, 1);
            _timer = new Timer { Interval = 1000 };
            _timer.Tick += Timer_Tick;
        }

        public event EventHandler IdleHideRequested;

        public void Start()
        {
            _timer.Start();
            _logService.Info(LocalizationService.Format(
                "Log.AutoHide.Started",
                _idleSeconds));
        }

        public void Stop()
        {
            _timer.Stop();
            _minimizedByService = false;
            _hideRequested = false;
        }

        public void ResetIdleHideState()
        {
            _minimizedByService = false;
            _hideRequested = false;
        }

        public void Dispose()
        {
            Stop();
            _timer.Dispose();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            var handles = GetScrcpyWindowHandles();
            if (handles.Count == 0)
            {
                _minimizedByService = false;
                return;
            }

            var idleMilliseconds = GetIdleMilliseconds();
            if (idleMilliseconds >= _idleSeconds * 1000L)
            {
                var minimized = false;
                foreach (var handle in handles)
                {
                    if (NativeMethods.IsIconic(handle)) continue;
                    NativeMethods.ShowWindow(handle, NativeMethods.SwMinimize);
                    minimized = true;
                }
                if (minimized)
                {
                    _minimizedByService = true;
                    _logService.Info(LocalizationService.Get(
                        "Log.AutoHide.ScrcpyMinimized"));
                }

                if (!_hideRequested)
                {
                    _hideRequested = true;
                    _logService.Info(LocalizationService.Get(
                        "Log.AutoHide.ManagerHidden"));
                    RaiseIdleHideRequested();
                }
            }
            else if (_minimizedByService && HasRestoredWindow(handles))
            {
                // The user restored the window explicitly. Never restore it automatically.
                _minimizedByService = false;
            }
        }

        private IList<IntPtr> GetScrcpyWindowHandles()
        {
            var handles = new List<IntPtr>();
            var dexHandle = _scrcpyService.MainWindowHandle;
            if (dexHandle != IntPtr.Zero) handles.Add(dexHandle);
            foreach (var handle in _singleWindowService.GetWindowHandles())
                handles.Add(handle);
            return handles;
        }

        private static bool HasRestoredWindow(IList<IntPtr> handles)
        {
            foreach (var handle in handles)
            {
                if (!NativeMethods.IsIconic(handle)) return true;
            }
            return false;
        }

        private void RaiseIdleHideRequested()
        {
            var handler = IdleHideRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private static long GetIdleMilliseconds()
        {
            var info = new LastInputInfo
            {
                Size = (uint)Marshal.SizeOf(typeof(LastInputInfo))
            };

            if (!NativeMethods.GetLastInputInfo(ref info)) return 0;
            return unchecked((uint)Environment.TickCount - info.Time);
        }
    }
}

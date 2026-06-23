using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed class AutoHideService : IDisposable
    {
        private readonly ScrcpyService _scrcpyService;
        private readonly LogService _logService;
        private readonly int _idleSeconds;
        private readonly Timer _timer;
        private bool _minimizedByService;
        private bool _hideRequested;

        public AutoHideService(
            ScrcpyService scrcpyService,
            LogService logService,
            int idleSeconds)
        {
            _scrcpyService = scrcpyService;
            _logService = logService;
            _idleSeconds = Math.Max(idleSeconds, 1);
            _timer = new Timer { Interval = 1000 };
            _timer.Tick += Timer_Tick;
        }

        public event EventHandler IdleHideRequested;

        public void Start()
        {
            _timer.Start();
            _logService.Info(
                "자동 숨김을 시작했습니다: " + _idleSeconds + "초");
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
            var handle = _scrcpyService.MainWindowHandle;
            if (handle == IntPtr.Zero)
            {
                _minimizedByService = false;
                return;
            }

            var idleMilliseconds = GetIdleMilliseconds();
            if (idleMilliseconds >= _idleSeconds * 1000L)
            {
                if (!NativeMethods.IsIconic(handle))
                {
                    NativeMethods.ShowWindow(handle, NativeMethods.SwMinimize);
                    _minimizedByService = true;
                    _logService.Info("사용자 입력이 없어 Scrcpy 창을 최소화했습니다.");
                }

                if (!_hideRequested)
                {
                    _hideRequested = true;
                    _logService.Info("사용자 입력이 없어 DEX Manager UI를 트레이로 숨깁니다.");
                    RaiseIdleHideRequested();
                }
            }
            else if (_minimizedByService && !NativeMethods.IsIconic(handle))
            {
                // The user restored the window explicitly. Never restore it automatically.
                _minimizedByService = false;
            }
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

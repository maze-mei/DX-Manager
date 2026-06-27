using System;
using System.Drawing;
using System.Media;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Forms;
using DexManager.Models;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed class CaptureCoordinator : IDisposable
    {
        private readonly HotkeyService _hotkeyService;
        private readonly CaptureService _captureService;
        private readonly ScrcpyService _scrcpyService;
        private readonly SingleWindowService _singleWindowService;
        private readonly AppSettings _settings;
        private readonly LogService _logService;
        private readonly Timer _pollTimer;
        private readonly Timer _timeoutTimer;
        private readonly CaptureHintOverlayForm _hintOverlay;
        private bool _waitingForCaptureChoice;
        private bool _leftButtonWasDown;
        private Point _mouseDownPoint;
        private bool _mouseDragCandidate;
        private IntPtr _captureWindowHandle;

        public CaptureCoordinator(
            HotkeyService hotkeyService,
            CaptureService captureService,
            ScrcpyService scrcpyService,
            SingleWindowService singleWindowService,
            AppSettings settings,
            LogService logService)
        {
            _hotkeyService = hotkeyService;
            _captureService = captureService;
            _scrcpyService = scrcpyService;
            _singleWindowService = singleWindowService;
            _settings = settings;
            _logService = logService;

            _pollTimer = new Timer { Interval = 20 };
            _pollTimer.Tick += PollTimer_Tick;
            _timeoutTimer = new Timer
            {
                Interval = Math.Max(settings.Timing.CaptureWaitSeconds, 1) * 1000
            };
            _timeoutTimer.Tick += delegate { CancelCaptureMode("시간 초과"); };
            _hotkeyService.CaptureHotkeyPressed += HotkeyService_CaptureHotkeyPressed;
            _hintOverlay = new CaptureHintOverlayForm();
        }

        public event EventHandler<CaptureCompletedEventArgs> CaptureCompleted;
        public event EventHandler<CaptureFailedEventArgs> CaptureFailed;
        public event EventHandler CaptureModeStarted;
        public event EventHandler ExitHotkeyPressed;

        public void Start()
        {
            Keys key;
            if (!Enum.TryParse(_settings.KeyMappings.CaptureHotkey, true, out key))
                key = Keys.F8;
            _hotkeyService.RegisterCaptureHotkey(key);
            _hotkeyService.ExitHotkeyPressed += HotkeyService_ExitHotkeyPressed;
        }

        public void Dispose()
        {
            _hintOverlay.Dispose();
            _hotkeyService.ExitHotkeyPressed -= HotkeyService_ExitHotkeyPressed;
            _pollTimer.Dispose();
            _timeoutTimer.Dispose();
            _hotkeyService.Dispose();
        }

        private void HotkeyService_CaptureHotkeyPressed(object sender, EventArgs e)
        {
            if (!_waitingForCaptureChoice)
            {
                BeginCaptureMode();
                return;
            }

            EndCaptureMode();
            RunCaptureAsync(delegate
            {
                return _captureService.CaptureWindow(
                    _captureWindowHandle);
            });
        }

        private void HotkeyService_ExitHotkeyPressed(object sender, EventArgs e)
        {
            var handler = ExitHotkeyPressed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void BeginCaptureMode()
        {
            BringScrcpyToFront();
            ReleaseScrcpyInputCapture();
            _waitingForCaptureChoice = true;
            _leftButtonWasDown = false;
            _mouseDragCandidate = false;
            _pollTimer.Start();
            _timeoutTimer.Start();
            _hintOverlay.ShowHint();
            _logService.Info("캡처 대기: F8 재입력 또는 마우스 드래그");

            var handler = CaptureModeStarted;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private static void ReleaseScrcpyInputCapture()
        {
            NativeMethods.keybd_event(
                NativeMethods.VkLMenu,
                NativeMethods.LeftAltScanCode,
                NativeMethods.KeyeventfScancode,
                UIntPtr.Zero);
            NativeMethods.keybd_event(
                NativeMethods.VkLMenu,
                NativeMethods.LeftAltScanCode,
                NativeMethods.KeyeventfScancode | NativeMethods.KeyeventfKeyup,
                UIntPtr.Zero);
            NativeMethods.ReleaseCapture();
            NativeMethods.ClipCursor(IntPtr.Zero);
        }

        private void BringScrcpyToFront()
        {
            var handle = GetPreferredScrcpyWindow();
            if (handle == IntPtr.Zero)
            {
                _logService.Warning("Scrcpy 창을 찾지 못해 캡처 모드를 시작할 수 없습니다.");
                return;
            }

            _captureWindowHandle = handle;
            if (NativeMethods.IsIconic(handle))
                NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
            NativeMethods.SetForegroundWindow(handle);
        }

        private IntPtr GetPreferredScrcpyWindow()
        {
            var foreground = NativeMethods.GetForegroundWindow();
            var dexHandle = _scrcpyService.MainWindowHandle;
            if (foreground != IntPtr.Zero &&
                (foreground == dexHandle ||
                    _singleWindowService.ContainsWindowHandle(foreground)))
            {
                return foreground;
            }

            if (dexHandle != IntPtr.Zero) return dexHandle;
            foreach (var handle in _singleWindowService.GetWindowHandles())
                return handle;
            return IntPtr.Zero;
        }

        private void PollTimer_Tick(object sender, EventArgs e)
        {
            if ((NativeMethods.GetAsyncKeyState(NativeMethods.VkEscape) & 0x8000) != 0)
            {
                CancelCaptureMode("ESC");
                return;
            }

            var leftDown =
                (NativeMethods.GetAsyncKeyState(NativeMethods.VkLButton) & 0x8000) != 0;
            if (leftDown && !_leftButtonWasDown)
            {
                _mouseDownPoint = Cursor.Position;
                _mouseDragCandidate = true;
            }

            if (leftDown && _mouseDragCandidate)
            {
                var current = Cursor.Position;
                if (Math.Abs(current.X - _mouseDownPoint.X) >= 8 ||
                    Math.Abs(current.Y - _mouseDownPoint.Y) >= 8)
                {
                    var startPoint = _mouseDownPoint;
                    EndCaptureMode();
                    SelectRegionAndCapture(startPoint);
                    return;
                }
            }

            if (!leftDown && _leftButtonWasDown)
            {
                _mouseDragCandidate = false;
            }

            _leftButtonWasDown = leftDown;
        }

        private void SelectRegionAndCapture(Point startPoint)
        {
            using (var form = new RegionSelectionForm(
                startPoint,
                _settings.Timing.CaptureWaitSeconds))
            {
                if (form.ShowDialog() != DialogResult.OK) return;
                var rectangle = form.SelectedScreenRectangle;
                RunCaptureAsync(delegate
                {
                    return _captureService.CaptureRectangle(rectangle, "Drag");
                });
            }
        }

        private void RunCaptureAsync(Func<CaptureResult> captureAction)
        {
            Task.Run(delegate
            {
                System.Threading.Thread.Sleep(100);
                return captureAction();
            }).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    var exception = task.Exception == null
                        ? new InvalidOperationException("캡처에 실패했습니다.")
                        : task.Exception.GetBaseException();
                    _logService.Error("캡처에 실패했습니다.", exception);
                    RaiseFailed(exception);
                    return;
                }

                SystemSounds.Beep.Play();
                var handler = CaptureCompleted;
                if (handler != null)
                    handler(this, new CaptureCompletedEventArgs(task.Result));
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void CancelCaptureMode(string reason)
        {
            if (!_waitingForCaptureChoice) return;
            EndCaptureMode();
            _logService.Info("캡처를 취소했습니다: " + reason);
        }

        private void EndCaptureMode()
        {
            _waitingForCaptureChoice = false;
            _pollTimer.Stop();
            _timeoutTimer.Stop();
            _hintOverlay.HideHint();
        }

        private void RaiseFailed(Exception exception)
        {
            var handler = CaptureFailed;
            if (handler != null)
                handler(this, new CaptureFailedEventArgs(exception));
        }
    }

    public sealed class CaptureCompletedEventArgs : EventArgs
    {
        public CaptureCompletedEventArgs(CaptureResult result)
        {
            Result = result;
        }

        public CaptureResult Result { get; private set; }
    }

    public sealed class CaptureFailedEventArgs : EventArgs
    {
        public CaptureFailedEventArgs(Exception exception)
        {
            Exception = exception;
        }

        public Exception Exception { get; private set; }
    }
}

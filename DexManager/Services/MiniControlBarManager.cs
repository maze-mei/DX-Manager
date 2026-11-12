using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DexManager.Forms;
using DexManager.Models;
using DexManager.Utils;

namespace DexManager.Services
{
    internal sealed class MiniControlBarManager : IDisposable
    {
        private const int FollowIntervalMs = 60;
        private const int BarGap = 6;
        private readonly AppSettings _settings;
        private readonly AdbService _powerAdb;
        private readonly ScrcpyService _scrcpyService;
        private readonly SingleWindowService _singleWindowService;
        private readonly CaptureCoordinator _captureCoordinator;
        private readonly Action _showMainWindow;
        private readonly LogService _logService;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Dictionary<string, MiniControlBarForm> _bars =
            new Dictionary<string, MiniControlBarForm>(
                StringComparer.OrdinalIgnoreCase);
        private bool _usbConnected, _wifiConnected;
        private bool _started;
        private bool _disposed;

        internal MiniControlBarManager(
            AppSettings settings,
            ScrcpyService scrcpyService,
            SingleWindowService singleWindowService,
            CaptureCoordinator captureCoordinator,
            Action showMainWindow,
            LogService logService,
            AdbService powerAdb = null)
        {
            _powerAdb=powerAdb;
            _settings = settings ?? throw new ArgumentNullException("settings");
            _scrcpyService = scrcpyService ??
                throw new ArgumentNullException("scrcpyService");
            _singleWindowService = singleWindowService ??
                throw new ArgumentNullException("singleWindowService");
            _captureCoordinator = captureCoordinator ??
                throw new ArgumentNullException("captureCoordinator");
            _showMainWindow = showMainWindow ??
                throw new ArgumentNullException("showMainWindow");
            _logService = logService ??
                throw new ArgumentNullException("logService");
            _timer = new System.Windows.Forms.Timer
            {
                Interval = FollowIntervalMs
            };
            _timer.Tick += Timer_Tick;
        }

        internal void Start()
        {
            if (_disposed || _started) return;
            _started = true;
            Synchronize();
            _timer.Start();
        }

        internal void UpdateConnection(PhysicalDeviceInfo device)
        {
            if (_disposed) return;
            var usb = device != null && device.SelectAuthorizedTransport(DeviceTransportKind.Usb, null) != null;
            var wifi = device != null && device.SelectAuthorizedTransport(DeviceTransportKind.Wireless, null) != null;
            if (usb == _usbConnected && wifi == _wifiConnected) return;
            _usbConnected = usb;
            _wifiConnected = wifi;
            foreach (var bar in _bars.Values)
                if (!bar.IsDisposed) bar.SetConnectionState(usb, wifi);
        }

        internal void ApplySettings()
        {
            if (_disposed) return;
            CloseAll();
            Synchronize();
        }

        internal bool TryGetActiveBarBounds(out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            if (_disposed) return false;

            var foreground = NativeMethods.GetForegroundWindow();
            foreach (var bar in _bars.Values)
            {
                if (!bar.IsDisposed && bar.Visible &&
                    bar.TargetHandle == foreground)
                {
                    bounds = bar.Bounds;
                    return true;
                }
            }

            foreach (var bar in _bars.Values)
            {
                if (!bar.IsDisposed && bar.Visible)
                {
                    bounds = bar.Bounds;
                    return true;
                }
            }
            return false;
        }

        internal bool TryGetBarBounds(
            IntPtr targetHandle,
            out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            if (_disposed || targetHandle == IntPtr.Zero) return false;

            foreach (var bar in _bars.Values)
            {
                if (!bar.IsDisposed &&
                    bar.TargetHandle == targetHandle &&
                    !bar.Bounds.IsEmpty)
                {
                    // Keep the intended location even while the bar is
                    // temporarily hidden because another window has focus.
                    bounds = bar.Bounds;
                    return true;
                }
            }
            return false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            _timer.Dispose();
            CloseAll();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            Synchronize();
        }

        private void Synchronize()
        {
            if (_disposed) return;
            if (!_settings.Features.MiniControlBarEnabled)
            {
                CloseAll();
                return;
            }

            var desired = GetDesiredWindows();
            RemoveMissingBars(desired);
            foreach (var item in desired)
                EnsureBar(item.Key, item.Value);

            var foreground = NativeMethods.GetForegroundWindow();
            var scrcpyForeground = false;
            foreach (var handle in desired.Values)
            {
                if (foreground == handle)
                {
                    scrcpyForeground = true;
                    break;
                }
            }

            foreach (var item in _bars)
            {
                IntPtr target;
                var canShow = desired.TryGetValue(item.Key, out target) &&
                    target == item.Value.TargetHandle &&
                    scrcpyForeground &&
                    NativeMethods.IsWindow(target) &&
                    NativeMethods.IsWindowVisible(target) &&
                    !NativeMethods.IsIconic(target);
                if (!canShow)
                {
                    if (item.Value.Visible) item.Value.Hide();
                    continue;
                }

                PositionBar(item.Value, target);
                if (!item.Value.Visible)
                    item.Value.Show(new WindowHandleOwner(target));
            }
        }

        private Dictionary<string, IntPtr> GetDesiredWindows()
        {
            var desired = new Dictionary<string, IntPtr>(
                StringComparer.OrdinalIgnoreCase);
            AddWindow(desired, "dex", _scrcpyService.MainWindowHandle);
            for (var slot = 1; slot <= 3; slot++)
            {
                AddWindow(
                    desired,
                    "single:" + slot,
                    _singleWindowService.MainWindowHandle(slot));
            }
            return desired;
        }

        private static void AddWindow(
            IDictionary<string, IntPtr> windows,
            string key,
            IntPtr handle)
        {
            if (handle != IntPtr.Zero && NativeMethods.IsWindow(handle))
                windows[key] = handle;
        }

        private void EnsureBar(string key, IntPtr target)
        {
            MiniControlBarForm existing;
            if (_bars.TryGetValue(key, out existing))
            {
                if (existing.TargetHandle == target &&
                    !existing.IsDisposed)
                {
                    return;
                }
                CloseBar(existing);
                _bars.Remove(key);
            }

            var bar = new MiniControlBarForm(
                target,
                _settings.Theme,
                _settings.KeyMappings.CaptureHotkey);
            bar.SetConnectionState(_usbConnected, _wifiConnected);
            bar.CommandRequested += Bar_CommandRequested;
            _bars[key] = bar;
        }

        private void RemoveMissingBars(
            IDictionary<string, IntPtr> desired)
        {
            var missing = new List<string>();
            foreach (var item in _bars)
            {
                IntPtr target;
                if (!desired.TryGetValue(item.Key, out target) ||
                    target != item.Value.TargetHandle)
                {
                    missing.Add(item.Key);
                }
            }
            foreach (var key in missing)
            {
                CloseBar(_bars[key]);
                _bars.Remove(key);
            }
        }

        private void PositionBar(
            MiniControlBarForm bar,
            IntPtr target)
        {
            NativeRect rect;
            if (!TryGetVisibleWindowRect(target, out rect)) return;
            var workArea = Screen.FromHandle(target).WorkingArea;
            var leftSide =
                _settings.Features.MiniControlBarSide ==
                    MiniControlBarSide.Left;
            var x = leftSide
                ? rect.Left - bar.Width - BarGap
                : rect.Right + BarGap;

            if (x < workArea.Left ||
                x + bar.Width > workArea.Right)
            {
                x = leftSide
                    ? rect.Left + BarGap
                    : rect.Right - bar.Width - BarGap;
            }
            x = Math.Max(
                workArea.Left,
                Math.Min(workArea.Right - bar.Width, x));
            var y = GetClientTop(target, rect.Top + 10);
            y = Math.Max(
                workArea.Top,
                Math.Min(workArea.Bottom - bar.Height, y));
            var location = new Point(x, y);
            if (bar.Location != location) bar.Location = location;
        }

        private static int GetClientTop(
            IntPtr target,
            int fallback)
        {
            NativeRect clientRect;
            var origin = new NativePoint();
            if (NativeMethods.GetClientRect(target, out clientRect) &&
                NativeMethods.ClientToScreen(target, ref origin))
            {
                return origin.Y;
            }
            return fallback;
        }

        private static bool TryGetVisibleWindowRect(
            IntPtr target,
            out NativeRect rect)
        {
            try
            {
                var result = NativeMethods.DwmGetWindowAttribute(
                    target,
                    NativeMethods.DwmwaExtendedFrameBounds,
                    out rect,
                    Marshal.SizeOf(typeof(NativeRect)));
                if (result == 0 &&
                    rect.Right > rect.Left &&
                    rect.Bottom > rect.Top)
                {
                    return true;
                }
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }

            return NativeMethods.GetWindowRect(target, out rect);
        }

        private void Bar_CommandRequested(
            object sender,
            MiniControlBarCommandEventArgs e)
        {
            if (_disposed || e == null ||
                !NativeMethods.IsWindow(e.TargetHandle))
            {
                return;
            }

            try
            {
                switch (e.Command)
                {
                    case MiniControlBarCommand.ScreenOff:
                        SendShortcut(e.TargetHandle, Keys.O, false);
                        break;
                    case MiniControlBarCommand.ScreenOn:
                        SendShortcut(e.TargetHandle, Keys.O, true);
                        break;
                    case MiniControlBarCommand.Power:
                        if(!PhonePowerKeyDispatcher.Queue(e.TargetHandle,_scrcpyService,_singleWindowService,_powerAdb,_logService))
                            SendShortcut(e.TargetHandle,Keys.P,false);
                        break;
                    case MiniControlBarCommand.Fullscreen:
                        SendShortcut(e.TargetHandle, Keys.F, false);
                        break;
                    case MiniControlBarCommand.ResetSize:
                        SendShortcut(e.TargetHandle, Keys.G, false);
                        break;
                    case MiniControlBarCommand.Capture:
                        _captureCoordinator.CaptureWindow(e.TargetHandle);
                        break;
                    case MiniControlBarCommand.OpenManager:
                        _showMainWindow();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.MiniBar.CommandFailed"),
                    ex);
            }
        }

        private void SendShortcut(
            IntPtr target,
            Keys key,
            bool shift)
        {
            string serial;
            int processId;
            var targetFound = _scrcpyService.TryGetWindowTarget(
                    target,
                    out serial,
                    out processId) ||
                _singleWindowService.TryGetWindowTarget(
                    target,
                    out serial,
                    out processId);
            if (!targetFound ||
                !ScrcpyShortcutSender.Send(target, processId, key, shift))
            {
                _logService.Warning(LocalizationService.Get(
                    "Log.MiniBar.CommandFailed"));
            }
        }

        private void CloseAll()
        {
            foreach (var bar in _bars.Values) CloseBar(bar);
            _bars.Clear();
        }

        private void CloseBar(MiniControlBarForm bar)
        {
            if (bar == null) return;
            bar.CommandRequested -= Bar_CommandRequested;
            try { bar.Close(); }
            catch (InvalidOperationException) { }
            bar.Dispose();
        }
    }

    internal sealed class WindowHandleOwner : IWin32Window
    {
        private readonly IntPtr _handle;

        internal WindowHandleOwner(IntPtr handle)
        {
            _handle = handle;
        }

        public IntPtr Handle
        {
            get { return _handle; }
        }
    }

    internal static class ScrcpyShortcutSender
    {
        internal static bool Send(
            IntPtr target,
            int expectedProcessId,
            Keys key,
            bool shift)
        {
            if (!WindowBelongsToProcess(target, expectedProcessId))
                return false;

            if (NativeMethods.IsIconic(target))
                NativeMethods.ShowWindow(target, NativeMethods.SwRestore);
            if (NativeMethods.GetForegroundWindow() != target &&
                !NativeMethods.SetForegroundWindow(target))
            {
                return false;
            }
            Thread.Sleep(20);
            if (NativeMethods.GetForegroundWindow() != target ||
                !WindowBelongsToProcess(target, expectedProcessId))
            {
                return false;
            }

            var inputs = new List<Input>();
            AddKey(inputs, NativeMethods.VkLMenu, false);
            if (shift) AddKey(inputs, NativeMethods.VkLShift, false);
            AddKey(inputs, (int)key, false);
            AddKey(inputs, (int)key, true);
            if (shift) AddKey(inputs, NativeMethods.VkLShift, true);
            AddKey(inputs, NativeMethods.VkLMenu, true);

            var array = inputs.ToArray();
            var sent = NativeMethods.SendInput(
                (uint)array.Length,
                array,
                Marshal.SizeOf(typeof(Input)));
            if (sent == array.Length) return true;

            NativeMethods.keybd_event(
                (byte)key,
                0,
                NativeMethods.KeyeventfKeyup,
                UIntPtr.Zero);
            if (shift)
            {
                NativeMethods.keybd_event(
                    (byte)NativeMethods.VkLShift,
                    0,
                    NativeMethods.KeyeventfKeyup,
                    UIntPtr.Zero);
            }
            NativeMethods.keybd_event(
                (byte)NativeMethods.VkLMenu,
                0,
                NativeMethods.KeyeventfKeyup,
                UIntPtr.Zero);
            return false;
        }

        private static bool WindowBelongsToProcess(
            IntPtr target,
            int expectedProcessId)
        {
            if (target == IntPtr.Zero ||
                expectedProcessId <= 0 ||
                !NativeMethods.IsWindow(target))
            {
                return false;
            }

            uint actualProcessId;
            NativeMethods.GetWindowThreadProcessId(
                target,
                out actualProcessId);
            return actualProcessId == (uint)expectedProcessId;
        }

        private static void AddKey(
            ICollection<Input> inputs,
            int virtualKey,
            bool keyUp)
        {
            inputs.Add(new Input
            {
                Type = NativeMethods.InputKeyboard,
                Data = new InputUnion
                {
                    Keyboard = new KeyboardInput
                    {
                        VirtualKey = (ushort)virtualKey,
                        ScanCode = 0,
                        Flags = keyUp
                            ? NativeMethods.KeyeventfKeyUpInput
                            : 0,
                        Time = 0,
                        ExtraInfo = UIntPtr.Zero
                    }
                }
            });
        }
    }
}

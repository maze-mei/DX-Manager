using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed class HotkeyService : NativeWindow, IDisposable
    {
        private const int CaptureHotkeyId = 1001;
        private readonly LogService _logService;
        private readonly KeyMappingSettings _settings;
        private readonly NativeMethods.LowLevelKeyboardProc _callback;
        private IntPtr _hookHandle;
        private bool _registered;
        private KeyShortcut _captureShortcut;
        private KeyShortcut _exitShortcut;
        private bool _f8Held;

        public HotkeyService(LogService logService, KeyMappingSettings settings)
        {
            _logService = logService;
            _settings = settings;
            _callback = HookCallback;
            CreateHandle(new CreateParams());
        }

        public event EventHandler CaptureHotkeyPressed;
        public event EventHandler ExitHotkeyPressed;

        public void RegisterCaptureHotkey(Keys fallbackKey)
        {
            UnregisterCaptureHotkey();
            ParseShortcuts(fallbackKey);
            TryRegisterHotKeyForDiagnostics();

            if (_settings.UseLowLevelHotkeys)
            {
                InstallHook();
                _logService.Info(
                    "저수준 키보드 후크 핫키 사용: 캡처=" +
                    _captureShortcut + ", 종료=" + _exitShortcut);
            }
        }

        public void UnregisterCaptureHotkey()
        {
            if (_registered)
            {
                NativeMethods.UnregisterHotKey(Handle, CaptureHotkeyId);
                _registered = false;
            }

            if (_hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            UnregisterCaptureHotkey();
            DestroyHandle();
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == NativeMethods.WmHotkey)
            {
                _logService.Info(
                    "WM_HOTKEY 수신: WParam=" + message.WParam +
                    ", LParam=" + message.LParam);

                if (!_settings.UseLowLevelHotkeys &&
                    message.WParam.ToInt32() == CaptureHotkeyId)
                {
                    RaiseCapture();
                }
            }

            base.WndProc(ref message);
        }

        private void ParseShortcuts(Keys fallbackKey)
        {
            _captureShortcut = KeyShortcut.Parse(
                _settings.CaptureHotkey,
                new KeyShortcut(fallbackKey));
            _exitShortcut = KeyShortcut.Parse(
                _settings.ExitHotkey,
                new KeyShortcut(Keys.F8) { LeftAlt = true });
        }

        private void TryRegisterHotKeyForDiagnostics()
        {
            uint modifiers;
            uint key;
            _captureShortcut.ToRegisterHotKey(out modifiers, out key);

            _registered = NativeMethods.RegisterHotKey(
                Handle,
                CaptureHotkeyId,
                modifiers,
                key);

            if (_registered)
            {
                _logService.Info(
                    "RegisterHotKey 등록 성공: " +
                    _captureShortcut +
                    ", Modifiers=" + modifiers +
                    ", Key=" + key);
                return;
            }

            var error = Marshal.GetLastWin32Error();
            _logService.Warning(
                "RegisterHotKey 등록 실패: " +
                _captureShortcut +
                ", LastWin32Error=" + error);

            if (!_settings.UseLowLevelHotkeys)
            {
                throw new Win32Exception(
                    error,
                    "전역 캡처 단축키를 등록하지 못했습니다: " +
                    _captureShortcut);
            }
        }

        private void InstallHook()
        {
            if (_hookHandle != IntPtr.Zero) return;

            using (var process = Process.GetCurrentProcess())
            using (var module = process.MainModule)
            {
                _hookHandle = NativeMethods.SetWindowsHookEx(
                    NativeMethods.WhKeyboardLl,
                    _callback,
                    NativeMethods.GetModuleHandle(module.ModuleName),
                    0);
            }

            if (_hookHandle == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                throw new Win32Exception(
                    error,
                    "저수준 키보드 후크를 설치하지 못했습니다.");
            }
        }

        private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code < 0)
                return NativeMethods.CallNextHookEx(
                    _hookHandle,
                    code,
                    wParam,
                    lParam);

            var data = (LowLevelKeyboardInput)Marshal.PtrToStructure(
                lParam,
                typeof(LowLevelKeyboardInput));
            var message = wParam.ToInt32();
            var keyDown = message == NativeMethods.WmKeyDown ||
                message == NativeMethods.WmSysKeyDown;
            var keyUp = message == NativeMethods.WmKeyUp ||
                message == NativeMethods.WmSysKeyUp;

            if (_settings.LogKeyboardDiagnostics && IsInteresting(data))
                LogKeyboardEvent(data, message);

            if ((data.Flags & NativeMethods.LlkhfInjected) != 0)
                return NativeMethods.CallNextHookEx(
                    _hookHandle,
                    code,
                    wParam,
                    lParam);

            if (data.VirtualKey == (uint)Keys.F8)
            {
                if (keyUp)
                {
                    _f8Held = false;
                }
                else if (keyDown && !_f8Held)
                {
                    _f8Held = true;
                    var state = ModifierState.Read();
                    if (_exitShortcut.Matches((Keys)data.VirtualKey, state))
                    {
                        RaiseExit();
                        return new IntPtr(1);
                    }

                    if (_captureShortcut.Matches((Keys)data.VirtualKey, state))
                    {
                        RaiseCapture();
                        return new IntPtr(1);
                    }
                }
            }

            return NativeMethods.CallNextHookEx(
                _hookHandle,
                code,
                wParam,
                lParam);
        }

        private static bool IsInteresting(LowLevelKeyboardInput data)
        {
            return data.VirtualKey == (uint)Keys.F8 ||
                data.VirtualKey == (uint)NativeMethods.VkLMenu ||
                data.VirtualKey == (uint)NativeMethods.VkRMenu ||
                data.VirtualKey == (uint)NativeMethods.VkLControl ||
                data.VirtualKey == (uint)NativeMethods.VkRControl ||
                data.VirtualKey == (uint)NativeMethods.VkLShift ||
                data.VirtualKey == (uint)NativeMethods.VkRShift ||
                data.VirtualKey == (uint)NativeMethods.VkLwin ||
                data.VirtualKey == (uint)NativeMethods.VkRwin;
        }

        private void LogKeyboardEvent(LowLevelKeyboardInput data, int message)
        {
            var state = ModifierState.Read();
            _logService.Info(
                "키 진단: msg=0x" + message.ToString("X") +
                ", vk=0x" + data.VirtualKey.ToString("X") +
                ", scan=0x" + data.ScanCode.ToString("X") +
                ", flags=0x" + data.Flags.ToString("X") +
                ", mods=" + state);
        }

        private void RaiseCapture()
        {
            var handler = CaptureHotkeyPressed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void RaiseExit()
        {
            var handler = ExitHotkeyPressed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private sealed class ModifierState
        {
            public bool LeftAlt;
            public bool RightAlt;
            public bool LeftCtrl;
            public bool RightCtrl;
            public bool LeftShift;
            public bool RightShift;
            public bool LeftWindows;
            public bool RightWindows;

            public static ModifierState Read()
            {
                return new ModifierState
                {
                    LeftAlt = IsDown(NativeMethods.VkLMenu),
                    RightAlt = IsDown(NativeMethods.VkRMenu),
                    LeftCtrl = IsDown(NativeMethods.VkLControl),
                    RightCtrl = IsDown(NativeMethods.VkRControl),
                    LeftShift = IsDown(NativeMethods.VkLShift),
                    RightShift = IsDown(NativeMethods.VkRShift),
                    LeftWindows = IsDown(NativeMethods.VkLwin),
                    RightWindows = IsDown(NativeMethods.VkRwin)
                };
            }

            public override string ToString()
            {
                return "LAlt=" + LeftAlt +
                    ",RAlt=" + RightAlt +
                    ",LCtrl=" + LeftCtrl +
                    ",RCtrl=" + RightCtrl +
                    ",LShift=" + LeftShift +
                    ",RShift=" + RightShift +
                    ",LWin=" + LeftWindows +
                    ",RWin=" + RightWindows;
            }

            private static bool IsDown(int virtualKey)
            {
                return (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
            }
        }

        private sealed class KeyShortcut
        {
            public KeyShortcut(Keys key)
            {
                Key = key;
            }

            public Keys Key { get; private set; }
            public bool LeftAlt { get; set; }
            public bool RightAlt { get; set; }
            public bool AnyAlt { get; set; }
            public bool LeftCtrl { get; set; }
            public bool RightCtrl { get; set; }
            public bool AnyCtrl { get; set; }
            public bool LeftShift { get; set; }
            public bool RightShift { get; set; }
            public bool AnyShift { get; set; }
            public bool LeftWindows { get; set; }
            public bool RightWindows { get; set; }
            public bool AnyWindows { get; set; }

            public static KeyShortcut Parse(string text, KeyShortcut fallback)
            {
                if (string.IsNullOrWhiteSpace(text)) return fallback;

                var result = new KeyShortcut(fallback.Key);
                var parts = text.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var rawPart in parts)
                {
                    var part = rawPart.Trim();
                    if (part.Equals("LeftAlt", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("LAlt", StringComparison.OrdinalIgnoreCase))
                        result.LeftAlt = true;
                    else if (part.Equals("RightAlt", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("RAlt", StringComparison.OrdinalIgnoreCase))
                        result.RightAlt = true;
                    else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                        result.AnyAlt = true;
                    else if (part.Equals("LeftCtrl", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("LCtrl", StringComparison.OrdinalIgnoreCase))
                        result.LeftCtrl = true;
                    else if (part.Equals("RightCtrl", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("RCtrl", StringComparison.OrdinalIgnoreCase))
                        result.RightCtrl = true;
                    else if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                        result.AnyCtrl = true;
                    else if (part.Equals("LeftShift", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("LShift", StringComparison.OrdinalIgnoreCase))
                        result.LeftShift = true;
                    else if (part.Equals("RightShift", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("RShift", StringComparison.OrdinalIgnoreCase))
                        result.RightShift = true;
                    else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                        result.AnyShift = true;
                    else if (part.Equals("LeftWindows", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("LeftWin", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("LWin", StringComparison.OrdinalIgnoreCase))
                        result.LeftWindows = true;
                    else if (part.Equals("RightWindows", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("RightWin", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("RWin", StringComparison.OrdinalIgnoreCase))
                        result.RightWindows = true;
                    else if (part.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("Win", StringComparison.OrdinalIgnoreCase))
                        result.AnyWindows = true;
                    else
                    {
                        Keys key;
                        if (Enum.TryParse(part, true, out key))
                            result.Key = key;
                    }
                }

                return result;
            }

            public bool Matches(Keys key, ModifierState state)
            {
                if (key != Key) return false;
                if (!MatchesSide(LeftAlt, RightAlt, AnyAlt, state.LeftAlt, state.RightAlt))
                    return false;
                if (!MatchesSide(LeftCtrl, RightCtrl, AnyCtrl, state.LeftCtrl, state.RightCtrl))
                    return false;
                if (!MatchesSide(LeftShift, RightShift, AnyShift, state.LeftShift, state.RightShift))
                    return false;
                if (!MatchesSide(
                    LeftWindows,
                    RightWindows,
                    AnyWindows,
                    state.LeftWindows,
                    state.RightWindows))
                    return false;
                return true;
            }

            public void ToRegisterHotKey(out uint modifiers, out uint key)
            {
                const uint modAlt = 0x0001;
                const uint modControl = 0x0002;
                const uint modShift = 0x0004;
                const uint modWindows = 0x0008;
                modifiers = 0;
                if (LeftAlt || RightAlt || AnyAlt) modifiers |= modAlt;
                if (LeftCtrl || RightCtrl || AnyCtrl) modifiers |= modControl;
                if (LeftShift || RightShift || AnyShift) modifiers |= modShift;
                if (LeftWindows || RightWindows || AnyWindows)
                    modifiers |= modWindows;
                key = (uint)Key;
            }

            public override string ToString()
            {
                var value = string.Empty;
                Append(ref value, LeftCtrl, "LeftCtrl");
                Append(ref value, RightCtrl, "RightCtrl");
                Append(ref value, AnyCtrl, "Ctrl");
                Append(ref value, LeftAlt, "LeftAlt");
                Append(ref value, RightAlt, "RightAlt");
                Append(ref value, AnyAlt, "Alt");
                Append(ref value, LeftShift, "LeftShift");
                Append(ref value, RightShift, "RightShift");
                Append(ref value, AnyShift, "Shift");
                Append(ref value, LeftWindows, "LeftWindows");
                Append(ref value, RightWindows, "RightWindows");
                Append(ref value, AnyWindows, "Windows");
                return value + Key;
            }

            private static bool MatchesSide(
                bool needLeft,
                bool needRight,
                bool needAny,
                bool leftDown,
                bool rightDown)
            {
                if (needLeft || needRight || needAny)
                {
                    if (needLeft && !leftDown) return false;
                    if (needRight && !rightDown) return false;
                    if (needAny && !leftDown && !rightDown) return false;
                    if (!needLeft && !needAny && leftDown) return false;
                    if (!needRight && !needAny && rightDown) return false;
                    return true;
                }

                return !leftDown && !rightDown;
            }

            private static void Append(ref string value, bool condition, string text)
            {
                if (!condition) return;
                value += text + "+";
            }
        }
    }
}

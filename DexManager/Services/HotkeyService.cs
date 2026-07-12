using System;
using System.Collections.Generic;
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
        private const int ExitHotkeyId = 1002;
        private readonly LogService _logService;
        private readonly AppSettings _appSettings;
        private readonly NativeMethods.LowLevelKeyboardProc _callback;
        private readonly HashSet<Keys> _heldKeys = new HashSet<Keys>();
        private IntPtr _hookHandle;
        private bool _captureRegistered;
        private bool _exitRegistered;
        private KeyShortcut _captureShortcut;
        private KeyShortcut _exitShortcut;
        private bool _configured;
        private bool _configuredUseLowLevel;

        public HotkeyService(LogService logService, AppSettings settings)
        {
            _logService = logService;
            _appSettings = settings;
            _callback = HookCallback;
            CreateHandle(new CreateParams());
        }

        private KeyMappingSettings Settings
        {
            get { return _appSettings.KeyMappings; }
        }

        public event EventHandler CaptureHotkeyPressed;
        public event EventHandler ExitHotkeyPressed;
        public Func<bool> CaptureContextActive { get; set; }

        public void RegisterCaptureHotkey(Keys fallbackKey)
        {
            var previousCapture = _captureShortcut;
            var previousExit = _exitShortcut;
            var previousConfigured = _configured;
            var previousUseLowLevel = _configuredUseLowLevel;
            UnregisterNativeHotkeys();
            try
            {
                ParseShortcuts(fallbackKey);
                _configuredUseLowLevel = Settings.UseLowLevelHotkeys;
                ApplyConfiguration(_configuredUseLowLevel);
                _configured = true;
            }
            catch
            {
                UnregisterNativeHotkeys();
                _captureShortcut = previousCapture;
                _exitShortcut = previousExit;
                _configuredUseLowLevel = previousUseLowLevel;
                _configured = previousConfigured;
                if (previousConfigured)
                {
                    try
                    {
                        ApplyConfiguration(previousUseLowLevel);
                    }
                    catch (Exception restoreException)
                    {
                        _configured = false;
                        _logService.Error(
                            LocalizationService.Get(
                                "Log.Hotkey.RestoreFailed"),
                            restoreException);
                    }
                }
                throw;
            }
        }

        public void UnregisterCaptureHotkey()
        {
            UnregisterNativeHotkeys();
            _configured = false;
        }

        private void UnregisterNativeHotkeys()
        {
            if (_captureRegistered)
            {
                NativeMethods.UnregisterHotKey(Handle, CaptureHotkeyId);
                _captureRegistered = false;
            }
            if (_exitRegistered)
            {
                NativeMethods.UnregisterHotKey(Handle, ExitHotkeyId);
                _exitRegistered = false;
            }

            if (_hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
            _heldKeys.Clear();
        }

        private void ApplyConfiguration(bool useLowLevel)
        {
            EnsureShortcutsDoNotConflict(useLowLevel);

            if (useLowLevel)
            {
                TryRegisterHotKeyForDiagnostics();
                InstallHook();
                _logService.Info(LocalizationService.Format(
                    "Log.Hotkey.LowLevelEnabled",
                    _captureShortcut,
                    _exitShortcut));
                return;
            }

            RegisterSystemHotkeys();
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
                _logService.Info(LocalizationService.Format(
                    "Log.Hotkey.MessageReceived",
                    message.WParam,
                    message.LParam));

                if (!_configuredUseLowLevel &&
                    message.WParam.ToInt32() == CaptureHotkeyId)
                {
                    RaiseCapture();
                }
                else if (!_configuredUseLowLevel &&
                    message.WParam.ToInt32() == ExitHotkeyId)
                {
                    RaiseExit();
                }
            }

            base.WndProc(ref message);
        }

        private void ParseShortcuts(Keys fallbackKey)
        {
            _captureShortcut = KeyShortcut.Parse(
                Settings.CaptureHotkey,
                new KeyShortcut(fallbackKey));
            _exitShortcut = KeyShortcut.Parse(
                Settings.ExitHotkey,
                new KeyShortcut(Keys.F8) { LeftAlt = true });
            EnsureShortcutsDoNotConflict(Settings.UseLowLevelHotkeys);
        }

        public static bool IsValidShortcut(string text)
        {
            KeyShortcut shortcut;
            return KeyShortcut.TryParse(text, out shortcut);
        }

        public static bool ShortcutsConflict(
            string first,
            string second,
            bool useLowLevel)
        {
            KeyShortcut firstShortcut;
            KeyShortcut secondShortcut;
            if (!KeyShortcut.TryParse(first, out firstShortcut) ||
                !KeyShortcut.TryParse(second, out secondShortcut))
            {
                return false;
            }

            return ShortcutsConflict(
                firstShortcut,
                secondShortcut,
                useLowLevel);
        }

        private void EnsureShortcutsDoNotConflict(bool useLowLevel)
        {
            if (!ShortcutsConflict(
                _captureShortcut,
                _exitShortcut,
                useLowLevel))
            {
                return;
            }

            throw new InvalidOperationException(
                LocalizationService.Get(
                    "Settings.HotkeysMustDiffer"));
        }

        private static bool ShortcutsConflict(
            KeyShortcut first,
            KeyShortcut second,
            bool useLowLevel)
        {
            if (first == null || second == null ||
                first.Key != second.Key)
            {
                return false;
            }

            if (!useLowLevel)
            {
                uint firstModifiers;
                uint firstKey;
                uint secondModifiers;
                uint secondKey;
                first.ToRegisterHotKey(
                    out firstModifiers,
                    out firstKey);
                second.ToRegisterHotKey(
                    out secondModifiers,
                    out secondKey);
                return firstModifiers == secondModifiers &&
                    firstKey == secondKey;
            }

            for (var mask = 0; mask < 256; mask++)
            {
                var state = new ModifierState
                {
                    LeftAlt = (mask & 1) != 0,
                    RightAlt = (mask & 2) != 0,
                    LeftCtrl = (mask & 4) != 0,
                    RightCtrl = (mask & 8) != 0,
                    LeftShift = (mask & 16) != 0,
                    RightShift = (mask & 32) != 0,
                    LeftWindows = (mask & 64) != 0,
                    RightWindows = (mask & 128) != 0
                };

                if (first.Matches(first.Key, state) &&
                    second.Matches(second.Key, state))
                {
                    return true;
                }
            }

            return false;
        }

        private void TryRegisterHotKeyForDiagnostics()
        {
            int error;
            if (TryRegisterShortcut(
                CaptureHotkeyId,
                _captureShortcut,
                out error))
            {
                NativeMethods.UnregisterHotKey(Handle, CaptureHotkeyId);
                _logService.Info(LocalizationService.Get(
                    "Log.Hotkey.DiagnosticRegistrationRemoved"));
            }
        }

        private void RegisterSystemHotkeys()
        {
            int error;
            if (!TryRegisterShortcut(
                CaptureHotkeyId,
                _captureShortcut,
                out error))
            {
                throw new Win32Exception(
                    error,
                    LocalizationService.Format(
                        "Error.Hotkey.GlobalCaptureRegistrationFailed",
                        _captureShortcut));
            }
            _captureRegistered = true;

            if (!TryRegisterShortcut(
                ExitHotkeyId,
                _exitShortcut,
                out error))
            {
                NativeMethods.UnregisterHotKey(Handle, CaptureHotkeyId);
                _captureRegistered = false;
                throw new Win32Exception(
                    error,
                    LocalizationService.Format(
                        "Error.Hotkey.GlobalExitRegistrationFailed",
                        _exitShortcut));
            }
            _exitRegistered = true;
        }

        private bool TryRegisterShortcut(
            int id,
            KeyShortcut shortcut,
            out int error)
        {
            uint modifiers;
            uint key;
            shortcut.ToRegisterHotKey(out modifiers, out key);
            if (NativeMethods.RegisterHotKey(
                Handle,
                id,
                modifiers,
                key))
            {
                error = 0;
                _logService.Info(LocalizationService.Format(
                    "Log.Hotkey.Registered",
                    shortcut,
                    modifiers,
                    key));
                return true;
            }

            error = Marshal.GetLastWin32Error();
            _logService.Warning(LocalizationService.Format(
                "Log.Hotkey.RegistrationFailed",
                shortcut,
                error));
            return false;
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
                    LocalizationService.Get(
                        "Error.Hotkey.LowLevelHookFailed"));
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

            if (Settings.LogKeyboardDiagnostics && IsInteresting(data))
                LogKeyboardEvent(data, message);

            if ((data.Flags & NativeMethods.LlkhfInjected) != 0)
                return NativeMethods.CallNextHookEx(
                    _hookHandle,
                    code,
                    wParam,
                    lParam);

            var key = (Keys)data.VirtualKey;
            var isConfiguredKey = key == _captureShortcut.Key ||
                key == _exitShortcut.Key;
            if (isConfiguredKey)
            {
                if (keyUp)
                {
                    _heldKeys.Remove(key);
                }
                else if (keyDown && _heldKeys.Add(key))
                {
                    var state = ModifierState.Read();
                    if (_exitShortcut.Matches(key, state))
                    {
                        RaiseExit();
                        return new IntPtr(1);
                    }

                    if (_captureShortcut.Matches(key, state))
                    {
                        var contextActive = CaptureContextActive == null ||
                            CaptureContextActive();
                        if (contextActive)
                        {
                            RaiseCapture();
                            return new IntPtr(1);
                        }
                    }
                }
            }

            return NativeMethods.CallNextHookEx(
                _hookHandle,
                code,
                wParam,
                lParam);
        }

        private bool IsInteresting(LowLevelKeyboardInput data)
        {
            return data.VirtualKey == (uint)_captureShortcut.Key ||
                data.VirtualKey == (uint)_exitShortcut.Key ||
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
            _logService.Info(LocalizationService.Format(
                "Log.Hotkey.Diagnostics",
                message.ToString("X"),
                data.VirtualKey.ToString("X"),
                data.ScanCode.ToString("X"),
                data.Flags.ToString("X"),
                state));
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
                KeyShortcut result;
                return TryParse(text, out result) ? result : fallback;
            }

            public static bool TryParse(
                string text,
                out KeyShortcut result)
            {
                result = null;
                if (string.IsNullOrWhiteSpace(text)) return false;

                var parsed = new KeyShortcut(Keys.None);
                var keyFound = false;
                var parts = text.Split(
                    new[] { '+' },
                    StringSplitOptions.None);
                foreach (var rawPart in parts)
                {
                    var part = rawPart.Trim();
                    if (part.Length == 0) return false;
                    if (part.Equals("LeftAlt", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("LAlt", StringComparison.OrdinalIgnoreCase))
                        parsed.LeftAlt = true;
                    else if (part.Equals("RightAlt", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("RAlt", StringComparison.OrdinalIgnoreCase))
                        parsed.RightAlt = true;
                    else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                        parsed.AnyAlt = true;
                    else if (part.Equals("LeftCtrl", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("LCtrl", StringComparison.OrdinalIgnoreCase))
                        parsed.LeftCtrl = true;
                    else if (part.Equals("RightCtrl", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("RCtrl", StringComparison.OrdinalIgnoreCase))
                        parsed.RightCtrl = true;
                    else if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                        parsed.AnyCtrl = true;
                    else if (part.Equals("LeftShift", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("LShift", StringComparison.OrdinalIgnoreCase))
                        parsed.LeftShift = true;
                    else if (part.Equals("RightShift", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("RShift", StringComparison.OrdinalIgnoreCase))
                        parsed.RightShift = true;
                    else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                        parsed.AnyShift = true;
                    else if (part.Equals("LeftWindows", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("LeftWin", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("LWin", StringComparison.OrdinalIgnoreCase))
                        parsed.LeftWindows = true;
                    else if (part.Equals("RightWindows", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("RightWin", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("RWin", StringComparison.OrdinalIgnoreCase))
                        parsed.RightWindows = true;
                    else if (part.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
                        part.Equals("Win", StringComparison.OrdinalIgnoreCase))
                        parsed.AnyWindows = true;
                    else
                    {
                        Keys key;
                        int numericKey;
                        if (part.IndexOf(',') >= 0 ||
                            int.TryParse(part, out numericKey) ||
                            !Enum.TryParse(part, true, out key) ||
                            keyFound ||
                            !IsValidMainKey(key))
                        {
                            return false;
                        }
                        parsed.Key = key;
                        keyFound = true;
                    }
                }

                if (!keyFound) return false;
                result = parsed;
                return true;
            }

            private static bool IsValidMainKey(Keys key)
            {
                if (key == Keys.None ||
                    !Enum.IsDefined(typeof(Keys), key) ||
                    (key & Keys.Modifiers) != Keys.None)
                {
                    return false;
                }

                switch (key)
                {
                    case Keys.ShiftKey:
                    case Keys.LShiftKey:
                    case Keys.RShiftKey:
                    case Keys.ControlKey:
                    case Keys.LControlKey:
                    case Keys.RControlKey:
                    case Keys.Menu:
                    case Keys.LMenu:
                    case Keys.RMenu:
                    case Keys.LWin:
                    case Keys.RWin:
                    case Keys.LButton:
                    case Keys.RButton:
                    case Keys.MButton:
                    case Keys.XButton1:
                    case Keys.XButton2:
                    case Keys.KeyCode:
                        return false;
                    default:
                        return true;
                }
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

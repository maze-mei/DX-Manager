using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using DexManager.Models;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed class KeyMappingService : IDisposable
    {
        private readonly ScrcpyService _scrcpyService;
        private readonly SingleWindowService _singleWindowService;
        private readonly AdbService _adbService;
        private readonly AppSettings _appSettings;
        private readonly KeyMappingSettings _settings;
        private readonly LogService _logService;
        private readonly NativeMethods.LowLevelKeyboardProc _callback;
        private IntPtr _hookHandle;
        private bool _enterHeld;
        private volatile bool _hangulHeld;
        private bool _scrollLockHeld;
        private bool _enterShiftMode;
        private bool _rightShiftMapped;
        private bool _rightShiftMappingLogged;

        private KeyMappingSettings Settings
        {
            get { return _appSettings.KeyMappings ?? _settings; }
        }

        public KeyMappingService(
            ScrcpyService scrcpyService,
            SingleWindowService singleWindowService,
            AdbService adbService,
            AppSettings appSettings,
            KeyMappingSettings settings,
            LogService logService)
        {
            _scrcpyService = scrcpyService;
            _singleWindowService = singleWindowService;
            _adbService = adbService;
            _appSettings = appSettings;
            _settings = settings;
            _logService = logService;
            _callback = HookCallback;
        }

        public void Start()
        {
            if (_hookHandle != IntPtr.Zero) return;

            using (var process = Process.GetCurrentProcess())
            using (var module = process.MainModule)
            {
                var moduleHandle = NativeMethods.GetModuleHandle(module.ModuleName);
                _hookHandle = NativeMethods.SetWindowsHookEx(
                    NativeMethods.WhKeyboardLl,
                    _callback,
                    moduleHandle,
                    0);
            }

            if (_hookHandle == IntPtr.Zero)
                throw new Win32Exception(
                    LocalizationService.Get(
                        "Error.KeyMapping.HookFailed"));

            _logService.Info(LocalizationService.Get(
                "Log.KeyMapping.Started"));
            if (Settings.ConvertEnterToShiftEnter)
            {
                _enterShiftMode = false;
                _logService.Info(LocalizationService.Get(
                    "Log.KeyMapping.EnterConversionEnabled"));
            }
            else
            {
                _logService.Warning(LocalizationService.Get(
                    "Log.KeyMapping.EnterConversionDisabled"));
            }
        }

        public void Stop()
        {
            ReleaseRightShiftMapping();
            _enterHeld = false;
            _hangulHeld = false;
            _scrollLockHeld = false;
            if (_hookHandle == IntPtr.Zero) return;
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
            _logService.Info(LocalizationService.Get(
                "Log.KeyMapping.Stopped"));
        }

        public void Dispose()
        {
            Stop();
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
            if ((data.Flags & NativeMethods.LlkhfInjected) != 0)
                return NativeMethods.CallNextHookEx(
                    _hookHandle,
                    code,
                    wParam,
                    lParam);

            var message = wParam.ToInt32();
            var keyDown = message == NativeMethods.WmKeyDown ||
                message == NativeMethods.WmSysKeyDown;
            var keyUp = message == NativeMethods.WmKeyUp ||
                message == NativeMethods.WmSysKeyUp;

            if (Settings.LogKeyboardDiagnostics && IsHangulScanCode(data))
                LogKeyboardDiagnostic(data, message);

            if (keyUp && ReleaseOwnedKey(data)) return new IntPtr(1);

            IntPtr foreground;
            if (!IsScrcpyForeground(out foreground))
                return NativeMethods.CallNextHookEx(
                    _hookHandle,
                    code,
                    wParam,
                    lParam);

            if (Settings.LogKeyboardDiagnostics &&
                !IsHangulScanCode(data) &&
                IsKeyboardDiagnosticTarget(data))
            {
                LogKeyboardDiagnostic(data, message);
            }

            if (_scrcpyService.RuntimeInfo.RequiresRightShiftWorkaround &&
                data.VirtualKey == NativeMethods.VkRShift &&
                data.ScanCode == NativeMethods.RightShiftScanCode)
            {
                if (keyDown)
                {
                    if (!_rightShiftMapped)
                    {
                        _rightShiftMapped = true;
                        if (!SendLeftShiftCompatibility(false))
                        {
                            _rightShiftMapped = false;
                            return NativeMethods.CallNextHookEx(
                                _hookHandle,
                                code,
                                wParam,
                                lParam);
                        }

                        if (!_rightShiftMappingLogged)
                        {
                            _rightShiftMappingLogged = true;
                            _logService.Info(LocalizationService.Get(
                                "Log.KeyMapping.RightShiftCompatibility"));
                        }
                    }
                    return new IntPtr(1);
                }

            }

            if (Settings.ConvertEnterToShiftEnter &&
                data.VirtualKey == NativeMethods.VkScroll)
            {
                if (keyDown && !_scrollLockHeld)
                {
                    _scrollLockHeld = true;
                    _enterShiftMode = !_enterShiftMode;
                    _logService.Info(LocalizationService.Format(
                        "Log.KeyMapping.EnterMode",
                        _enterShiftMode
                            ? "Shift+Enter"
                            : LocalizationService.Get(
                                "KeyMapping.NormalEnter")));
                }
                return new IntPtr(1);
            }

            if (Settings.ConvertKoreanEnglishKey && IsHangulScanCode(data))
            {
                if (keyDown && !_hangulHeld)
                {
                    _hangulHeld = true;
                    var targetWindow = foreground;
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            if (!IsExpectedForeground(targetWindow)) return;
                            SendKeyCombination(
                                "Hangul Shift+Space",
                                Settings.KoreanEnglishInputMode,
                                NativeMethods.VkSpace,
                                NativeMethods.SpaceScanCode,
                                "keycombination -t 20 59 62",
                                true,
                                targetWindow);
                        }
                        finally
                        {
                            // Some Korean/English keys emit SC1F2 KeyDown
                            // without a matching KeyUp. Do not leave the
                            // repeat guard latched after the injected input.
                            _hangulHeld = false;
                            if (Settings.LogKeyboardDiagnostics)
                            {
                                _logService.Info(LocalizationService.Format(
                                    "Log.KeyMapping.HangulState",
                                    _hangulHeld));
                            }
                        }
                    });
                }
                else if (keyDown && Settings.LogKeyboardDiagnostics)
                {
                    _logService.Info(LocalizationService.Format(
                        "Log.KeyMapping.HangulSuppressed",
                        _hangulHeld));
                }
                return new IntPtr(1);
            }

            if (Settings.IgnoreShiftSpace &&
                data.VirtualKey == NativeMethods.VkSpace &&
                IsKeyDown(NativeMethods.VkShift))
            {
                return new IntPtr(1);
            }

            if (Settings.ConvertEnterToShiftEnter &&
                data.VirtualKey == NativeMethods.VkReturn &&
                _enterShiftMode)
            {
                if (keyDown && !_enterHeld)
                {
                    _enterHeld = true;
                    var targetWindow = foreground;
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        if (!IsExpectedForeground(targetWindow)) return;
                        SendKeyCombination(
                            "Shift+Enter",
                            Settings.EnterInputMode,
                            NativeMethods.VkReturn,
                            NativeMethods.EnterScanCode,
                            "keycombination -t 20 59 66",
                            false,
                            targetWindow);
                    });
                }
                return new IntPtr(1);
            }

            if (Settings.HandleRightWindowsKey &&
                data.VirtualKey == NativeMethods.VkRwin &&
                keyUp)
            {
                SendKey(NativeMethods.VkF24);
            }

            return NativeMethods.CallNextHookEx(
                _hookHandle,
                code,
                wParam,
                lParam);
        }

        private bool IsScrcpyForeground(out IntPtr foreground)
        {
            foreground = NativeMethods.GetForegroundWindow();
            var scrcpyHandle = _scrcpyService.MainWindowHandle;
            return foreground != IntPtr.Zero &&
                (foreground == scrcpyHandle ||
                    _singleWindowService.ContainsWindowHandle(foreground));
        }

        private static bool IsExpectedForeground(IntPtr expected)
        {
            return expected != IntPtr.Zero &&
                NativeMethods.GetForegroundWindow() == expected;
        }

        private bool ReleaseOwnedKey(LowLevelKeyboardInput data)
        {
            if (data.VirtualKey == NativeMethods.VkRShift &&
                data.ScanCode == NativeMethods.RightShiftScanCode &&
                _rightShiftMapped)
            {
                ReleaseRightShiftMapping();
                return true;
            }

            if (data.VirtualKey == NativeMethods.VkReturn && _enterHeld)
            {
                _enterHeld = false;
                return true;
            }

            if (data.VirtualKey == NativeMethods.VkScroll && _scrollLockHeld)
            {
                _scrollLockHeld = false;
                return true;
            }

            if (IsHangulScanCode(data) && _hangulHeld)
            {
                _hangulHeld = false;
                return true;
            }

            return false;
        }

        private static bool IsKeyDown(int virtualKey)
        {
            return (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private static bool IsToggleOn(int virtualKey)
        {
            return (NativeMethods.GetKeyState(virtualKey) & 0x0001) != 0;
        }

        private static bool IsHangulScanCode(LowLevelKeyboardInput data)
        {
            return data.ScanCode == NativeMethods.HangulScanCode;
        }

        private static bool IsKeyboardDiagnosticTarget(LowLevelKeyboardInput data)
        {
            return data.VirtualKey == NativeMethods.VkHangul ||
                data.VirtualKey == NativeMethods.VkRMenu ||
                data.VirtualKey == NativeMethods.VkLMenu ||
                data.VirtualKey == NativeMethods.VkSpace ||
                data.VirtualKey == NativeMethods.VkScroll ||
                data.VirtualKey == NativeMethods.VkReturn ||
                data.VirtualKey == NativeMethods.VkRShift ||
                data.ScanCode == NativeMethods.HangulScanCode ||
                data.ScanCode == NativeMethods.LeftAltScanCode;
        }

        private void LogKeyboardDiagnostic(LowLevelKeyboardInput data, int message)
        {
            _logService.Info(LocalizationService.Format(
                "Log.KeyMapping.Diagnostics",
                message.ToString("X"),
                data.VirtualKey.ToString("X"),
                data.ScanCode.ToString("X"),
                data.Flags.ToString("X"),
                (data.Flags & NativeMethods.LlkhfInjected) != 0));
        }

        private void SendKeyCombination(
            string description,
            KeyInputMode mode,
            int keyVirtualKey,
            ushort keyScanCode,
            string inputCommand,
            bool writeProcessLog,
            IntPtr targetWindow)
        {
            if (!IsExpectedForeground(targetWindow)) return;

            if (mode == KeyInputMode.SendInputVirtualKey)
            {
                if (SendShiftKeyByVirtualKey(
                        description,
                        keyVirtualKey,
                        targetWindow))
                {
                    return;
                }
                _logService.Warning(description + " SendInput VK failed. ADB fallback.");
            }
            else if (mode == KeyInputMode.SendInputScanCode)
            {
                if (SendShiftKeyByScanCode(
                        description,
                        keyScanCode,
                        targetWindow))
                {
                    return;
                }
                _logService.Warning(description + " SendInput scan-code failed. ADB fallback.");
            }

            SendKeyCombinationToAndroid(
                description,
                inputCommand,
                writeProcessLog,
                targetWindow);
        }

        private void SendKeyCombinationToAndroid(
            string description,
            string inputCommand,
            bool writeProcessLog,
            IntPtr targetWindow)
        {
            try
            {
                if (!IsExpectedForeground(targetWindow)) return;

                string serial;
                int displayId;
                if (!TryResolveAdbTarget(
                        targetWindow,
                        out serial,
                        out displayId))
                {
                    _logService.Warning(LocalizationService.Format(
                        "Log.KeyMapping.AdbTargetUnavailable",
                        description));
                    return;
                }

                var displayArgument = "-d " + displayId + " ";
                var command = "input " + displayArgument + inputCommand;

                if (writeProcessLog)
                    _logService.Info(description + " ADB command: adb shell " + command);

                string currentSerial;
                int currentDisplayId;
                if (!TryResolveAdbTarget(
                        targetWindow,
                        out currentSerial,
                        out currentDisplayId) ||
                    !string.Equals(
                        serial,
                        currentSerial,
                        StringComparison.Ordinal) ||
                    displayId != currentDisplayId ||
                    !IsExpectedForeground(targetWindow))
                {
                    return;
                }

                var result = _adbService.ShellForSerial(
                    serial,
                    command,
                    writeProcessLog);

                if (!result.IsSuccess)
                {
                    _logService.Warning(
                        description + " ADB input failed: " +
                        GetCommandError(result));
                }
                else if (writeProcessLog)
                {
                    _logService.Info(
                        description + " ADB input success: ExitCode=" +
                        result.ExitCode);
                }
            }
            catch (Exception ex)
            {
                _logService.Error(description + " ADB input failed.", ex);
            }
        }

        private bool TryResolveAdbTarget(
            IntPtr targetWindow,
            out string serial,
            out int displayId)
        {
            serial = null;
            displayId = 0;

            var session = _scrcpyService.GetSessionSnapshot();
            if (session.IsRunning &&
                session.DisplayId > 0 &&
                !string.IsNullOrWhiteSpace(session.Serial) &&
                targetWindow == _scrcpyService.MainWindowHandle)
            {
                serial = session.Serial;
                displayId = session.DisplayId;
                return true;
            }

            return _singleWindowService.TryGetAdbTargetForWindow(
                targetWindow,
                out serial,
                out displayId);
        }

        private static string GetCommandError(ProcessResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.StandardError))
                return result.StandardError.Trim();
            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                return result.StandardOutput.Trim();
            return "ExitCode=" + result.ExitCode + ", Timeout=" + result.TimedOut;
        }

        private bool SendShiftKeyByVirtualKey(
            string description,
            int keyVirtualKey,
            IntPtr targetWindow)
        {
            var inputs = new[]
            {
                CreateVirtualKeyInput(NativeMethods.VkShift, false),
                CreateVirtualKeyInput(keyVirtualKey, false),
                CreateVirtualKeyInput(keyVirtualKey, true),
                CreateVirtualKeyInput(NativeMethods.VkShift, true)
            };

            var inputSize = Marshal.SizeOf(typeof(Input));
            if (!IsExpectedForeground(targetWindow)) return true;
            var sent = NativeMethods.SendInput(
                (uint)inputs.Length,
                inputs,
                inputSize);
            var error = sent == inputs.Length
                ? 0
                : Marshal.GetLastWin32Error();
            LogSendInputResult(
                description,
                "VK",
                sent,
                inputs.Length,
                error);
            if (sent != inputs.Length)
            {
                LogSendInputFailure(
                    description,
                    "VK",
                    sent,
                    inputs.Length,
                    inputSize,
                    error);
                RecoverPartialVirtualKeyInput(
                    description,
                    sent,
                    keyVirtualKey);
            }
            return sent == inputs.Length;
        }

        private bool SendShiftKeyByScanCode(
            string description,
            ushort keyScanCode,
            IntPtr targetWindow)
        {
            var inputs = new[]
            {
                CreateScanCodeInput(NativeMethods.LeftShiftScanCode, false),
                CreateScanCodeInput(keyScanCode, false),
                CreateScanCodeInput(keyScanCode, true),
                CreateScanCodeInput(NativeMethods.LeftShiftScanCode, true)
            };

            var inputSize = Marshal.SizeOf(typeof(Input));
            if (!IsExpectedForeground(targetWindow)) return true;
            var sent = NativeMethods.SendInput(
                (uint)inputs.Length,
                inputs,
                inputSize);
            var error = sent == inputs.Length
                ? 0
                : Marshal.GetLastWin32Error();
            LogSendInputResult(
                description,
                "ScanCode",
                sent,
                inputs.Length,
                error);
            if (sent != inputs.Length)
            {
                LogSendInputFailure(
                    description,
                    "ScanCode",
                    sent,
                    inputs.Length,
                    inputSize,
                    error);
                RecoverPartialScanCodeInput(
                    description,
                    sent,
                    keyScanCode);
            }
            return sent == inputs.Length;
        }

        private void RecoverPartialVirtualKeyInput(
            string description,
            uint sent,
            int keyVirtualKey)
        {
            if (sent == 0 || sent >= 4) return;
            var releases = sent == 2
                ? new[]
                {
                    CreateVirtualKeyInput(keyVirtualKey, true),
                    CreateVirtualKeyInput(NativeMethods.VkShift, true)
                }
                : new[]
                {
                    CreateVirtualKeyInput(NativeMethods.VkShift, true)
                };
            LogRecoveryResult(description, "VK", releases);
        }

        private void RecoverPartialScanCodeInput(
            string description,
            uint sent,
            ushort keyScanCode)
        {
            if (sent == 0 || sent >= 4) return;
            var releases = sent == 2
                ? new[]
                {
                    CreateScanCodeInput(keyScanCode, true),
                    CreateScanCodeInput(NativeMethods.LeftShiftScanCode, true)
                }
                : new[]
                {
                    CreateScanCodeInput(NativeMethods.LeftShiftScanCode, true)
                };
            LogRecoveryResult(description, "ScanCode", releases);
        }

        private void LogRecoveryResult(
            string description,
            string mode,
            Input[] releases)
        {
            var recovered = NativeMethods.SendInput(
                (uint)releases.Length,
                releases,
                Marshal.SizeOf(typeof(Input)));
            if (recovered != releases.Length)
            {
                _logService.Warning(
                    description + " " + mode +
                    " partial-input recovery failed: sent=" +
                    recovered + "/" + releases.Length +
                    ", Win32Error=" + Marshal.GetLastWin32Error());
            }
        }

        private bool SendLeftShiftCompatibility(bool keyUp)
        {
            var inputs = new[]
            {
                CreateScanCodeInput(
                    NativeMethods.LeftShiftScanCode,
                    keyUp)
            };
            var inputSize = Marshal.SizeOf(typeof(Input));
            var sent = NativeMethods.SendInput(
                1,
                inputs,
                inputSize);
            var error = sent == 1 ? 0 : Marshal.GetLastWin32Error();
            if (sent != 1)
            {
                LogSendInputFailure(
                    "Right Shift compatibility",
                    "ScanCode",
                    sent,
                    1,
                    inputSize,
                    error);
            }
            return sent == 1;
        }

        private void ReleaseRightShiftMapping()
        {
            if (!_rightShiftMapped) return;
            _rightShiftMapped = false;
            if (!SendLeftShiftCompatibility(true))
                KeyUp(NativeMethods.VkLShift);
        }

        private void LogSendInputFailure(
            string description,
            string mode,
            uint sent,
            int expected,
            int inputSize,
            int error)
        {
            _logService.Warning(
                description + " SendInput " + mode +
                " failed: sent=" + sent +
                "/" + expected +
                ", inputSize=" + inputSize +
                ", LastWin32Error=" + error);
        }

        private void LogSendInputResult(
            string description,
            string mode,
            uint sent,
            int expected,
            int error)
        {
            if (!Settings.LogKeyboardDiagnostics) return;
            _logService.Info(LocalizationService.Format(
                "Log.KeyMapping.SendInputResult",
                description,
                mode,
                sent,
                expected,
                error));
        }

        private static Input CreateVirtualKeyInput(int virtualKey, bool keyUp)
        {
            return new Input
            {
                Type = NativeMethods.InputKeyboard,
                Data = new InputUnion
                {
                    Keyboard = new KeyboardInput
                    {
                        VirtualKey = (ushort)virtualKey,
                        ScanCode = 0,
                        Flags = keyUp ? NativeMethods.KeyeventfKeyUpInput : 0,
                        Time = 0,
                        ExtraInfo = UIntPtr.Zero
                    }
                }
            };
        }

        private static Input CreateScanCodeInput(ushort scanCode, bool keyUp)
        {
            return new Input
            {
                Type = NativeMethods.InputKeyboard,
                Data = new InputUnion
                {
                    Keyboard = new KeyboardInput
                    {
                        VirtualKey = 0,
                        ScanCode = scanCode,
                        Flags = NativeMethods.KeyeventfScanCodeInput |
                            (keyUp ? NativeMethods.KeyeventfKeyUpInput : 0),
                        Time = 0,
                        ExtraInfo = UIntPtr.Zero
                    }
                }
            };
        }

        private static void SendKey(int virtualKey)
        {
            KeyDown(virtualKey);
            KeyUp(virtualKey);
        }

        private static void KeyDown(int virtualKey)
        {
            NativeMethods.keybd_event(
                (byte)virtualKey,
                0,
                0,
                UIntPtr.Zero);
        }

        private static void KeyUp(int virtualKey)
        {
            NativeMethods.keybd_event(
                (byte)virtualKey,
                0,
                NativeMethods.KeyeventfKeyup,
                UIntPtr.Zero);
        }
    }
}

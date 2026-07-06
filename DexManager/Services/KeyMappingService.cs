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
        private bool _hangulHeld;
        private bool _scrollLockHeld;
        private bool _enterShiftMode;

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
            if (_settings.ConvertEnterToShiftEnter)
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
            if (code < 0 || !IsScrcpyForeground())
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

            if (_settings.LogKeyboardDiagnostics &&
                IsKeyboardDiagnosticTarget(data))
            {
                LogKeyboardDiagnostic(data, message);
            }

            if (_settings.ConvertEnterToShiftEnter &&
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
                else if (keyUp)
                {
                    _scrollLockHeld = false;
                }

                return new IntPtr(1);
            }

            if (_settings.ConvertKoreanEnglishKey && IsHangulScanCode(data))
            {
                if (keyDown && !_hangulHeld)
                {
                    _hangulHeld = true;
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            SendKeyCombination(
                                "Hangul Shift+Space",
                                _settings.KoreanEnglishInputMode,
                                NativeMethods.VkSpace,
                                NativeMethods.SpaceScanCode,
                                "keycombination -t 20 59 62",
                                true);
                        }
                        finally
                        {
                            _hangulHeld = false;
                        }
                    });
                }
                else if (keyUp)
                {
                    _hangulHeld = false;
                }
                return new IntPtr(1);
            }

            if (_settings.IgnoreShiftSpace &&
                data.VirtualKey == NativeMethods.VkSpace &&
                IsKeyDown(NativeMethods.VkShift))
            {
                return new IntPtr(1);
            }

            if (_settings.ConvertEnterToShiftEnter &&
                data.VirtualKey == NativeMethods.VkReturn &&
                _enterShiftMode)
            {
                if (keyDown && !_enterHeld)
                {
                    _enterHeld = true;
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        SendKeyCombination(
                            "Shift+Enter",
                            _settings.EnterInputMode,
                            NativeMethods.VkReturn,
                            NativeMethods.EnterScanCode,
                            "keycombination -t 20 59 66",
                            false);
                    });
                }
                else if (keyUp)
                {
                    _enterHeld = false;
                }
                return new IntPtr(1);
            }

            if (_settings.HandleRightWindowsKey &&
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

        private bool IsScrcpyForeground()
        {
            var foreground = NativeMethods.GetForegroundWindow();
            var scrcpyHandle = _scrcpyService.MainWindowHandle;
            return foreground != IntPtr.Zero &&
                (foreground == scrcpyHandle ||
                    _singleWindowService.ContainsWindowHandle(foreground));
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
            bool writeProcessLog)
        {
            if (mode == KeyInputMode.SendInputVirtualKey)
            {
                if (SendShiftKeyByVirtualKey(description, keyVirtualKey)) return;
                _logService.Warning(description + " SendInput VK failed. ADB fallback.");
            }
            else if (mode == KeyInputMode.SendInputScanCode)
            {
                if (SendShiftKeyByScanCode(description, keyScanCode)) return;
                _logService.Warning(description + " SendInput scan-code failed. ADB fallback.");
            }

            SendKeyCombinationToAndroid(description, inputCommand, writeProcessLog);
        }

        private void SendKeyCombinationToAndroid(
            string description,
            string inputCommand,
            bool writeProcessLog)
        {
            try
            {
                var displayId = _appSettings.LastSuccess == null
                    ? 0
                    : _appSettings.LastSuccess.DisplayId;
                var displayArgument = displayId > 0
                    ? "-d " + displayId + " "
                    : string.Empty;
                var command = "input " + displayArgument + inputCommand;

                if (writeProcessLog)
                    _logService.Info(description + " ADB command: adb shell " + command);

                var result = _adbService.Shell(
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

        private static string GetCommandError(ProcessResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.StandardError))
                return result.StandardError.Trim();
            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                return result.StandardOutput.Trim();
            return "ExitCode=" + result.ExitCode + ", Timeout=" + result.TimedOut;
        }

        private bool SendShiftKeyByVirtualKey(string description, int keyVirtualKey)
        {
            var inputs = new[]
            {
                CreateVirtualKeyInput(NativeMethods.VkShift, false),
                CreateVirtualKeyInput(keyVirtualKey, false),
                CreateVirtualKeyInput(keyVirtualKey, true),
                CreateVirtualKeyInput(NativeMethods.VkShift, true)
            };

            var inputSize = Marshal.SizeOf(typeof(Input));
            var sent = NativeMethods.SendInput(
                (uint)inputs.Length,
                inputs,
                inputSize);
            if (sent != inputs.Length)
                LogSendInputFailure(description, "VK", sent, inputs.Length, inputSize);
            return sent == inputs.Length;
        }

        private bool SendShiftKeyByScanCode(string description, ushort keyScanCode)
        {
            var inputs = new[]
            {
                CreateScanCodeInput(NativeMethods.LeftShiftScanCode, false),
                CreateScanCodeInput(keyScanCode, false),
                CreateScanCodeInput(keyScanCode, true),
                CreateScanCodeInput(NativeMethods.LeftShiftScanCode, true)
            };

            var inputSize = Marshal.SizeOf(typeof(Input));
            var sent = NativeMethods.SendInput(
                (uint)inputs.Length,
                inputs,
                inputSize);
            if (sent != inputs.Length)
                LogSendInputFailure(description, "ScanCode", sent, inputs.Length, inputSize);
            return sent == inputs.Length;
        }

        private void LogSendInputFailure(
            string description,
            string mode,
            uint sent,
            int expected,
            int inputSize)
        {
            _logService.Warning(
                description + " SendInput " + mode +
                " failed: sent=" + sent +
                "/" + expected +
                ", inputSize=" + inputSize +
                ", LastWin32Error=" + Marshal.GetLastWin32Error());
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

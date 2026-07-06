using System;
using System.Runtime.InteropServices;

namespace DexManager.Utils
{
    internal static class NativeMethods
    {
        internal const int WmHotkey = 0x0312;
        internal const int VkEscape = 0x1B;
        internal const int VkLButton = 0x01;
        internal const int SwMinimize = 6;
        internal const int SwRestore = 9;
        internal const int WhKeyboardLl = 13;
        internal const int WmKeyDown = 0x0100;
        internal const int WmKeyUp = 0x0101;
        internal const int WmSysKeyDown = 0x0104;
        internal const int WmSysKeyUp = 0x0105;
        internal const int WmSettingChange = 0x001A;
        internal const int HwndBroadcast = 0xFFFF;
        internal const int SmtoAbortIfHung = 0x0002;
        internal const int LlkhfInjected = 0x00000010;
        internal const byte KeyeventfKeyup = 0x0002;
        internal const byte KeyeventfScancode = 0x0008;
        internal const int VkShift = 0x10;
        internal const int VkControl = 0x11;
        internal const int VkMenu = 0x12;
        internal const int VkLShift = 0xA0;
        internal const int VkRShift = 0xA1;
        internal const int VkLControl = 0xA2;
        internal const int VkRControl = 0xA3;
        internal const int VkLMenu = 0xA4;
        internal const int VkRMenu = 0xA5;
        internal const int VkSpace = 0x20;
        internal const int VkReturn = 0x0D;
        internal const int VkScroll = 0x91;
        internal const int VkLwin = 0x5B;
        internal const int VkRwin = 0x5C;
        internal const int VkF24 = 0x87;
        internal const int VkHangul = 0x15;
        internal const int HangulScanCode = 0xF2;
        internal const int LeftAltScanCode = 0x38;
        internal const ushort LeftShiftScanCode = 0x2A;
        internal const ushort SpaceScanCode = 0x39;
        internal const ushort EnterScanCode = 0x1C;
        internal const uint InputKeyboard = 1;
        internal const uint KeyeventfScanCodeInput = 0x0008;
        internal const uint KeyeventfKeyUpInput = 0x0002;

        internal delegate IntPtr LowLevelKeyboardProc(
            int code,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(
            IntPtr windowHandle,
            int id,
            uint modifiers,
            uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(IntPtr windowHandle, int id);

        [DllImport("user32.dll")]
        internal static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("user32.dll")]
        internal static extern short GetKeyState(int virtualKey);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetClientRect(IntPtr windowHandle, out NativeRect rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ClientToScreen(IntPtr windowHandle, ref NativePoint point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetLastInputInfo(ref LastInputInfo lastInputInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsIconic(IntPtr windowHandle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindow(IntPtr windowHandle, int command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr windowHandle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ClipCursor(IntPtr rect);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(
            int hookId,
            LowLevelKeyboardProc callback,
            IntPtr moduleHandle,
            uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

        [DllImport("user32.dll")]
        internal static extern IntPtr CallNextHookEx(
            IntPtr hookHandle,
            int code,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("user32.dll")]
        internal static extern void keybd_event(
            byte virtualKey,
            byte scanCode,
            uint flags,
            UIntPtr extraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint SendInput(
            uint inputCount,
            Input[] inputs,
            int inputSize);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr SendMessageTimeout(
            IntPtr windowHandle,
            uint message,
            UIntPtr wParam,
            string lParam,
            uint flags,
            uint timeout,
            out UIntPtr result);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct LowLevelKeyboardInput
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HardwareInput
    {
        public uint Message;
        public ushort ParamLow;
        public ushort ParamHigh;
    }
}

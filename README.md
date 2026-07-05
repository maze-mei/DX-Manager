<p align="center">
  <img src="DexManager/Resources/DXManager_256.png" width="112" alt="DX Manager icon">
</p>

<h1 align="center">DX Manager</h1>

<p align="center">
  Manage a Samsung DeX virtual display and up to three independent Android app windows from Windows.
</p>

<p align="center">
  Samsung DeX 가상 화면과 앱별 단일창 3개를 Windows에서 관리하는 데스크톱 도구입니다.
</p>

<p align="center">
  <a href="docs/USER_GUIDE_EN.md">English guide</a> ·
  <a href="docs/USER_GUIDE_KO.md">한국어 사용법</a> ·
  <a href="DexManager/THIRD_PARTY_NOTICES.md">Third-party notices</a>
</p>

## Overview

DX Manager is a Windows utility built around Samsung DeX, ADB, and
[scrcpy](https://github.com/Genymobile/scrcpy). It creates and tracks the
correct DeX virtual display, launches scrcpy against that display, and can
open up to three additional app-specific virtual displays.

The application does not depend on an `adb.exe` registered in the system
`PATH`. It selects and runs a bundled ADB by absolute path.

## Features

- Start and stop one Samsung DeX virtual display
- Open three independently configured single-app windows
- USB and wireless ADB connections
- Wi-Fi address detection and Android 11+ pairing
- Per-window resolution, DPI, bitrate, FPS, and app selection
- HID keyboard and mouse support
- Korean/English key correction and Enter/Shift+Enter switching
- Full scrcpy-window and selected-region capture
- Optional capture transfer to the phone
- Automatic hiding to the system tray after inactivity
- Light, dark, and Windows-following themes
- Automatic Korean/English UI selection
- Session logs and environment diagnostics
- Windows 7 SP1 compatibility through .NET Framework 4.6.2

## Requirements

- Windows 7 SP1, 8.1, 10, or 11
- .NET Framework 4.6.2 or later
- A Samsung device that supports Samsung DeX
- Android Developer options and USB debugging enabled
- A data-capable USB cable for initial authorization

Wireless ADB additionally requires the PC and phone to communicate on the
same local network. Guest Wi-Fi, AP isolation, VLAN rules, or corporate
network policies may block the connection.

## Quick Start

1. Download the release ZIP and extract the entire folder.
2. Enable Developer options and USB debugging on the phone.
3. Connect the phone over USB and approve the RSA debugging prompt.
4. Run `DXManager.exe`.
5. Wait for the connected-device status, then select **Start DeX**.

Do not copy only `DXManager.exe`. The adjacent `tools`, DLL, license, and
scrcpy server files are required.

For complete instructions, see:

- [한국어 사용 설명서](docs/USER_GUIDE_KO.md)
- [English user guide](docs/USER_GUIDE_EN.md)

## Default Shortcuts

| Shortcut | Action |
| --- | --- |
| `F8` | Enter capture mode while a scrcpy window is active |
| `F8` again | Capture the scrcpy client area |
| Mouse drag | Capture a selected region |
| `Esc` | Cancel capture mode |
| `Left Alt+F8` | Exit DX Manager |
| `Scroll Lock` | Toggle normal Enter / Shift+Enter mode when enabled |

The capture and exit shortcuts are configurable.

## Building

- Visual Studio 2019
- .NET Framework 4.6.2 targeting pack
- C# WinForms
- No external NuGet packages

Open `DexManager.sln` and build the `Release` configuration. The output is
written to `DexManager/bin/Release`. See
[DexManager/README.md](DexManager/README.md) for packaging notes.

## Project Status

Version 1.0.0 has been tested with:

- Windows 11: USB and wireless DeX, plus three simultaneous single-app windows
- Windows 7 SP1 with .NET Framework 4.6.2: core USB workflow and scrcpy 4.0

Hardware, Android versions, network policies, and Samsung firmware can affect
behavior. Use **Settings > Diagnostics** and the session log when reporting a
problem.

## Trademark and Independence

DX Manager is an independently developed utility. It is not affiliated with,
sponsored by, endorsed by, or distributed by Samsung Electronics or
Genymobile.

Samsung and Samsung DeX are trademarks of Samsung Electronics Co., Ltd.
scrcpy is an independent open-source project maintained by its respective
authors.

## License

The license for DX Manager's original source code must be selected before the
repository is made public. Bundled third-party components remain under their
own licenses. See
[THIRD_PARTY_NOTICES.md](DexManager/THIRD_PARTY_NOTICES.md).


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
  <a href="#english">English</a> ·
  <a href="#korean">한국어</a> ·
  <a href="docs/USER_GUIDE_EN.md">English guide</a> ·
  <a href="docs/USER_GUIDE_KO.md">한국어 사용 설명서</a> ·
  <a href="DexManager/THIRD_PARTY_NOTICES.md">Third-party notices</a>
</p>

<a id="english"></a>

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

---

<a id="korean"></a>

# 한국어

## 개요

DX Manager는 Samsung DeX, ADB와
[scrcpy](https://github.com/Genymobile/scrcpy)를 기반으로 동작하는
Windows 유틸리티입니다. 올바른 DeX 가상 디스플레이를 생성하고 추적한 뒤
해당 화면을 scrcpy로 실행하며, 앱별 가상 디스플레이를 최대 3개까지 추가로
열 수 있습니다.

이 프로그램은 시스템 `PATH`에 등록된 `adb.exe`에 의존하지 않습니다.
동봉된 ADB를 자동으로 선택하고 항상 절대 경로로 실행합니다.

## 주요 기능

- Samsung DeX 가상 디스플레이 1개 실행 및 중지
- 각각 독립적으로 설정할 수 있는 앱 단일창 3개
- USB 및 무선 ADB 연결
- Wi-Fi 주소 자동 감지와 Android 11 이상 무선 페어링
- 창별 해상도, DPI, 비트레이트, FPS와 시작 앱 설정
- HID 키보드 및 마우스 지원
- 한영키 보정과 Enter/Shift+Enter 전환
- scrcpy 전체 화면 및 선택 영역 캡처
- 캡처 결과의 휴대폰 전송
- 미입력 시 시스템 트레이 자동 숨김
- 라이트, 다크 및 Windows 설정 연동 테마
- Windows 언어에 따른 한국어·영어 UI 자동 선택
- 실행 세션 로그와 환경 점검
- .NET Framework 4.6.2를 통한 Windows 7 SP1 호환

## 요구 사항

- Windows 7 SP1, 8.1, 10 또는 11
- .NET Framework 4.6.2 이상
- Samsung DeX를 지원하는 Samsung 기기
- Android 개발자 옵션 및 USB 디버깅 활성화
- 최초 인증을 위한 데이터 통신 지원 USB 케이블

무선 ADB를 사용하려면 PC와 휴대폰이 같은 로컬 네트워크에서 서로 통신할
수 있어야 합니다. 게스트 Wi-Fi, AP 격리, VLAN 규칙 또는 회사 네트워크
정책으로 연결이 차단될 수 있습니다.

## 빠른 시작

1. 릴리스 ZIP을 내려받아 폴더 전체의 압축을 풉니다.
2. 휴대폰에서 개발자 옵션과 USB 디버깅을 활성화합니다.
3. 휴대폰을 USB로 연결하고 RSA 디버깅 허용 창을 승인합니다.
4. `DXManager.exe`를 실행합니다.
5. 장치 연결 상태가 표시되면 **DeX 시작**을 선택합니다.

`DXManager.exe`만 따로 복사하면 안 됩니다. 함께 제공되는 `tools` 폴더,
DLL, 라이선스 파일과 scrcpy 서버 파일이 모두 필요합니다.

자세한 사용법은 다음 문서를 참조하십시오.

- [한국어 사용 설명서](docs/USER_GUIDE_KO.md)
- [English user guide](docs/USER_GUIDE_EN.md)

## 기본 단축키

| 단축키 | 동작 |
| --- | --- |
| `F8` | scrcpy 창이 활성화된 상태에서 캡처 모드 진입 |
| `F8` 다시 누름 | scrcpy 화면 영역 캡처 |
| 마우스 드래그 | 선택 영역 캡처 |
| `Esc` | 캡처 모드 취소 |
| `왼쪽 Alt+F8` | DX Manager 종료 |
| `Scroll Lock` | 사용 설정 시 일반 Enter와 Shift+Enter 모드 전환 |

캡처 및 종료 단축키는 설정에서 변경할 수 있습니다.

## 빌드

- Visual Studio 2019
- .NET Framework 4.6.2 Targeting Pack
- C# WinForms
- 외부 NuGet 패키지 없음

`DexManager.sln`을 열고 `Release` 구성으로 빌드합니다. 결과물은
`DexManager/bin/Release`에 생성됩니다. 배포 파일 구성은
[DexManager/README.md](DexManager/README.md)를 참조하십시오.

## 프로젝트 상태

버전 1.0.0은 다음 환경에서 확인했습니다.

- Windows 11: USB 및 무선 DeX, 단일창 3개 동시 실행
- Windows 7 SP1 및 .NET Framework 4.6.2: USB 핵심 기능과 scrcpy 4.0

하드웨어, Android 버전, 네트워크 정책과 Samsung 펌웨어에 따라 동작이
달라질 수 있습니다. 문제를 제보할 때는 **설정 > 진단**과 실행 세션
로그를 활용하십시오.

## 상표 및 독립성 고지

DX Manager는 독립적으로 개발된 유틸리티입니다. Samsung Electronics 또는
Genymobile과 제휴·후원·보증 관계가 없으며 해당 회사에서 배포하는
프로그램이 아닙니다.

Samsung과 Samsung DeX는 Samsung Electronics Co., Ltd.의 상표입니다.
scrcpy는 해당 개발자들이 유지·관리하는 독립적인 오픈소스 프로젝트입니다.

## 라이선스

DX Manager 자체 소스 코드의 라이선스는 저장소 공개 전에 결정해야 합니다.
동봉된 제3자 구성요소에는 각각의 라이선스가 적용됩니다. 자세한 내용은
[THIRD_PARTY_NOTICES.md](DexManager/THIRD_PARTY_NOTICES.md)를
참조하십시오.

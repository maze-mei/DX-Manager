# DX Manager

Portable Windows package for managing Samsung DeX and up to three independent
scrcpy app windows per connected Galaxy phone. Version 2.0.1 supports multiple
physical phones simultaneously and bundles scrcpy 4.1.

## English

### Requirements

- 64-bit Windows 7 SP1, 8.1, 10, or 11
- .NET Framework 4.6.2 or later
- A Samsung device that supports Samsung DeX
- Android Developer options and USB debugging enabled
- A data-capable USB cable for the initial authorization

The currently verified phone baseline is Android 16 with One UI 8.x on a
DeX-capable Galaxy device. One UI 7.x and earlier have not been confirmed to
work reliably and may show a black DeX window.

Some banking, game, streaming, and security-sensitive apps may reject USB
debugging, block protected or DRM-controlled content from mirroring, or open
on the phone because they do not fully support secondary displays. DX Manager
does not bypass these app and Android security policies.

Windows 7 and 8.1 may require Universal CRT updates for the bundled legacy
ADB. Wireless ADB also requires the PC and phone to communicate on the same
local network.

Windows 7 SP1 does not include .NET Framework 4.6.2 by default. Install it from
the [official Microsoft download page](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net462)
if necessary; an offline installer is available there. If .NET Framework 4.7.2
or 4.8 is already installed, no downgrade or separate 4.6.2 installation is
required.

### Quick start

1. Extract the entire ZIP to a user-writable folder.
2. Enable Developer options and USB debugging on the phone.
3. Connect the phone by USB and approve the RSA debugging prompt.
4. Run `DXManager.exe`.
5. Wait until the connected phone is shown, then select **Start DeX**.

Do not copy only `DXManager.exe`. Keep the adjacent `tools`, DLL, license,
language, and documentation files together.

> [!IMPORTANT]
> Keep the phone connected and use **Stop DeX**, press `Left Alt+F8`, or
> right-click the tray icon and select **Exit**. Wait for cleanup before
> disconnecting the phone.
> If a simulated display remains, open **Developer options > Simulate secondary
> displays**, select any resolution once, open the menu again, and select
> **None**. Selecting **None** first may not clear a stale display.

The signed **DX Companion** APK is included under `tools\companion` but is never
installed automatically. Use **Settings > Diagnostics > DX Companion** to
install, update, grant permission to, or uninstall it on the currently selected
phone. DX Manager verifies the exact bundled APK hash and official signing
certificate before installation and rechecks the installed package afterward.
The companion provides recovery actions and phone-to-PC file/folder transfer.
For files, use Gallery or My Files and choose **Send to DX Manager** from the
Android Share menu. For folders, use **Send folder > Select folder** inside DX
Companion. DX Manager must be running with the target phone connected. The PC
destination can be changed under **Settings > Paths / ADB**.

The **Selected device version** card under **Settings > Diagnostics** shows the
current phone's Android, SDK, One UI, security patch and transport information.
Use **Save diagnostic report** to create a privacy-redacted text report for a
bug report.

### Multiple phones

When two or more phones are detected, choose the target in the left device
list. Every phone keeps independent DeX, Single-Window, connection, Companion,
and transfer state. USB and wireless ADB connections for the same physical
phone are merged, while the saved connection policy determines which transport
is used. DX Manager does not silently switch to the other transport.

### Default settings

- DeX and single windows: 1600 x 900, 150 DPI, 8 Mbps, 60 FPS
- Delay after device detection: 1 second
- Start minimized to tray: Off
- Start DeX automatically on connection: Off
- Automatic hiding: Off
- Ignore direct Shift+Space input: Off
- Scroll Lock Enter/Shift+Enter switching: Off
- Stay awake while a managed window is running: On
- DX Manager Unicode-compatible file transfer: On
- Phone file-transfer destination: `/sdcard/Download/`
- Mini control bars beside scrcpy windows: On, right side

### Mini control bar and app profiles

Each DeX and single-app window can have its own mini control bar for screen
off/on, power, fullscreen, 1:1 size, capture, and opening DX Manager. Hover
over an icon to see its shortcut. Configure its side under **Settings >
General**.

In single-window mode, open **App profile** to save the selected app's current
display and run settings. Selecting that app in any slot applies the profile
automatically. DeX settings are not affected.

### Drag-and-drop file transfer

Drop files or complete folders onto a DeX or single-app window. The default DX
Manager transfer preserves Korean, Japanese, and other Unicode names on
Windows 7 SP1 through 11. The default destination is `/sdcard/Download/` and can be changed
under **Settings > Paths / ADB > Programs and storage paths**. It must be below
`/sdcard/` or `/storage/emulated/0/`. The adjacent **Browse** button lists
existing folders on the connected phone, including Unicode names.

The independent, movable status window shows the active item, up to four
waiting items, file size, elapsed time, completed/failed/waiting counts, and a
cancel action. It does not show a percentage or ETA because reliable byte
progress is not available on every supported Windows/ADB combination. Existing
names are kept; DX Manager uses `name (1).ext` or `folder (1)` when necessary.
Cancel stops the active and waiting transfers for that scrcpy window and
attempts to remove their temporary data; it is disabled during final commit.

To use scrcpy's original behavior instead, turn off **Use DX Manager file
transfer (Unicode-compatible)** under **Settings > Paths / ADB > Programs and
storage paths**. The change applies to newly opened DeX and single-app windows.

### Default shortcuts

- `F8`: Enter capture mode while a scrcpy window is active
- `F8` again: Capture the scrcpy client area
- Mouse drag: Capture a selected PC screen region
- `Esc`: Cancel capture mode
- `Left Alt+F8`: Exit DX Manager
- `Scroll Lock`: Toggle Enter/Shift+Enter mode when that option is enabled

### Useful scrcpy shortcuts

These are scrcpy-window shortcuts, not Samsung DeX shortcuts.

- `Alt+F` or `F11`: Toggle fullscreen
- `Alt+G`: Resize the window to the video's 1:1 pixel size
- `Alt+P`: Press the phone's power button
- `Alt+O`: Turn the phone screen off (`O` is the letter O)
- `Alt+Shift+O`: Turn the phone screen on
- `Alt+V`: Synchronize the PC clipboard and paste
- `Ctrl+V`: Send Ctrl+V to the active Android app (app-dependent)

See the [official scrcpy 4.1 shortcut list](https://github.com/Genymobile/scrcpy/blob/v4.1/doc/shortcuts.md).
Some Android system shortcuts may do nothing or act on the phone's primary
display instead of the simulated DeX display.

### Documentation

- [English user guide](docs/USER_GUIDE_EN.md)
- [English FAQ](docs/FAQ_EN.md)
- [Korean user guide](docs/USER_GUIDE_KO.md)
- [Korean FAQ](docs/FAQ_KO.md)
- [Third-party notices](licenses/THIRD_PARTY_NOTICES.md)
- [DX Manager MIT License](LICENSE)

Project page: https://github.com/maze-mei/DX-Manager

---

## 한국어

여러 Galaxy 휴대폰의 Samsung DeX 가상 디스플레이와 휴대폰별 scrcpy
단일창을 최대 3개까지 동시에 관리하는 Windows용 포터블 프로그램입니다.
버전 2.0.1은 scrcpy 4.1을 포함합니다.

### 요구 사항

- 64비트 Windows 7 SP1, 8.1, 10 또는 11
- .NET Framework 4.6.2 이상
- Samsung DeX를 지원하는 삼성 기기
- Android 개발자 옵션과 USB 디버깅 활성화
- 최초 인증을 위한 데이터 전송 가능 USB 케이블

현재 정상 동작을 확인한 휴대폰 기준은 Android 16 / One UI 8.x의 DeX 지원
Galaxy 기기입니다. One UI 7.x 이하에서는 원활한 동작을 확인하지 못했으며
DeX 창이 검게 표시될 수 있습니다.

일부 금융·게임·스트리밍·보안 앱은 USB 디버깅 환경을 거부하거나 보호된
화면과 DRM 콘텐츠의 미러링을 차단할 수 있습니다. 다중 디스플레이를 완전히
지원하지 않는 앱은 휴대폰 화면에서 열릴 수도 있습니다. DX Manager는 이러한
앱과 Android의 보안 정책을 우회하지 않습니다.

Windows 7과 8.1에서는 동봉된 레거시 ADB를 위해 Universal CRT 업데이트가
필요할 수 있습니다. 무선 ADB는 PC와 휴대폰이 같은 로컬 네트워크에서 직접
통신할 수 있어야 합니다.

Windows 7 SP1에는 .NET Framework 4.6.2가 기본 포함되지 않습니다. 필요한
경우 [Microsoft 공식 다운로드
페이지](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net462)에서
설치하십시오. 같은 페이지에서 오프라인 설치 파일도 받을 수 있습니다.
4.7.2 또는 4.8이 이미 설치되어 있다면 4.6.2로 낮추거나 추가 설치할 필요가
없습니다.

### 빠른 시작

1. ZIP 전체를 쓰기 가능한 폴더에 압축 해제합니다.
2. 휴대폰에서 개발자 옵션과 USB 디버깅을 활성화합니다.
3. USB로 연결한 뒤 휴대폰의 RSA 디버깅 허용 창을 승인합니다.
4. `DXManager.exe`를 실행합니다.
5. 연결된 휴대폰이 표시되면 **DeX 시작**을 선택합니다.

`DXManager.exe`만 따로 복사하면 동작하지 않습니다. 같은 폴더의 `tools`, DLL,
라이선스, 언어 및 문서 파일을 함께 유지하십시오.

> [!IMPORTANT]
> 휴대폰을 연결한 상태에서 **DeX 중지**를 누르거나 `Left Alt+F8`을 누르거나,
> 트레이 아이콘을 마우스 오른쪽 버튼으로 눌러 **종료**를 선택하십시오. 정리가
> 끝난 뒤 연결을 해제하십시오. 가상 화면이 남으면 **개발자 옵션 > 보조 디스플레이
> 시뮬레이션**에서 아무 해상도나 한 번 선택하고, 메뉴를 다시 열어 **없음**을
> 선택하십시오. 처음부터 **없음**만 선택하면 남은 화면이 지워지지 않을 수
> 있습니다.

서명된 **DX Companion** APK는 `tools\companion`에 포함되지만 자동으로 설치되지
않습니다. **설정 > 진단 > DX Companion**에서 현재 선택된 휴대폰에 설치·업데이트,
권한 부여 또는 삭제할 수 있습니다. DX Manager는 설치 전 정확한 번들 APK 해시와
공식 서명을 확인하고 설치 후 패키지를 다시 검증합니다. Companion은 복구 기능과
휴대폰에서 PC로 파일·폴더 전송 기능을 제공합니다.
파일은 갤러리나 내 파일의 Android 공유 메뉴에서 **DX Manager로 보내기**를
선택하고, 폴더는 DX Companion의 **폴더 보내기 > 폴더 선택**을 사용하십시오.
DX Manager가 실행 중이고 대상 휴대폰이 연결돼 있어야 하며, PC 저장 위치는
**설정 > 경로 / ADB**에서 바꿀 수 있습니다.

**설정 > 진단**의 **선택 기기 버전** 카드에는 현재 휴대폰의 Android, SDK,
One UI, 보안 패치와 연결 방식이 표시됩니다. 문제를 제보할 때는 **진단 보고서
저장**으로 개인정보를 가린 텍스트 보고서를 만들 수 있습니다.

### 여러 휴대폰

두 대 이상의 휴대폰이 감지되면 왼쪽 기기 목록에서 대상을 선택하십시오.
각 휴대폰의 DeX·단일창·연결·Companion·전송 상태는 독립적으로 유지됩니다.
같은 물리 휴대폰의 USB와 무선 ADB 연결은 하나로 합치지만, 실제 사용
transport는 그 휴대폰에 저장한 연결 정책을 따르며 반대 방식으로 임의
전환하지 않습니다.

### 기본 설정

- DeX와 단일창: 1600 x 900, 150 DPI, 8 Mbps, 60 FPS
- 기기 인식 후 시작 대기: 1초
- 트레이로 최소화하여 시작: 꺼짐
- 기기 연결 시 DeX 자동 시작: 꺼짐
- 자동 숨김: 꺼짐
- 직접 Shift+Space 입력 무시: 꺼짐
- Scroll Lock Enter/Shift+Enter 전환: 꺼짐
- 관리 창 실행 중 잠자기 방지: 켜짐
- DX Manager 한글·Unicode 호환 파일 전송: 켜짐
- 휴대폰 파일 전송 위치: `/sdcard/Download/`
- scrcpy 창 옆 미니 컨트롤바: 켜짐, 오른쪽

### 미니 컨트롤바와 앱 프로필

각 DeX·단일창에는 휴대폰 화면 끄기·켜기, 전원, 전체 화면, 1:1 크기, 캡처와
DX Manager 열기를 제공하는 전용 미니바를 표시할 수 있습니다. 아이콘에
마우스를 올리면 단축키가 표시되며 **설정 > 기본**에서 위치를 바꿀 수 있습니다.

단일창에서 **앱 프로필**을 열면 선택 앱의 현재 화면·실행 설정을 저장할 수
있습니다. 이후 어느 슬롯에서든 같은 앱을 선택하면 프로필이 자동 적용되며,
DeX 설정에는 영향을 주지 않습니다.

### 드래그 앤 드롭 파일 전송

DeX 또는 단일창에 파일이나 폴더 전체를 놓을 수 있습니다. 기본 DX Manager
전송은 Windows 7 SP1부터 11까지 한글·일본어 등 Unicode 이름을 보존합니다. 기본 저장
위치는 `/sdcard/Download/`이며 **설정 > 경로 / ADB > 프로그램 및 저장
경로**에서 바꿀 수 있습니다. `/sdcard/` 또는 `/storage/emulated/0/` 아래
폴더만 지정할 수 있습니다. 옆의 **찾아보기** 버튼은 연결된 휴대폰의 기존
폴더와 Unicode 이름을 표시합니다.

독립적으로 이동할 수 있는 상태창에는 현재 항목, 다음 대기 항목 4개, 파일
크기, 경과 시간, 완료·실패·대기 수와 취소 기능이 표시됩니다. 지원하는 모든
Windows와 ADB 조합에서 정확한 전송 바이트를 얻을 수 없으므로 진행률과 남은
시간은 표시하지 않습니다. 같은 이름은 `이름 (1).확장자` 또는 `폴더 (1)`로
저장합니다.
취소하면 해당 scrcpy 창의 진행 중·대기 중 전송을 모두 중단하고 임시 데이터를
정리하도록 시도하며, 최종 저장 단계에서는 버튼이 비활성화됩니다.

scrcpy 순정 방식을 사용하려면 **설정 > 경로 / ADB > 프로그램 및 저장 경로**의
**DX Manager 파일 전송 사용 (한글/Unicode 호환)**을 끄십시오. 변경은 새로
여는 DeX와 단일창부터 적용됩니다.

### 기본 단축키

- `F8`: scrcpy 창 활성 상태에서 캡처 모드 진입
- `F8` 다시 누르기: scrcpy 창 영역 캡처
- 마우스 드래그: PC 화면의 선택 영역 캡처
- `Esc`: 캡처 취소
- `Left Alt+F8`: DX Manager 종료
- `Scroll Lock`: 해당 옵션 사용 시 Enter/Shift+Enter 모드 전환

### 자주 사용하는 scrcpy 단축키

다음은 Samsung DeX 자체 단축키가 아니라 scrcpy 창 단축키입니다.

- `Alt+F` 또는 `F11`: 전체 화면 전환
- `Alt+G`: 현재 영상의 1:1 픽셀 크기에 맞춰 창 크기 조정
- `Alt+P`: 휴대폰 전원 버튼 누르기
- `Alt+O`: 휴대폰 화면 끄기 (`O`는 숫자 0이 아닌 영문자 O)
- `Alt+Shift+O`: 휴대폰 화면 켜기
- `Alt+V`: PC 클립보드를 동기화하고 붙여넣기
- `Ctrl+V`: 활성 Android 앱에 Ctrl+V 전달(앱에 따라 동작)

[scrcpy 4.1 공식 단축키 목록](https://github.com/Genymobile/scrcpy/blob/v4.1/doc/shortcuts.md)도
참조하십시오. 일부 Android 시스템 단축키는 가상 DeX 화면에서 동작하지
않거나 휴대폰의 기본 화면에서 실행될 수 있습니다.

### 문서

- [English user guide](docs/USER_GUIDE_EN.md)
- [English FAQ](docs/FAQ_EN.md)
- [한국어 사용 설명서](docs/USER_GUIDE_KO.md)
- [한국어 자주 묻는 질문](docs/FAQ_KO.md)
- [제3자 고지](licenses/THIRD_PARTY_NOTICES.md)
- [DX Manager MIT 라이선스](LICENSE)

프로젝트 페이지: https://github.com/maze-mei/DX-Manager

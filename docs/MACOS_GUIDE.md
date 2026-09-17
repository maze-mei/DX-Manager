# DX Manager for macOS 사용 및 개발 가이드

<p align="center">
  <b>Samsung DeX & 다중 가상 디스플레이 매니저 for macOS (.NET 8 Native Edition)</b>
</p>

---

## 1. 개요 (Overview)

**DX Manager for macOS**는 삼성 갤럭시 스마트폰의 **Samsung DeX 가상 디스플레이** 및 **앱별 독립 가상 디스플레이(단일창)**를 macOS 환경에서 고성능 저지연으로 제어하고 관리할 수 있도록 포팅된 .NET 8 기반 크로스플랫폼 도구입니다.

기존 Windows 전용 WinForms 구현의 핵심 엔진(`DexManager.Core`)을 플랫폼 중립적인 .NET 8 아키텍처로 분리하고, macOS 환경에 최적화된 대화형 TUI 호스트(`DexManager.Mac`)와 네이티브 플랫폼 서비스(screencapture, launchd, POSIX 권한 관리 등)를 제공합니다.

---

## 2. 시스템 요구사항 (Requirements)

- **운영체제**: macOS 14 Sonoma 이상
- **아키텍처**: 
  - Apple Silicon (Apple M series) - Native ARM64
  - Intel Mac (x86_64)
- **포터블 ZIP 사용자**: Homebrew, .NET, scrcpy와 ADB를 별도로 설치하지
  않습니다. Mac 아키텍처에 맞게 미리 빌드된 ZIP에 self-contained .NET
  런타임, scrcpy 4.1, ADB와 scrcpy 서버가 포함됩니다.
- **소스 개발자**: `global.json`에 지정된 .NET 8 SDK가 필요합니다.
- **지원 스마트폰**:
  - Samsung Galaxy 기기 중 Samsung DeX를 지원하는 기기 (Galaxy S시리즈, Note시리즈, Z Fold시리즈, Tab S시리즈 등)
  - Android 16 / One UI 8.x (현재 기준 동작 검증)

---

## 3. 포터블 ZIP 실행 (Portable Release)

### 3.1 Mac에 맞는 ZIP 선택

- Apple M 시리즈 Mac: `DX-Manager-v<version>-macos-arm64.zip`
- Intel Mac: `DX-Manager-v<version>-macos-x64.zip`

여기서 `<version>`은 공개된 Release 버전(예: `2.0.1`)으로 바꿉니다. ZIP 안의
문서에는 패키징 시 실제 버전이 자동 반영됩니다.

저장소의 GitHub Actions workflow는 `macos-15` Apple Silicon 실행 환경과
`macos-15-intel` Intel 실행 환경에서 각각 전체 빌드·테스트·패키지 검증을
수행하도록 구성되어 있습니다. 버전 태그의 두 작업이 성공하면 검증된 ZIP 두 개와
SHA-256 파일을 포함한 GitHub Release 초안을 만들며, 유지관리자가 확인 후
공개합니다. 이 변경의 첫 원격 workflow 성공 여부는 아직 확인해야 합니다.
Release가 공개된 뒤 사용자는 Mac에 맞는 ZIP 하나를
내려받아 전체 폴더의 압축을 풀고 `Start DX Manager.command`를 더블클릭합니다.
소스 빌드는 필요하지 않습니다.

현재 자동 생성 패키지는 Apple Developer ID 서명·공증 전 단계이므로 최초 실행
시 macOS 승인이 필요할 수 있습니다. Control-클릭 후 **열기**를 사용하거나
**시스템 설정 > 개인정보 보호 및 보안**에서 차단된 항목을 확인하십시오. 자동으로
보안 속성을 지우는 명령은 실행하지 않습니다.

### 3.2 스마트폰 설정

1. **개발자 옵션 활성화**:
   - 휴대폰 **설정 > 휴대전화 정보 > 소프트웨어 정보**에서 **빌드번호**를 7회 연속 탭합니다.
2. **USB 디버깅 켜기**:
   - **설정 > 개발자 옵션**으로 이동하여 **USB 디버깅**을 활성화합니다.
3. **USB 케이블 연결 및 RSA 디버깅 허용**:
   - 데이터 전송이 가능한 USB-C 케이블로 Mac과 갤럭시 스마트폰을 연결합니다.
   - 스마트폰 화면에 나타나는 **"이 컴퓨터에서 항상 디버깅을 허용합니까?"** 팝업에서 **항상 허용**을 체크하고 승인합니다.

## 4. 소스 개발 및 포터블 패키징 (Development & Packaging)

이 절은 프로그램을 수정하거나 배포 ZIP을 만드는 개발자용입니다. 일반 사용자는
3절의 미리 빌드된 ZIP만 사용하면 됩니다.

### 4.1 솔루션 빌드

```bash
# Release 빌드
dotnet build DexManager.Mac.sln -c Release

# 빌드 경고를 에러로 엄격 처리하는 빌드
dotnet build DexManager.Mac.sln -c Release /warnaserror
```

### 4.2 테스트 스위트 실행

DX Manager for macOS는 xUnit 기반 단위/통합 테스트와 다중 기기 회귀 테스트를
사용합니다.

```bash
# 95개 xUnit 단위 및 통합 테스트 실행
dotnet test DexManager.Mac.sln -c Release

# 39개 다중 기기 세션 격리 회귀 테스트 실행
dotnet run --project DexManager.MultiDeviceTests -c Release
```

### 4.3 아키텍처별 포터블 ZIP 생성

```bash
# Apple Silicon용 self-contained ZIP
scripts/Package-Mac-Release.sh --rid osx-arm64

# Intel용 self-contained ZIP
scripts/Package-Mac-Release.sh --rid osx-x64
```

스크립트는 DX Manager와 ADB proxy를 지정한 RID로 미리 publish하고, 공식
scrcpy 4.1 정적 빌드의 SHA-256을 확인한 뒤 번들합니다. 생성한 ZIP을 새 임시
폴더에 다시 풀어 실행 권한, CPU 아키텍처, 외부 Homebrew 경로 의존성, 버전,
라이선스와 사용자 데이터 제외 여부를 검사합니다.

---

## 5. 실행 및 CLI 명령어 (Usage & CLI Options)

### 5.1 대화형 콘솔 대시보드 (Interactive Dashboard) 실행

포터블 ZIP 사용자는 압축을 푼 폴더에서 다음 실행기를 더블클릭합니다.

```bash
./Start\ DX\ Manager.command
```

소스 개발자는 다음 명령 중 하나로 같은 대시보드를 실행할 수 있습니다.

```bash
dotnet run --project DexManager.Mac
# 또는 빌드된 바이너리 직접 실행:
./DexManager.Mac/bin/Release/net8.0/DXManager.Mac
```

### 5.2 CLI 인자 모드 (Command-line Arguments)

자동화 스크립트나 터미널 단축 명령을 위한 CLI 인자를 제공합니다:

| 명령어 | 단축형 | 설명 |
| :--- | :--- | :--- |
| `--dex` | `-x` | 선택된 기기의 DeX 모드를 즉시 시작 (종료는 `Ctrl+C`) |
| `--stop-dex` | | 현재 실행 중인 DeX 세션을 중지하고 가상 디스플레이 오버레이 정리 |
| `--diag` | `-d` | 환경 점검 및 기기 호환성 진단 리포트를 실행하여 콘솔에 출력 |
| `--version` | `-v` | DX Manager for macOS 버전 정보 출력 |
| `--help` | `-h` | CLI 사용법 및 도움말 출력 |

예시:
```bash
# 포터블 ZIP에서 DeX 즉시 실행
./DXManager.Mac --dex

# 시스템 및 기기 진단 리포트 출력
./DXManager.Mac --diag
```

---

## 6. 대화형 콘솔 대시보드 조작 가이드 (Dashboard Guide)

대화형 콘솔이 시작되면 연결된 기기 목록, 현재 선택된 기기, 해상도/DPI 설정 및 상태가 실시간으로 표시됩니다:

```text
╔══════════════════════════════════════════════════════════════════════╗
║  DX MANAGER for macOS (.NET 8 Native Edition)                        ║
║  Samsung DeX & High-Performance Screen Mirroring Suite               ║
╚══════════════════════════════════════════════════════════════════════╝

▶ CONNECTED DEVICES & SYSTEM STATUS
  * [ACTIVE] [1] 현호의 S26 Ultra - Status: Connected
               Transports: Usb: R5CT1234567, Wireless: 192.168.0.50:5555

  Selected Device       : 현호의 S26 Ultra (R5CT1234567)
  Resolution / DPI      : 1920x1080 @ 200 DPI
  Stream Bitrate/FPS    : 24M / 60 FPS
  Screen Off / Awake    : ScreenOff=True, StayAwake=True

▶ OPERATIONS MENU
  [1] Start DeX Mode                [2] Stop DeX Mode
  [3] Start Single App Window       [4] Stop Single App Window
  [5] Wireless ADB Management       [6] File Transfer Coordinator
  [7] Diagnostics & Environment     [8] DX Companion Guardian
  [9] Settings & Configuration      [S] Select Active Device
  [L] View Recent Logs              [Q] Exit DX Manager
```

### 6.1 메뉴별 상세 기능

1. **`[1] Start DeX Mode`**:
   - 선택된 휴대폰에 가상 보조 디스플레이(Virtual Display Overlay)를 생성하고 Scrcpy를 통해 macOS 데스크톱에 삼성 덱스 화면을 엽니다.
2. **`[2] Stop DeX Mode`**:
   - 활성 DeX 세션의 Scrcpy를 종료하고 `overlay_display_devices` 정리를 요청합니다. 휴대폰 연결이 유지되면 가상 디스플레이 제거 결과를 확인하며, 먼저 연결이 끊기면 정리를 확인하지 못할 수 있습니다.
3. **`[3] Start Single App Window`**:
   - 가상 디스플레이 슬롯(1~3번)을 지정하고, 실행할 안드로이드 앱 패키지명(예: `com.sec.android.app.sbrowser`)을 입력하여 해당 앱만을 위한 독립된 가상 윈도우를 엽니다.
4. **`[4] Stop Single App Window`**:
   - 특정 단일 앱 슬롯 또는 전체(`A`) 단일 앱 창을 종료하고 리소스를 반환합니다.
5. **`[5] Wireless ADB Management`**:
   - **1. USB를 무선 모드로 전환**: USB로 연결된 기기에 `tcpip 5555`를 자동 적용하고 무선 엔드포인트로 전환합니다.
   - **2. 무선 기기 직접 연결**: IP 주소와 포트(기본 5555)를 입력하여 Wi-Fi 경유로 연결합니다.
   - **3. 무선 연결 해제**: 활성화된 무선 세션을 정상 종료합니다.
6. **`[6] File Transfer Coordinator`**:
   - Mac의 로컬 파일이나 디렉토리 경로를 스마트폰의 대상 폴더(기본: `/sdcard/Download`)로 전송(ADB Push)합니다.
7. **`[7] Diagnostics & Environment`**:
   - ADB, Scrcpy, .NET 8 런타임, 디바이스 SDK/One UI 버전, Companion 권한 상태를 종합 진단하여 점검 결과를 출력합니다.
8. **`[8] DX Companion Guardian`**:
   - 설치된 Companion의 상태와 권한을 확인합니다. 현재 macOS 공개 ZIP에는 Companion APK가 없으므로 자동 설치는 사용할 수 없습니다. 검증된 APK가 실제로 포함된 개발 빌드에서만 해시와 서명을 확인한 뒤 설치 메뉴가 동작합니다.
9. **`[9] Settings & Configuration`**:
   - 가상 디스플레이 가로/세로 해상도, DPI(기본: 160~240), 스트리밍 비트레이트(예: 16M, 24M), 최대 FPS(60/120), 화면 끄기(TurnScreenOff), 절전모드 방지(StayAwake) 설정을 인터랙티브하게 변경하고 영구 저장합니다.
- **`[S] Select Active Device`**: 연결된 여러 대의 갤럭시 기기 중 제어할 대상 기기를 전환합니다.
- **`[L] View Recent Logs`**: 실시간 세션 로그 및 ADB 트랜잭션 기록을 확인합니다.
- **`[C] Clear Screen`**: 터미널 화면을 정리하고 배너와 대시보드를 다시 그립니다.
- **`[Q] Exit`**: 활성 세션의 종료와 가상 디스플레이 정리를 요청하고 결과를 기다린 뒤 프로그램을 종료합니다. 휴대폰 연결이 먼저 끊기면 일부 정리를 확인하지 못했다는 메시지가 표시될 수 있습니다.

---

## 7. macOS Scrcpy 조작 및 단축키 안내

macOS 환경에서 Scrcpy 조작 시 기본 Modifier 키는 **`Option (⌥)`** 또는 **`Left Alt`**입니다:

| 동작 | macOS 단축키 | 설명 |
| :--- | :--- | :--- |
| **한/영 언어 전환** | **`Shift + Space`** | **DeX / 단일창에서 한국어 ↔ 영어 입력 전환 (스마트폰에 한국어 물리 키보드 등록 필요)** |
| 전체화면 전환 | `Option + F` 또는 `F11` | Scrcpy DeX 윈도우 전체화면 토글 |
| 1:1 창 크기 맞춤 | `Option + G` | 디스플레이 원본 픽셀 크기로 윈도우 리사이즈 |
| 스마트폰 화면 끄기 | `Option + O` | PC에서 미러링/DeX를 보면서 휴대폰 화면만 OFF |
| 스마트폰 화면 켜기 | `Option + Shift + O` | 휴대폰 화면 다시 켜기 |
| 전원 버튼 누름 | `Option + P` | 휴대폰 전원 키 에뮬레이션 |
| 클립보드 붙여넣기 | `Cmd + V` 또는 `Option + V` | Mac 클립보드 텍스트를 스마트폰으로 동기화 및 붙여넣기 |
| 텍스트 직접 주입 토글 | `Option + i` | Mac 키보드 텍스트 직접 주입 모드(`--prefer-text`) 켜기/끄기 |

---

## 8. 문제 해결 (Troubleshooting)

### Q1. DeX 종료 후 스마트폰 화면 구석에 작은 보조 화면이 남아있습니다.
> **원인**: 케이블을 강제로 뽑거나 비정상 종료 시 Android OS가 오버레이 설정을 유지할 수 있습니다.  
> **해결 방법**:
> 1. 대화형 콘솔에서 `[2] Stop DeX Mode`를 다시 실행합니다.
> 2. 또는 터미널에서 수동으로 ADB 명령을 전송합니다:
>    ```bash
>    adb shell settings delete global overlay_display_devices
>    ```
> 3. 또는 스마트폰 **설정 > 개발자 옵션 > 보조 디스플레이 시뮬레이션**에서 아무 해상도를 선택했다가 다시 **'없음'**을 선택합니다.

### Q2. `scrcpy`를 실행할 수 없다는 오류가 발생합니다.
> **포터블 ZIP 해결 방법**: `DXManager.Mac` 실행 파일만 따로 옮기지 않았는지
> 확인하고 ZIP 전체를 새 폴더에 다시 푸십시오. 같은 폴더의
> `tools/scrcpy/scrcpy`, `tools/scrcpy/adb`와 `scrcpy-server`가 모두 있어야
> 합니다. Homebrew 설치는 포터블 ZIP의 해결 조건이 아닙니다. 소스 개발
> 환경에서만 필요에 따라 PATH의 scrcpy를 fallback으로 사용할 수 있습니다.

### Q3. 기기 목록에 `unauthorized`로 표시됩니다.
> **해결 방법**: 스마트폰 화면을 켜고 잠금을 해제한 뒤, Mac에 대한 **"USB 디버깅을 항상 허용"** 팝업을 승인하십시오.

# DX Manager 새 작업 인수인계

마지막 갱신: 2026-08-22

이 문서는 긴 개발 대화를 보지 못한 새 Codex 작업에서도 DX Manager를 안전하게
이어가기 위한 현재 기준점이다. 자주 바뀌는 세부 진행 기록은 `SESSION.md`, 남은
작업은 `TODO.md`, 설계 이유는 `DECISIONS.md`를 함께 사용한다.

## 1. 새 작업에서 가장 먼저 할 일

1. 저장소 루트의 `AGENTS.md`를 읽는다.
2. 이 문서와 `PROJECT_BRIEF.md`, `SESSION.md`, `TODO.md`를 읽는다.
3. `git status --short --branch`, `git log -5 --oneline --decorate`와
   `git describe --tags --always --dirty`를 실행한다.
4. 미커밋 파일은 모두 메이님의 변경으로 간주하고 원인을 확인하기 전에는
   되돌리거나 삭제하지 않는다.
5. 진단·원인 파악 요청이면 수정하지 않고 먼저 조사 결과와 수정 방향을 설명한다.
6. 코드 변경 전 관련 서비스, 폼 partial, 모델과 문서의 기존 흐름을 함께 읽는다.
7. 검증과 배포는 `AI_WORKFLOW.md`를 따른다.

## 2. 현재 공식 기준점

- 제품 이름: **DX Manager**
- 현재 공개 버전: **v2.0.0**
- GitHub 업로드 준비 버전: **v2.0.1**
- Windows 앱: Assembly/File/Informational version `2.0.1`
- Android 앱 공개 이름: **DX Companion 2.0.0**, versionCode `6`
- 공개 태그: `v2.0.0` (`cec74a9`)
- 공개 태그 뒤 README 대표 이미지 갱신: `e8e47e6`
- 원격 저장소: `https://github.com/maze-mei/DX-Manager`
- 이 문서 작성 당시 작업 브랜치: `feature/v2-multi-device`
- v2.0.1은 최초 물리 기기 결속 뒤 DeX 기기별 설정이 공통 기본값으로 표시되던
  UI 동기화 회귀만 수정한 유지보수 릴리스다.

정확한 현재 HEAD는 이 문서 자체의 커밋 때문에 위 해시보다 앞설 수 있으므로 항상
Git 명령으로 다시 확인한다. `main`이라는 로컬 브랜치 이름보다 `origin/main`, 태그와
실제 HEAD를 기준으로 판단한다.

## 3. 제품과 지원 범위

DX Manager는 Windows에서 Samsung DeX overlay 가상 디스플레이와 Scrcpy 창을
관리한다. 여러 물리 Galaxy 휴대폰을 동시에 연결해 각 기기별로 DeX 또는 세 개의
단일 앱 창을 독립 실행하고, 유선·무선 ADB, 입력 보정, 화면 상태, 캡처, 양방향
파일 전송과 DX Companion을 함께 관리한다.

- Windows: 64비트 Windows 7 SP1, 8.1, 10, 11
- Windows 런타임: C# WinForms, .NET Framework 4.6.2, x64
- IDE/빌드 기준: Visual Studio 2019 MSBuild
- 외부 NuGet 패키지: 없음
- 번들 Scrcpy: 4.1
- Android companion: Java 17, min SDK 24, compile/target SDK 36
- DeX 모드 확인 기준: Android 16 / One UI 8.x의 New DeX 지원 Galaxy
- One UI 7.x 이하 및 Classic DeX 계열: DeX 모드는 검은 화면 등 정상 동작을
  보장하지 않으며 단일창 모드를 사용한다.
- 32비트 Windows, macOS와 Linux 빌드는 현재 제공하지 않는다.

## 4. 저장소와 산출물 위치

| 위치 | 역할 |
| --- | --- |
| `DexManager.sln` | Windows 솔루션 |
| `DexManager` | WinForms 본체 |
| `DexManager.AdbProxy` | Unicode 파일 드롭을 중계하는 공개 C# ADB proxy |
| `DexManager.MultiDeviceTests` | 외부 테스트 프레임워크 없는 다중 기기 회귀 테스트 |
| `DXDisplayCleanup` | DX Companion Android 소스 |
| `scripts` | Windows/Android 빌드와 패키징 |
| `docs` | 개발·사용자·릴리스 문서 |
| `docs/images` | 공개 README와 설명서 이미지 |

메이님의 원본 작업 및 **정식 릴리스 생성 위치는 항상**
`E:\vs\dex system`과 `E:\vs\dex system\dist`이다. C 드라이브의 Codex 작업
사본에 있는 `bin`, `obj`, `dist`는 개발·검증용이며 최종 릴리스라고 안내하지 않는다.

외부 작업 폴더에는 분석용 `repo-code.txt`, 이전 패키지, 다운로드 파일과 별도
`winget-pkgs` 사본이 섞여 있을 수 있다. 그것들은 이 제품 저장소의 변경으로
간주하지 않는다. WinGet manifest는 별도 저장소에서 관리한다.

## 5. 핵심 아키텍처

### 물리 기기와 transport

- `PhysicalDeviceRegistry`가 물리 identity를 기준으로 USB와 Wi-Fi ADB serial을
  하나의 휴대폰으로 합친다.
- 표시 이름은 UI 전용이다. 명령 대상은 작업 시작 시 캡처한 명시적 ADB serial이다.
- `DeviceRuntimeSessionRegistry`와 `DeviceRuntimeServiceSet`은 물리 기기별 독립
  서비스 묶음을 소유한다.
- 기기 탭을 바꿔도 비선택 기기의 DeX·단일창·전송·미니바는 계속 살아 있어야 한다.
- 연결 정책은 기기별 USB 전용 또는 Wi-Fi 전용이다. 선택 transport가 사라졌다고
  반대 transport로 자동 fallback하지 않는다.
- 같은 휴대폰의 transport가 바뀌면 물리 런타임과 설정은 유지하되 이전 serial에
  묶인 reverse, 전송과 세션 자원은 정리한다.

### DeX와 단일창

- `VirtualDisplayService`와 `DexOrchestrator`가 overlay 생성·재사용·정리 및
  display ID 판정을 담당한다.
- DeX overlay는 너비, 높이와 DPI가 모두 일치할 때만 재사용한다.
- display ID는 설정 및 실제 디스플레이 목록의 전후 차이로 찾는다. 가장 큰 숫자
  같은 추측값으로 선택하지 않는다.
- `SingleWindowService`는 Scrcpy `--new-display` 기반 단일창 세 개를 관리한다.
- Scrcpy 창 제목에는 휴대폰 표시 이름을 포함한다.
- `ScrcpyLaunchCoordinator`의 시작 직렬화와 프로세스 종료 규칙을 우회하지 않는다.

### 설정과 UI

- `AppSettings`는 공통 설정과 identity별 기기 설정, 단일창 앱 프로필을 저장한다.
- 기기별 해상도, DPI, 비트레이트, FPS, 앱, USB/Wi-Fi 정책과 자동 재연결 값은
  다른 휴대폰과 공유되면 안 된다.
- `MainForm.*`와 `SettingsForm.*`은 기능별 partial로 나뉘어 있다. 새 기능은
  단순히 `MainForm.cs`나 `SettingsForm.cs`에 몰아넣지 않는다.
- 설정 저장은 고유 임시 파일과 프로세스 간 잠금으로 직렬화된 atomic save다.
  다시 공용 `settings.json.tmp` 하나를 사용하면 동시 저장 경합이 재발한다.

### 파일 전송

- PC→휴대폰: Scrcpy 드롭을 `DXMAdbProxy.exe`가 관리형 전송으로 중계한다.
- proxy는 파일·폴더 push만 가로채며 DX Manager의 일반 ADB 명령에는 관여하지
  않는다.
- Unicode 이름, 폴더 구조, 빈 폴더와 이름 충돌을 보존·처리한다.
- ADB가 신뢰할 수 있는 byte 진행률을 주지 않으므로 거짓 퍼센트와 남은 시간을
  표시하지 않는다.
- 휴대폰→PC: DX Companion의 공유 메뉴/폴더 선택, 인증 토큰과 기기별 ADB reverse
  경로를 사용한다.
- PC 수신 폴더 아래에 휴대폰 표시 이름별 하위 폴더를 만든다.
- 한 기기의 연결 해제·취소·토큰 폐기가 다른 기기의 전송에 영향을 주면 안 된다.

### DX Companion과 Windows 종료

- Companion APK는 설치 전 SHA-256과 공식 서명, 설치 후 package·version·서명·
  권한을 검증한다. 검증되지 않은 APK에 `WRITE_SECURE_SETTINGS`를 부여하지 않는다.
- 공개 v2.0.0 APK SHA-256:
  `7CD40017789E22440DCA0291AB0C45ADB564A19D8A623E669F373395536B880F`
- Companion은 overlay 제거, DX Manager가 변경한 Stay awake 복원, 빠른 설정 타일,
  2 x 1 위젯, 휴대폰→PC 전송만 제공한다. 임의 shell 실행 기능을 추가하지 않는다.
- 연결 손실 자동 정리 선택지는 즉시, 1분, 5분(기본), 10분, 30분, 정리 안 함이다.
  같은 인증 세션이 다시 연결되면 예약 정리를 취소한다.
- Windows 종료 시 `WM_QUERYENDSESSION` 이후 **새 ADB 프로세스를 실행하지 않는다.**
  미리 인증된 Companion loopback 연결로만 PC 종료 메시지를 보낸다.
- Companion 미설치·미검증 기기는 Windows 종료 중 정리를 건너뛴다. 이때 무리하게
  ADB를 새로 띄우면 특히 Windows 7에서 네이티브 오류창이 반복될 수 있다.
- Alt+F8/트레이 종료는 Windows 종료 경로와 다르며 정상 ADB cleanup과 소유
  프로세스 정리를 수행한다.

### 입력

- Hangul 키는 scan code만으로 판정하지 않고 `VK_HANGUL` 환경도 고려한다.
- AltGr/브라질 키보드의 오른쪽 Alt와 한영키를 혼동하지 않는다.
- Scrcpy 4.x/SDL3 오른쪽 Shift 호환 보정은 실제 회귀 검증 없이 제거하지 않는다.
- injected 입력과 사용자 직접 Shift+Space를 구분하고 KeyUp 누락 시에도 상태가
  고정되지 않게 한다.

## 6. 현재 검증된 상태

- Windows 11에서 Galaxy S26 Ultra와 Galaxy S20 FE를 이용해 USB·Wi-Fi 혼합,
  두 무선 기기, 한 기기 DeX와 다른 기기 단일창 동시 실행을 확인했다.
- 탭별 실행 상태·설정 복원, 연결 해제 격리, transport 정책, 자동 DeX 시작,
  양방향 파일 전송과 Companion 관리가 기기별로 분리되는 것을 확인했다.
- 한 대만 연결하면 기기 선택 UI가 숨고, 두 번째 기기가 연결되면 나타난다.
- 동시 시작 시 최신 모델 우선, 순차 연결 시 첫 연결 순서를 유지한다.
- Windows 7 SP1에서 USB 복수 기기와 핵심 기능을 확인했다.
- .NET Framework 4.6.2 x64 Release 빌드는 경고·오류 없이 통과했다.
- `DexManager.MultiDeviceTests` 회귀 테스트 39개가 통과했다.
- DX Companion Android 단위 테스트 7개, `lintRelease`, v2 서명과 인증서 검증이
  통과했다.
- v2.0.0 공개 ZIP의 기록된 SHA-256:
  `D874021B8C3AC0B4DA7C69CBDFB4492DDE197426F954C8EB639796F26A287EBE`
- 공개 README 대표 이미지는 태그 뒤 `e8e47e6`에서 최종 교체했다.

실기 결과는 당시 환경의 사실이며 모든 Galaxy/One UI 조합에 대한 보장은 아니다.

## 7. 빌드와 검증

현재 작업 사본에서는 솔루션의 실제 절대 경로를 사용한다. .NET 4.6.2 Developer
Pack이 없으면 원본 저장소의 참조 어셈블리 경로를 지정한다.

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe' `
  '.\DexManager.sln' /t:Build /p:Configuration=Release `
  '/p:TargetFrameworkRootPath=E:\vs\dex system\.build-tools\net462\build' /m

& '.\DexManager.MultiDeviceTests\bin\Release\DexManager.MultiDeviceTests.exe'
```

Android 변경 시:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\Build-AndroidCleanup.ps1
```

개발 패키지 검증:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\Package-Release.ps1
```

공식 릴리스는 위 명령의 결과만 믿지 말고 `E:\vs\dex system\dist`에서 다시
생성·검사한다. 패키지에서 PDB, 로그, screenshot, 사용자 `settings.json`, Android
서명키·비밀번호를 제외하고 Scrcpy, proxy, Companion, 라이선스와 문서 포함 여부를
확인한다.

## 8. 다음 작업 우선순위

### 먼저 확인할 항목

1. Windows 7 회사 PC의 실제 Windows 종료에서 ADB 오류창이 사라지고 Companion이
   overlay와 Stay awake를 복원하는지 재확인한다.
2. 한국어 노트북 `VK_HANGUL + extended scan 0x38`과 브라질 ABNT/ABNT2·AltGr의
   `?`, `@`, 악센트 문자 회귀를 실제 키보드에서 확인한다.
3. Scrcpy 4.1에서도 오른쪽 Shift 보정이 필요한지 확인하고 필요하면 upstream에
   재현 정보를 보고한다.

### 합의된 차기 기능 후보

- Android 11+ 보안 무선 ADB 연결 포트가 바뀌는 문제를 줄이기 위한 mDNS endpoint
  자동 발견. 상세 안전 조건과 테스트 행렬은 `TODO.md`에 작성되어 있다.
- WinGet의 v2.0.0 manifest 상태 확인 및 최신 공개 자산 해시 반영.

### 테스트를 먼저 추가한 뒤 가능한 구조 개선

- Scrcpy 주 창/단일창의 종료 및 stdout/stderr drain 공통화
- `PhoneTransferReceiver`의 프로토콜과 저장 경로/충돌 처리 분리
- `WirelessAdbService`의 기기 명령과 자동 재연결 snapshot 계산 분리
- `AppSettings.EnsureDefaults`의 설정 범주별 helper 분리

### 논의됐지만 아직 확정하지 않은 장기 후보

- 기존 기능을 유지한 전체 UI/UX 재설계. 디자인 시스템을 먼저 확정하고 WinForms,
  Windows 7, 한·영문 길이와 DPI를 기준으로 단계적으로 적용한다.
- v2 안정화 뒤 Windows 10/11 전용 MSIX/Microsoft Store 변형. 기존 포터블
  .NET 4.6.2/Windows 7 빌드는 별도로 유지한다.
- One UI 9 정식판이 나온 뒤 실제 기기 호환성 확인.
- macOS/Linux 포팅은 테스트 장비와 별도 UI·플랫폼 구현이 필요해 현재 계획이 없다.

## 9. 회귀를 막기 위한 금지 사항

- 시스템 PATH의 `adb.exe`를 사용하지 않는다.
- 장치 명령에서 암묵적 전역 target serial 또는 `ANDROID_SERIAL`에 의존하지 않는다.
- 연결된 첫 serial, 가장 큰 display ID 또는 같은 모델이라는 이유로 대상을
  추측하지 않는다.
- 사용자가 USB 전용으로 정한 기기를 Wi-Fi로, Wi-Fi 전용 기기를 USB로 자동
  전환하지 않는다.
- Windows 종료 중 새 ADB·Scrcpy·보조 프로세스를 시작하지 않는다.
- 한 기기 cleanup 때문에 모든 ADB나 다른 경로의 동명 프로세스를 종료하지 않는다.
- Companion 서명 검증을 생략하거나 APK를 사용자 동의 없이 자동 설치하지 않는다.
- 관리형 전송 UI에 검증되지 않은 퍼센트·남은 시간을 표시하지 않는다.
- 사용자 설정, 로그, 캡처, 서명 비밀, 외부 분석 파일을 Git에 넣지 않는다.
- 공개용 언어 순서는 영어, 한국어 순서를 유지한다.

## 10. 메이님과 작업할 때의 진행 방식

- 사용자를 **메이님**이라고 부른다.
- 메이님은 문제를 먼저 설명하고 원인·대책을 합의한 뒤 “수정하자”고 요청하는
  흐름을 선호한다. 원인 파악 단계에서 선제 수정하지 않는다.
- 수정 요청을 받으면 합의된 범위는 자율적으로 구현·검증하되, 공개 push, tag,
  GitHub Release와 외부 게시물 수정은 별도 승인을 받는다.
- 실기 확인은 메이님이 Windows 11/Windows 7과 실제 Galaxy 기기에서 수행한다.
  결과를 코드 테스트 성공과 혼동하지 않는다.
- 스크린샷과 로그 폴더의 테스트 파일은 배포에 넣지 않는다. 삭제 전 경로가 저장소
  내부 생성물인지 확인한다.
- 정식 릴리스는 `E:\vs\dex system\dist`에서 만든다.

## 11. 새 작업 시작용 문구

다음 문구를 새 Codex 작업 첫 메시지로 사용할 수 있다.

> DX Manager 작업을 이어가자. 저장소의 AGENTS.md와
> docs/HANDOFF.md, PROJECT_BRIEF.md, SESSION.md, TODO.md,
> AI_WORKFLOW.md를 먼저 전부 읽고 Git 상태와 최근 커밋을 확인해줘.
> 현재 공개 기준은 v2.0.0이며 여러 휴대폰을 물리 identity별 독립 세션으로
> 관리한다. 기존 동작과 미커밋 변경을 보존하고, 바로 수정하지 말라는 요청이면
> 원인과 수정 방향만 먼저 설명해줘. 정식 릴리스 위치는
> E:\vs\dex system\dist야.

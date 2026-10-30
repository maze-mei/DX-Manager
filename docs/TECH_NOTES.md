# Technical notes

## Windows hidden-preview lease

`VirtualDisplayLease.IsLoopback` and `LoopbackSessionId` distinguish the helper-owned lease from the overlay workflow. `LoopbackDexService` verifies the bundled JAR hash and stable physical device identity before and after transfer. It holds one explicit-serial channel and token per session, sends 3-second heartbeats, and logs unconfirmed cleanup separately from process exit. Start generations cancel pending work; the next same-phone start waits for previous cleanup. Hidden DeX and 120 FPS share screen-on priority inside that phone's runtime.

`Build-DexLoopback.*` pins D8, uses JDK 17 and rebuilds the attributed helper reproducibly. `Verify-PhonePreviewBuild.ps1` and the Windows workflow exercise linked source, native settings/power/UI and package allowlists. See PHONE_PREVIEW_MODE.md. Contribution: asyouplz.


## Current Windows architecture — 2026-10-25

Baseline: code checkpoint `ccc10a9`, Windows2.5.0, .NET Framework4.6.2 x64, scrcpy5.0.1 and official Companion2.1.6/code13.
These notes describe the portable product; Store WinUI and the public macOS .NET8 implementation are separate.
The included companion/ release snapshot supplies public builds; canonical Android development remains shared.

### Physical devices and transports

`PhysicalDeviceRegistry` merges transports using stable physical identity rather than a friendly name.
`DeviceRuntimeSessionRegistry` and `DeviceRuntimeServiceFactory` bind one runtime service set to each phone.
Device commands capture an explicit serial; no global mutable target chooses commands for a live session.
New launches prefer authorized USB, then that phone's authorized wireless transport. Running windows retain their captured serial.
USB loss does not silently migrate those windows. A later manual start or a real reconnect follows the saved policy.

`MainForm.Devices` projects the device selector and device-owned run settings/app lists.
App profiles are keyed by Android package and stored per phone. DeX start app is optional; each single-window slot requires one.
A missing-app notice is scoped to its phone/slot.

### Launch, FPS and screen power

`DexOrchestrator` owns DeX overlay leases; `SingleWindowService` owns three separate scrcpy-created displays.
Overlay reuse requires matching width, height and DPI. Discover display IDs from observed state rather than guessing.
`ScrcpyLaunchCoordinator` serializes starts; the final shutdown check and `Process.Start` remain in the short process-start lock,
with window discovery/waits outside that lock.

`FrameRateRules` allows30/60/120, default60. Nullable `TurnScreenOffBeforeHighFps` preserves the prior true/false value
through root/default, per-phone DeX, slots, app profiles, normalization, clone and UI projection.
At120 screen-off is false/disabled. At30/60 restore the prior value and clear the backup.
Older experimental120 data without a backup uses the documented true fallback.

All actual120 UI starts/restarts use the common confirmation gate; service entry points also fail closed without confirmation.
Capture the runtime, serial and cloned settings before confirmation/delays. Default action is Cancel; no device launch I/O follows cancellation.
Builders omit `-S`/`--turn-screen-off`/`--no-power-on` at120 and reserve advanced `--max-fps` overrides.
Actual live-process FPS, not edited settings, determines a phone's screen-on requirement.
Any live120 DeX/app slot suppresses other starts/delayed screen-off on that phone; the last120 exit lets ordinary requests resume.
Other phones are independent. Manual power/shortcuts remain outside this automatic preference policy.

The official5.0.1 runtime receives `--hwdec=auto` unless an explicit hardware-decoder option is already supplied.
Use the selected scrcpy version/log output for diagnostics; a lower CPU reading is not decoder proof.

### Transfers, input and lifecycle

- `FileTransferCoordinator` and `DXMAdbProxy` intercept only managed scrcpy target-folder pushes.
  Device management/wake-up commands still use the explicit ADB service path.
- `PhoneTransferReceiver` uses authenticated per-phone local reverse sessions. Both directions preserve Unicode,
  structure and empty folders, reserve collision-safe sibling names and keep partial/committing states separate.
- The official Companion retains duplicate-share protection; don't combine a test share target with the official receiver.
- Keyboard/capture/mini-bar actions use the focused or explicitly owned session. Keep injected input separated from physical input.
  SDL3 right Shift→left Shift correction and optional manual Scroll Lock Enter conversion remain; no app tracker is present.
- Re-running the EXE signals the existing instance. Main-to-tray closes attached Settings/Help/dialogs while sessions continue.
  Idle auto-hide also minimizes scrcpy. Preserve user settings/history when overwriting the fixed preview.

### Online updates, local notes and consent

`GitHubReleaseSource` reads a numeric stable release tag and validated HTTPS release URL.
`UpdateCheckService` persists the local-day attempt before background networking, uses a five-second deadline,
single-flight control and no automatic offline loop. Manual checks stay explicit.
Automatic availability uses the lower-right banner or tray notification when hidden/minimized; current/offline automatic results are silent.
The GitHub button opens the release page without dismissing the banner.

`PortableUpdateNoticeService` uses independent `config/update-notice.json`, atomic writes and a monotonic version checkpoint.
Read previous safety acceptance before showing a new safety notice. Fresh config establishes a changes baseline;
existing accepted config can show an unseen version once. Show it before device startup/automatic120 consent.
Informational-note failures do not block startup; mandatory120 confirmation failure does.
First-run safety acknowledgement is independent of ordinary settings reset and absent from clean packages.

### Companion recovery and shutdown

Official Android source/APK are maintained in one shared component. The PC validates package/version/certificate/hash and grants protected
cleanup permission only to the trusted app on the selected phone. File-transfer connection and recovery permission are separate states.

Companion2.1.6 serializes connection/terminal callbacks with session-generation checks. Once the authenticated PC link is lost
and overlay/Stay awake are verified inactive, protection stops promptly, even with raw Wi-Fi still on.
Active/unverified recovery retains the existing transport/delay policy. Initial tunnel setup has a grace period.
Normal exit performs device cleanup; Windows shutdown blocks new ADB helpers and uses the established Companion channel best-effort.
Real shutdown delivery and every firmware are not guaranteed by headless tests.

### Packaging and verification

`Package-Release.ps1` copies an explicit runtime/license list. `Build-FinalRelease.ps1` backs up the old final and creates
`DX-Manager-win-x64.zip` containing `DX Manager v2.5.0/`. The39-file deliverable contains no personal config/history,
logs, captures, developer docs, screenshot assets, symbols, signing material or test programs.
The two scrcpy PNGs are required vendor runtime resources.

The prepared EXE is identical to the702-check English UI preview. Check exact hashes in [verification](VERIFICATION_v2.5.0.md).
Documentation-only work preserves the final ZIP/EXE/APK. Web guides can be expanded independently of the embedded document snapshot.
Changes to embedded resources require an explicit product rebuild and renewed artifact verification.

## 한국어

휴대폰별 런타임·명시적 serial·새 실행 USB 우선·실행 중 연결 고정,120 실행 동의와 원래 화면 끄기 값 복원,
동일 폰 화면 켜짐 우선, Unicode 전송·포커스 입력·트레이·정리 규칙을 유지합니다.
온라인 확인·로컬 변경사항·안전 동의는 별도 상태입니다. 현재 검증 수와 배포 파일의 해시는 위 기록을 따르며,
아래 과거 설계 기록을 현행2.5.0 동작으로 해석하지 않습니다.

<details>
<summary>Earlier implementation/preparation records (historical)</summary>

These dated records describe earlier states. Use the current sections above for2.5.0.


## 2026-10-24 English fallback coverage

DiagnosticsEnvironmentText.Text and DiagnosticsReportStatusText.Text lacked StoreStrings entries, exposing Korean fallbacks. Add matched EN/KO entries and English-safe fallbacks while retaining Korean wording. The955-call-site audit also found missing USB/Wi-Fi entries and Log.Wireless.DeviceMismatch; these are filled. Concatenated resource prefixes were checked against actual members.

InitializeDesktopLayout sets13px only for English Help after the common14px assignment and before RememberDesktopFonts. Existing font fitting preserves that choice; no renderer/global font change. LocalizationUiIntegrationTests checks full text fit at three window sizes and Korean Help parity. The English FAQ uses an English cross-language link label; language selection intentionally keeps the native 한국어 name.

The fixture selects English before creating forms on a Korean UI culture. User/device/app/file names, vendor output, native OS dialog language and third-party license content are not rewritten as application translations.


## 2026-10-24 diagnostics header layout

RefreshStoreEnvironment reparents RefreshDiagnosticsButton from EditorHost to HeaderSurface, then enables WideContent. Reflow previously expanded TitleLabel and DescriptionLabel across that button's rectangle, so the labels painted over most of it. Reserve178px of header text space for the wide DiagnosticsEnvironmentCard, matching its160px action plus margin. Keep EditorHost full-width for wrapped paths. Tests reproduce the old overlap and check expanded header/button bounds at520/716/900px and actual repeat clicks in light/dark themes.


## 2026-10-19 portable 120 FPS preparation

The maintainer authorized implementing the Store handoff in the portable product
and rebuilding the fixed preview/final ZIP locally. Store is read-only; reference
commit11050e7822c43ec409d7d943ece5d9884d997b99 is not merged.
Added default60/experimental120, one fail-closed warning before every120 start,
exact screen-off preference restoration, per-phone mixed-session screen policy
and local version-scoped change notes. scrcpy5.0.1 and signed Companion2.1.6 are
unchanged. No AI app detection, Android changes, commit/push/tag or publication.
See PORTABLE_120FPS_2026-10-19.md for implementation and fresh verification.
October16 scan evidence remains historical; the new EXE needs a fresh user scan.


## Current state — 2026-10-16

- Latest public Windows release:2.0.1. Prepared local Windows build:2.5.0; not published.
- Active portable branch: feature/portable-backport-v2.0.1-20260928, local HEAD8b19906 plus accepted uncommitted work.
- scrcpy 5.0.1; official shared Companion 2.1.6/code13/protocol1; Android 13 minimum.
- New launches USB-first; live serials stay captured. Companion management has its own Settings page.
- First-run consent is saved once; updates check daily in the background with offline failure recorded.
- The exact release ZIP/EXE/APK have been built and the maintainer reports clean antivirus results.
- Current work is local GitHub-documentation/screenshot preparation; no release approval has been given.
- Standalone public-source shared-component closure, remote macOS preservation and new portable captures
  are tracked in DOCUMENTATION_AUDIT_2026-10-16.md and SCREENSHOT_PLAN_v2.5.0.md.

See PROJECT_BRIEF.md, BUILDING.md and VERIFICATION_v2.5.0.md for the current contracts and boundaries.
Older dated implementation notes below describe earlier stages; their old version pins are historical.

## 주요 구조

`Program.cs`가 설정을 읽고 서비스를 조립한다.

- `PathService`: OS/설정별 ADB 선택
- `AdbService`: 전역 target 없이 명시적 serial 기반 ADB 실행
- `PhysicalDeviceRegistry`: 동일 휴대폰의 USB·무선 transport 병합
- `DeviceRuntimeSessionRegistry`: 물리 기기별 세션·전송·전원 상태 스냅샷
- `DeviceRuntimeServiceFactory`: 물리 기기별 독립 실행 서비스 묶음 생성
- `WirelessAdbService`: USB 준비, TCP/IP, 페어링, 재연결
- `VirtualDisplayService`: DeX overlay와 ID 탐색
- `DexOrchestrator`: DeX 실행/정리
- `ScrcpyService`: DeX Scrcpy
- `SingleWindowService`: 단일창 3개
- `ScrcpyLaunchCoordinator`: Scrcpy 시작 직렬화
- `ScreenOffService`: 남은 세션의 화면 OFF 재적용
- `KeyMappingService`: Scrcpy 활성 상태 키 보정
- `CaptureCoordinator`: F8 캡처
- `FileTransferCoordinator`: scrcpy 파일 드롭 IPC, 전역 FIFO 전송과 상태 관리

v2 런타임은 물리 identity 하나당 `DeviceRuntimeSessionSnapshot` 하나를 유지한다.
여기에는 활성 transport, DeX display/PID, 단일창 슬롯별 display/PID/HWND,
Companion 연결, 양방향 전송 수와 화면 OFF·절전모드 해제 복구 상태가 들어간다.
연결 해제 시에도 정리 증거를 보존하며, 표시 이름·연결·transport처럼 의미 있는
값이 바뀔 때만 revision과 registry generation을 올린다.

`DeviceRuntimeServiceSet`은 Scrcpy·DeX·단일창·화면 OFF·가상 디스플레이,
Companion 수신기와 PC→휴대폰 전송 큐를 함께 소유한다. 각 묶음의 고유 instance
ID는 한 물리 기기 런타임에 한 번만 결속된다. 서비스 묶음끼리는 상태와 큐를
공유하지 않고, 대상 상태가 없는 전역 시작 직렬화기만 공유한다. 메인 화면의
기기 탭은 물리 identity별 서비스 묶음을 선택한다. 탭을 바꿔도 비선택 기기의
Scrcpy·전송·미니바는 유지하고, 시스템 전역 자원인 캡처 단축키와 키보드 후킹만
선택한 기기 컨텍스트로 이동한다.
화면 끄기 재적용, 화면 깨우기와 `stay_on_while_plugged_in` 복원 판정은 현재 탭만
보지 않고 생성된 모든 기기 컨텍스트의 실행 세션을 합산한다.

## ADB

모든 명령은 선택한 `adb.exe`의 절대 경로로 실행한다.

1. 수동 모드면 지정 ADB 사용
2. Windows 10 미만이면 `tools\adb\legacy\adb.exe`
3. Windows 10 이상이면 선택한 Scrcpy 폴더의 `adb.exe` 사용
4. Scrcpy 폴더의 ADB가 없거나 실행되지 않으면 legacy ADB 사용

설정, 무선 연결, wake-up과 화면 상태를 포함한 DX Manager 자체 명령은 선택한
실제 ADB의 절대 경로로 실행한다. 관리형 파일 전송을 켠 DeX·단일창 scrcpy
프로세스만 `ADB` 환경 변수에 `tools\adb-proxy\DXMAdbProxy.exe`를 지정한다.

`adb version`의 첫 줄인 `Android Debug Bridge version 1.0.41`은 여러
platform-tools 버전이 공통으로 출력하는 프로토콜 문구다. 설정, 환경 점검과
로그에는 정규식으로 다음 `Version ...` 줄을 파싱한 실제 빌드 값을 표시한다.

일부 Windows 7 환경에서는 `adb start-server` 직후 ADB 프로세스가 반복
종료되거나 USB transport를 정상적으로 잡지 못할 수 있다. DX Manager는
기본적으로 ADB로 먼저 깨우고, 실패 시 설정에 따라 Scrcpy 기반 wake-up을
사용해 실제 push/shell/stream 경로까지 열어 ADB 연결을 초기화한다.

현재는 ADB 상태와 기기 이름을 즉시 확인한 뒤, DeX/단일창 실제 시작 명령
직전에 `ConnectedStartDelayMs`를 적용한다. 범위는 0~60초, 기본값은 1초다.
화면 OFF 재적용용 Scrcpy에는 이 대기를 적용하지 않는다.

기기별 ADB 명령은 `AdbCommandBuilder.ForDevice()`를 거쳐 반드시
`-s "SERIAL"`을 포함한다. `AdbService`는 target serial을 저장하지 않고
`ANDROID_SERIAL` 환경 변수도 사용하지 않는다. 무선 연결 계층은 기존 v1의
현재 선택 기기를 `SelectedSerial`로 보존하지만, 각 UI·orchestrator 작업은
시작 시 해당 값을 캡처한 뒤 명시적 매개변수로 전달한다.

DeX overlay 정리, 화면 전원, 앱 목록, 캡처 전송, Companion 권한·reverse와
단일창 재시작도 세션 또는 호출자가 전달한 serial만 사용한다. 파일 전송 취소와
Companion detach는 `DeviceSerialScope`로 요청 serial이 해당 작업의 serial과
같은 경우에만 수행한다. ADB 서버 관리와 기기 탐색 명령은 특정 기기 명령이
아니므로 serial이 없다.

## 관리형 파일 전송

기본값은 켜짐이며 설정 스키마의 `Features.ManagedFileTransferEnabled`에
저장한다. 설정 변경은 새로 시작하는 DeX·단일창 세션부터 적용한다. 끄거나
도우미가 없으면 실제 ADB를 scrcpy에 전달해 순정 파일 드롭 동작으로
되돌린다.

`DXMAdbProxy.exe`는 세션별 임의 named pipe, 토큰, 세션 ID, target serial,
세션 시작 시 고정한 휴대폰 대상 폴더와 실제 ADB 경로를 환경 변수로 받는다.
일반 ADB 명령은 인자와 표준 입출력을 유지해 실제 ADB로 전달하며, 해당 대상
폴더로 보내는 파일 또는 폴더 `push`만 DX Manager에 요청한다. scrcpy-server
push, shell과 단독 APK 설치는 가로채지 않는다.

`FileTransferCoordinator`는 다음 순서를 사용한다.

1. 요청 토큰, 세션, 고정된 기기 serial과 세션 대상 폴더를 검증한다.
2. 프로세스 제한시간과 분리된 전송 전용 프로세스로 전역 FIFO 큐를 처리한다.
   큐에 들어간 요청은 terminal 상태까지 별도 registry에도 유지해 dequeue와
   active 등록 사이의 짧은 순간에도 취소·세션 종료 요청을 놓치지 않는다.
3. 단일 파일은 `/sdcard/.dxm-file-GUID.part` ASCII 임시 이름으로 `adb push`한
   뒤 UTF-8/Base64 이름을 복원한다. 대상 폴더가 없으면 먼저 생성한다.
4. 폴더는 로컬에서 재귀 열거하고 정션·심볼릭 링크 등 재분석 지점을
   건너뛴다. 대상 폴더 안의 숨은 `.dxm-dir-GUID.part` staging 폴더에 파일을
   하나씩 전송하고 하위 폴더와 빈 폴더를 만든다.
5. 폴더 전체가 성공한 뒤에만 staging 폴더를 최종 최상위 이름으로 이동한다.
   취소나 실패 시 보이지 않는 staging과 현재 임시 파일을 정리한다.
6. Android의 UTF-8 255바이트 경로 구성요소 제한을 확인하고 같은 이름과
   충돌하면 파일은 `(1)`, `(2)`, 폴더는 `폴더 (1)` 방식으로 원자적으로
   이름을 정한다.
7. 최종 이름으로 이동하기 직전에 `/data/local/tmp/.dxm-commit-GUID.result`
   표식을 `P(준비)` 상태로 기록하고 이동이 끝나면 `C(완료)`로 바꾼다. ADB
   응답이나 제한시간이 이동 직후 끊겨도 임시 경로와 표식을 제한된 시간 동안
   재조회해 실제 커밋 결과를 복구한다. 결과가 아직 불명확하면 일반 실패 정리가
   증거를 먼저 지우지 않으며, 다음 유휴 첫 세션 준비 때 DX Manager 전용
   `.part`와 남은 표식을 정리한다.
8. 큰 폴더의 하위 디렉터리 생성 스크립트는 ADB 표준입력 전용 background
   writer로 보내므로 pipe write가 막혀도 취소와 제한시간 감시는 계속된다.

상태창은 현재 항목과 다음 대기 항목 4개, 원본 크기, 경과 시간과 누적
완료·실패·대기 수를 표시한다. 지원하는 모든 Windows/ADB 조합에서 신뢰할 수
있는 전송 바이트를 얻지 못하므로 퍼센트와 남은 시간은 표시하지 않는다.
사용자가 취소하거나 원본 scrcpy 창이 종료되면 해당 세션의 현재·대기 ADB
push와 shell을 중단하고 임시 데이터를 정리하며, 같은 드롭에서 이어지는
요청도 짧은 취소 구간 동안 거부한다. 최종 commit 구간에는 취소를 잠시 막는다.

휴대폰→PC 수신 경로 설정은 공통 상위 폴더다. 각 기기별 수신기는 표시 이름을
Windows 폴더 이름으로 안전하게 정규화하고 `상위 폴더\휴대폰 이름` 하위 폴더에
저장한다. 같은 파일 이름의 `(1)`, `(2)` 충돌 회피는 각 휴대폰 하위 폴더 안에서
독립적으로 적용된다.

## 무선 ADB

- v2에서는 설정 페이지에서 대상 물리 휴대폰을 먼저 선택한다. 메인 화면의 현재
  기기 탭이 기본 선택이며 각 identity별 프로필에 모드·IP·포트·자동 재연결을 저장한다.
- USB 무선 준비는 선택한 휴대폰의 승인된 USB transport serial을 정확히 사용한다.
- IP가 비어 있으면 선택한 `adb -s SERIAL shell ip route`의 `wlan` 경로에서
  `src` IPv4 주소를 우선 찾는다.
- `adb -s SERIAL tcpip PORT` 후 USB를 꽂아둔 채 해당 기기의 저장 주소 또는 감지
  주소로 `adb connect`한다. 저장 주소가 틀렸을 때만 감지 주소로 한 번 재시도한다.
- 연결된 endpoint의 실제 물리 identity가 선택한 기기와 다르면 설정 저장과
  transport 전환을 거부한다.
- 성공 시 해당 물리 기기의 선호 transport를 `IP:PORT`로 바꾼다.
- 설정 창은 메인 화면의 기기 탭 전환과 registry 변경을 따라 대상 목록을 다시
  읽는다. 라디오 버튼은 실제 연결 감지 결과가 아니라 사용자가 저장한 연결 정책을
  표시한다. 별도 상태 문구에는 현재 감지된 USB·무선 transport를 모두 표시한다.
- USB 모드는 무선 transport가 살아 있어도 사용하지 않고 승인된 USB가 나타날
  때까지 기다린다. 무선 모드도 USB로 대체하지 않으며 저장한 무선 transport를
  기다린다. 선택한 방식이 사라지면 해당 기기의 실행 세션과 전송을 정리한다.
- 두 transport가 모두 연결된 상태에서 연결 정책을 바꾸면 물리 기기 런타임은
  유지하되 이전 Scrcpy 세션·전송·ADB reverse를 정리한 뒤 새 serial을 사용한다.
- 자동 재연결은 최소 5초 간격이며 저장된 모든 기기별 무선 endpoint를 중복 없이
  순회한다.
- 페어링 포트와 실제 연결 포트는 다를 수 있다.
- 페어링 코드는 저장하거나 로그에 남기지 않는다.

## v1 기기 고정

`DeviceMonitorService`는 처음 선택한 기기를 앱 수명 동안 고정한다.
기기 식별은 `ro.serialno`, `ro.boot.serialno`, 마지막 수단으로 Android ID를
사용한다. 따라서 같은 휴대폰의 USB serial과 무선 `IP:PORT`가 달라도 같은
기기로 판정할 수 있다.

고정된 기기가 사라지면 연결된 다른 휴대폰을 선택하지 않고 Disconnected를
발행한다. 원래 휴대폰이 돌아오면 기존 연결 대기와 자동 실행 흐름을 다시
적용한다. 무선 주소가 사라졌다가 다시 나타나면 캐시를 폐기하고 실제 기기
식별값을 다시 읽어 다른 휴대폰의 주소 재사용을 방지한다.

기기가 보이지 않는 동안에도 ADB target serial은 고정값을 유지한다. 선택
서비스가 `targetWhenUnavailable`을 사용하므로 감시 주기마다 target을 비웠다가
다시 넣지 않는다. 백그라운드 장치 조회만 계속하고 연결 상태 이벤트는 실제
변경 시 한 번만 발행한다.

## DeX display ID

DeX는 `overlay_display_devices`로 만든다. 생성 전후 `dumpsys display`
스냅샷의 차집합을 구하고, 후보가 여러 개면 해상도/DPI로 좁힌다.
여전히 모호하면 실패시키고 후보를 로그에 남긴다. 최대 ID fallback은 없다.

기존 overlay가 있으면 문자열 전체가 아니라 실제 너비, 높이, DPI 숫자를
설정과 비교한다. 모두 같으면 ID를 등록해 재사용하고, 하나라도 다르면
`settings delete global overlay_display_devices`로 설정 항목 자체를 삭제한 뒤
다시 만든다. 정상 종료 시에도 생성 주체와 무관하게 같은 delete 명령으로
관리 기기의 overlay를 정리한다.

## Android 가상화면 정리 앱

`DXDisplayCleanup`은 `io.github.mazemei.dxdisplaycleanup` 패키지의 선택형 Android
앱이다. 공개 이름은 DX Companion이다. 복구 기능은 `Settings.Global`의
`overlay_display_devices`를 읽고 삭제하며 절전모드 해제를 끌 수 있다. 일반
앱에서 승인할 수 없는 `WRITE_SECURE_SETTINGS`를 ADB로 한 번 부여해야 한다.
삭제는 Settings provider의 `DELETE_global` call을 사용하고 다시 읽어 실제
제거 여부를 검증한다.

앱은 임의 shell이나 외부 서버 통신을 제공하지 않는다. 파일은 사용자가 Android
공유 메뉴나 시스템 폴더 선택기에서 명시적으로 고른 항목만 기기별 ADB reverse
수신 세션으로 전송한다. 네트워크 권한은 파일 전송 및 DX Manager가 만든
loopback ADB reverse 세션에만 사용한다. 메인 화면의 버튼 외에 Quick Settings `TileService`와
`AppWidgetProvider`를 제공한다. 활성은 컬러 DX 아이콘, 비활성은 흑백,
권한 없음/오류는 경고 상태로 구분한다.

### Companion 종료 감시 세션

`CompanionGuardianService`는 물리 기기 런타임마다 독립적인 임시 loopback
listener, 256비트 임의 토큰과 ADB reverse를 만든다. DX Companion의
`SessionGuardianService`는 package·인증서·versionCode·`WRITE_SECURE_SETTINGS`
검증을 통과한 경우에만 ADB shell의 `DUMP` 권한으로 시작된다. 앱은 protocol
magic·version·토큰을 모두 확인한 뒤 연결을 인정하며 3초 ping으로 세션 생존만
감시한다.

실제 Windows 종료는 일반 종료와 다르다. `WM_QUERYENDSESSION`에서 새 프로세스
실행을 먼저 봉쇄하고, 이미 열린 guardian socket으로만 기기별 cleanup 메시지를
보낸다. 이 단계에서는 `adb.exe`를 새로 실행하거나 종료하지 않는다. Companion이
연결되지 않은 기기는 정리를 건너뛰므로 Windows 종료 중 ADB 네이티브 오류창을
만들지 않는다. Alt+F8·트레이 종료는 기존 ADB 기반 overlay·stay-awake·reverse·
소유 프로세스 정리를 그대로 수행한다.

guardian 소켓만 잠시 끊어진 것은 cleanup 조건이 아니다. Companion은 DX Manager가
지정한 전송 방식의 물리 USB 연결 또는 Wi-Fi 연결까지 사라진 것을 확인한 뒤에만
기본 5분 유예를 시작한다. 같은 세션이 복구되면 예약을 취소하며, 사용자는
즉시·1분·5분·10분·30분 또는 자동 정리 안 함을 선택할 수 있다. 전송 방식을
판별하지 못하면 오정리를 피하기 위해 자동 정리하지 않는다. 명시적인 Windows
종료 메시지는 유예 없이 overlay를 제거하고 DX Manager가 기록한 stay-awake의
원래 값을 복원한다.

`overlay_display_devices`는 Android 전역 설정 하나이므로 생성 주체를 구분할
수 없다. 정리 앱은 현재 값을 삭제한 뒤 다시 읽어 비활성을 확인하며, 앱을
삭제했다가 다시 설치하면 package 권한도 사라져 ADB 권한 부여가 다시 필요하다.

DX Manager는 정확한 package ID만 믿지 않는다. 배포 폴더의 APK는 설치 전에
고정된 파일 SHA-256과 APK Signature Scheme v2 인증서를 검사한다. 휴대폰에
설치한 뒤 `base.apk`를 임시 폴더로 pull하고 단일 X.509 인증서, package와
versionCode를 다시 확인한다. 연결 serial을 고정한 채 권한 부여 직전과 직후에
재검증하고 사후 검증이 실패하면 즉시 `pm revoke`한다. 임시 APK는 검사 후
삭제한다. 설치·업데이트·재설치·삭제는 사용자가 누른 현재 선택 기기에만 적용한다.

휴대폰 폴더 탐색은 `/storage/emulated/0` 아래의 디렉터리 목록을 NUL 구분
UTF-8 바이트로 만들고 Base64로 받아 한글·Unicode와 공백을 보존한다. UI와
설정에는 같은 저장소의 사용자 표기인 `/sdcard/...`를 사용한다.

## 단일창

단일창은 DeX overlay가 아니라 Scrcpy가 직접 만든다.

```text
--new-display=WIDTHxHEIGHT/DPI
--start-app=PACKAGE
--window-title "DX Manager - APP NAME - DEVICE NAME"
```

슬롯마다 프로세스와 설정을 따로 관리한다. 강제 종료는
`--start-app=+PACKAGE`이며 기본값은 꺼짐이다. 앱 이름과 패키지를 함께
저장해 명령에는 패키지, UI와 창 제목에는 이름을 쓴다.
DeX 창도 `DX Manager - DeX Station - DEVICE NAME` 형식을 사용하므로 동시에 여러
휴대폰의 scrcpy 창이 떠 있어도 serial을 확인하지 않고 대상 휴대폰을 구분할 수 있다.

## Scrcpy와 화면 상태

옵션은 각 프로필 설정에 따라 붙는다.

- HID 키보드 `-K`
- HID 마우스 `-M`
- 화면 끄기 `-S --no-power-on`
- Scrcpy 4.0 이상 잠자기 방지 `--keep-active`
- Scrcpy 3.3.4 잠자기 방지 `-w`
- Scrcpy 4.0 이상 단일창 동적 크기 `--flex-display`

시작 시 `scrcpy --version`을 실행해 Scrcpy와 SDL 주 버전을 저장한다.
Scrcpy 3.3.4에서는 지원하지 않는 `--keep-active`와 `--flex-display`를
전달하지 않는다. Scrcpy 4.x/SDL3 출력은 UTF-8, 3.3.4/SDL2와 구형 ADB의
로컬 경로 출력은 Windows 기본 코드 페이지로 읽는다.

기준 번들은 Scrcpy 4.1/SDL 3.4.12다. 4.0에서 확인된 오른쪽 Shift 전달
문제를 위해 SDL3 기반 4.x 클라이언트에는 현재 호환 치환을 적용한다.

한 창이 끝나도 다른 세션이 화면 OFF를 원하면 `ScreenOffService`가 보이지
않는 Scrcpy를 다음 옵션으로 직렬 실행한다.

```text
--no-video --no-audio --no-window -S --no-power-on --no-cleanup
```

`Device display turned off`를 확인한 뒤 종료한다. 메인 Scrcpy와 동시에
시작하지 않는다. 모든 세션 종료, 프로그램 종료, 연결 해제 시 화면과
stay-awake 상태를 복구한다.

## 키 입력

- LowLevelKeyboardHook은 Scrcpy 활성 상태에서만 보정한다.
- 한영키는 전용 키의 `scanCode == 0xF2` 또는 일부 한국어 노트북의
  `VK_HANGUL + scanCode 0x38 + extended` 조합으로 감지한다.
- `scanCode 0x38`만으로는 한영키로 판정하지 않는다. 브라질·유럽 키보드의
  오른쪽 Alt/AltGr 입력은 `VK_RMENU`인 상태로 그대로 통과시킨다.
- SendInput scan-code Left Shift+Space를 우선 사용한다.
- SC1F2 KeyUp이 오지 않는 환경을 위해 주입 작업 완료 후 반복 방지 상태를
  반드시 해제한다.
- Enter 변환 기능의 시작 상태는 일반 Enter다.
- Scroll Lock으로 일반 Enter/Shift+Enter를 전환한다.
- SendInput 실패 시 ADB keycombination fallback이 있다.
- 오른쪽 Alt는 한영키와 분리한다.
- SDL3 기반 Scrcpy에서는 물리 오른쪽 Shift(`vk=0xA1`, `scan=0x36`)를
  차단하고 SendInput 왼쪽 Shift scan code로 치환한다.
- 오른쪽 Shift 치환은 Scrcpy 창이 활성화된 동안에만 적용하며 SDL2와
  다른 Windows 앱에는 적용하지 않는다.

Win32 `INPUT` 구조체 정렬을 임의로 바꾸면 32비트 Windows에서 SendInput이
실패할 수 있다.

## 다중 기기 로그

- 물리 기기 런타임 등록과 현재 탭 선택에는 표시 이름, transport serial,
  USB·Wi-Fi 연결 방식을 함께 기록한다.
- Scrcpy, 화면 OFF 보조 실행, 화면 전원, 파일 전송처럼 특정 기기에 귀속되는
  작업은 로그 메시지 앞에 `[serial]`을 붙인다.
- 기존 `DeviceMonitorService`는 최초 연결과 v1 호환 자동 시작을 위한 기본
  연결 감시 범위다. 해당 감시 대상은 v2에서 동시에 관리하는 전체 기기를
  뜻하지 않으므로 로그에도 `기본 연결 감시`라고 명시한다.
- Scrcpy 서버 파일 전송 성공 문구는 stderr로 출력되더라도 실패가 아니므로
  INFO로 분류한다. 설정상 정상인 Enter 변환 비활성화 상태는 최초 1회만
  INFO로 기록해 반복 경고를 만들지 않는다.

## 다중 기기 자동 시작과 기기 선택 표시

물리 기기 snapshot에서 선택 transport가 새로 연결되면 해당
`DeviceUiContext.ConnectionGeneration`과 serial을 캡처한다. deferred overlay 정리와
사용자 지정 연결 후 대기 시간이 끝난 뒤에도 세대, `ActiveSerial`, 저장된 transport
정책과 연결 상태가 모두 같을 때만 그 컨텍스트의 `DexOrchestrator.StartAsync(serial)`을
호출한다. 따라서 대기 중 탭 선택이 바뀌어도 올바른 기기가 시작되고, 연결 해제나
USB·무선 정책 변경 뒤에는 오래된 비동기 작업이 새 연결에 개입하지 않는다.

자동 시작 설정은 `GetDeviceRunSettings(settings, identity)`로 읽어 각 기기의
해상도·DPI·Scrcpy 옵션을 사용한다. 선택된 탭의 전역 편의 필드나 UI 저장 동작을
거치지 않으며, 시작 결과가 현재 선택 컨텍스트일 때만 메인 화면 상태를 갱신한다.

`_deviceTabsVisibleForRun`은 프로세스 실행 중 연결된 물리 기기가 두 대 이상인
snapshot을 한 번이라도 관찰하면 true로 고정되는 UI 수명 상태다. 연결이 한 대로
줄어도 분리된 컨텍스트 항목을 유지하지만 프로세스를 다시 시작하면 false에서 다시
계산한다.

`DevicePresentationOrder`는 registry 열거 순서와 별도로 사이드바 표시 순서를
보존한다. 첫 snapshot에 여러 기기가 있으면 모델 세대가 최신인 기기를 먼저 배치하고,
실행 중 새로 발견된 identity는 기존 항목 아래에만 추가한다. 따라서 ADB serial의
사전 순서가 UI 위치를 결정하지 않으며, 재연결과 transport 전환으로 기존 기기의
위치가 흔들리지 않는다. 세 대 이상은 제한된 기기 영역 안에서 스크롤한다.

## 정상 종료와 Windows 세션 종료

DX Manager 자체 종료 명령은 기기와 통신할 시간이 보장되는 정상 종료다. 모든
런타임을 순회해 화면 전원, 절전모드 해제, overlay와 ADB reverse를 정리한 뒤
`adb kill-server`를 실행한다. 그 다음 `ProcessRunner.BeginShutdown()`으로 새
프로세스 실행을 영구 차단한다.

`FormClosing`의 `WindowsShutDown` 또는 `TaskManagerClosing`은 제한 시간 종료 경로를
사용한다. 먼저 런타임과 기기 감시의 신규 작업을 차단한 뒤, 최대 5초 동안 일반 종료와
같은 기기별 overlay 제거·`절전모드 해제` 복원·화면 전원 복원을 시도한다. 정리가
끝나거나 제한 시간을 넘으면 process shutdown gate를 닫아 늦은 ADB·보조 프로세스
실행을 취소한다. 이후 프로세스 실행 파일의 절대 경로가 `AdbService.AdbPath`와 정확히
같은 `adb.exe`만 종료한다. 이름만 같은 다른 경로의 ADB는 대상이 아니다.

일반 종료와 Windows 세션 종료 모두 `Application.Run()`이 반환된 뒤 최종 프로세스
정리를 한 번 더 수행한다. 선택한 ADB, 번들 scrcpy 폴더의 ADB·scrcpy, ADB 전송
프록시의 절대 실행 경로만 등록해 같은 경로에 남은 프로세스를 종료한다. 따라서 ADB
서버처럼 원래 명령 프로세스에서 분리된 백그라운드 프로세스도 설치 폴더를 잠그지
않으며, 이름이 같아도 Android Studio나 다른 폴더에서 실행한 프로세스는 건드리지
않는다. 서비스가 보유한 PID/`Process` 정리가 주 경로이고 이 경로 비교는 종료 시의
최종 안전망이다.

프로그램 시작 시 `SEM_NOGPFAULTERRORBOX` 프로세스 오류 모드를 적용한다. 이 설정은
자식 프로세스에도 상속되므로 Windows 7이 세션 종료 중 ADB를 강제로 정리하더라도
네이티브 오류 대화상자가 반복해서 종료를 가로막지 않는다.

shutdown gate 이후 요청은 예외 창 대신 `Canceled` 결과를 반환한다. 이 취소는
오류 로그와 초기화 실패 대화상자를 만들지 않으며 Windows 종료를 취소하지 않는다.
반면 일반 실행 중 timeout과 실제 실패는 기존처럼 오류로 기록한다.

## 캡처, 숨김, 설정

F8 캡처는 Scrcpy를 전면으로 가져오고 오버레이를 표시한다. 캡처 직전
오버레이를 숨기고 약 100ms 기다린다. 전체 캡처는 client area만 찍는다.

자동 숨김은 설정 시간 동안 입력이 없으면 Scrcpy와 모든 UI를 숨겨 트레이로
보낸다. 이후 입력만으로 자동 복구하지 않는다.
Scrcpy 프로세스가 종료되는 순간과 타이머가 겹쳐도 프로세스 참조를 한 번만
고정해 읽으며, 타이머 예외는 UI 스레드 밖으로 전파하지 않고 로그만 남긴다.

- 설정: 실행 폴더의 `config\settings.json`
- 현재 스키마: 20
- DPI 입력 범위는 120~640이며 120 미만은 입력 확정 시 편집 전 값으로 복원한다.
- 사용자 지정 해상도는 프리셋과 별도로 보존하며 가로·세로 상한은 각각 4096
- 성공적으로 자동 실행한 앱은 최근 20개까지 공통 목록으로 보존
- 로그는 현재 실행 세션만 유지하고 수동으로 저장
- 경로/추가 인자 필드는 커스텀 프레임 안의 네이티브 TextBox를 사용해
  드래그 선택, 긴 문자열 가로 스크롤, `Ctrl+Z`와 IME를 지원

## 구조 감사 기준

`MainForm`은 기능별 partial을 사용하되 파일 이름이 책임을 나타내도록 유지한다.
기기 선택 영역과 현재 선택 UI는 `MainForm.Devices`, snapshot 조정·transport 전환·기기별
자동 시작은 `MainForm.DeviceConnections`, 연결 시각·연결 해제 표식·시작 대기는
`MainForm.DeviceConnectionState`가 담당한다. 실행 버튼과 세션 시작·중지는
`MainForm.Sessions`에 남긴다.

`AppSettings`는 기본값 생성과 마이그레이션·정규화 동작만 보유하고, 직렬화되는
설정 DTO와 열거형은 `AppSettings.Types`에 둔다. 사용자 입력 컨트롤은 기본 입력,
숫자·단축키 입력, 드롭다운 팝업 구현을 서로 다른 파일로 분리한다.

Scrcpy 주 창과 단일창 서비스에는 프로세스 종료, 출력 drain, 늦게 도착한 Exited
이벤트를 처리하는 비슷한 코드가 있다. 그러나 두 서비스의 슬롯 상태와 명시적 종료
규칙이 다르고 과거 종료 경합 회귀가 있었으므로 단순 공통 helper 추출은 하지 않는다.
공통화는 비정상 분리·사용자 창 닫기·동시 종료를 검증하는 전용 테스트를 먼저 만든
뒤 진행한다.

</details>

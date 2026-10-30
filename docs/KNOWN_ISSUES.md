# Known issues and limits

## Experimental phone-preview hiding

The Windows option is off by default. Samsung Android 12/API 31 and the required internal APIs are checked, but those checks do not prove model/One UI compatibility. Hidden-preview operation and real disconnect completion time remain physically unverified. The 15-second timeout starts cleanup; it is not a guaranteed firmware completion deadline. No automatic overlay fallback is performed after failure. A second dummy wireless stream may affect heat/battery/audio and requires testing. Existing Companion permissions and the normal overlay default are unchanged.


## Current scope — 2026-10-24

Windows2.5.0 / scrcpy5.0.1 / official Companion2.1.6. See [FAQ](FAQ_EN.md), [한국어 FAQ](FAQ_KO.md) and [exact verification](VERIFICATION_v2.5.0.md).

### Device and application limits

- Intended Windows targets are7 SP1/8.1/10/11 x64. Windows7 needs the framework/legacy ADB prerequisites.
  Automated702 checks are not physical verification of every OS/GPU/driver/DPI combination.
- Android16/One UI8.x and the maintainer's S26 Ultra/Android17/One UI9 are reported working cases.
  Older Classic DeX firmware may show a black desktop. Protected apps/content and secondary-display restrictions are not bypassed.
- Lower DeX resolutions or DPI combinations can change launcher layout, wallpaper and icon behavior. The configured virtual resolution
  differs from the PC scrcpy window's display size.
- Experimental120 FPS is a maximum only. Keep the phone screen on and confirm heat/battery/OLED warnings at every start.
  Firmware, app/activity, performance and transport can limit real FPS. Manual screen-off can still lower it.
- SDL3 right Shift compatibility maps it to left Shift in eligible scrcpy windows. Android cannot distinguish the sides in that session.

### Connection, transfer and cleanup limits

- Guest Wi-Fi/client isolation/VLAN/firewall rules can block local traffic or endpoint discovery even with the same Wi-Fi name.
  Pairing and connection ports differ. Verify phone ownership before updating an endpoint; don't guess another phone.
- New USB preference does not migrate live wireless sessions. USB loss stops affected sessions without an immediate Wi-Fi replacement.
- Android shares do not supply an arbitrary folder tree; use Companion's folder selector for folders.
  Transfer progress intentionally omits unsupported percentage/ETA. Disconnect/cancel/partial cleanup can require retry.
- Complete normal exit while still connected is preferred. Windows shutdown uses an existing authenticated Companion channel best-effort;
  no new ADB helper is started.
- Companion protection can remain while connected or while recovery is active/unverified.
  A lost PC socket alone does not guarantee recovery while the underlying configured transport still appears present.
  Version2.1.6 promptly stops a disconnected, verified-clean guardian.
- Companion cleanup removes the global simulated-display setting, including manually configured displays; it cannot identify their origin.

### Notifications and portable state

- A background update starts before the optional startup hide-to-tray finishes. If a response arrives unusually early,
  the main banner can be created before hiding and no separate tray notice may be issued at that moment.
  The cached banner remains available when the main window is reopened. This is a static ordering risk; no change was requested.
- Update checking is once per local day, not an Internet reachability monitor. Offline results consume the automatic attempt;
  use the manual button if a connection becomes available later. Cached update availability can remain offline.
- A fresh extraction folder lacks the previous config/history unless the user copies it. Safety consent can reappear in a clean folder.
  Local changes notes identify older installations through previous accepted consent, not settings-file existence alone.

### Source and release scope

The final Windows EXE has a maintainer-reported clean antivirus result for its exact D3342F… hash.
The public source includes the matching Companion source/constants/APK snapshot under `companion/`.
Historical scan results still refer to their own exact files.
Remote macOS projects/workflow/docs are preserved in the merged source, with separate .NET 8 proxy/foundation projects. Published Mac ZIPs were not found at the review date.
Documentation preparation does not authorize commit/push/tag/release.

## 한국어

기기·OS·GPU·펌웨어·앱별 제한,120 FPS의 최대값 특성과 발열·번인, 무선 포트·네트워크 격리,
연결 손실 시 정리의 한계는 위 현행 설명을 따릅니다.
프리뷰/최종 파일은 검증했지만 모든 환경의 실기 결과나 새 백신 검사로 확대하지 않습니다.
공용 Companion의 해당 버전 소스·APK 스냅샷을 공개 소스에 포함하고 macOS 프로젝트를 분리해 보존합니다.
아래 옛 Companion·UI·단일 기기 기록은 당시 상태이며 현재 문제 목록과 구분합니다.

<details>
<summary>Earlier implementation/preparation records (historical)</summary>

These dated records describe earlier states. Use the current sections above for2.5.0.


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

## 새 WinForms 테마의 검증 범위

밝음·다크 카드 배경은 GDI+ 알파 색상 혼합이며 실제 데스크톱을 비추는 Acrylic/Mica가 아니다.
제목 표시줄은 Windows 네이티브 테마를 따른다. 새 배치는 명령을 차단한 UI 사본에서
확인했으며 Windows 7·높은 DPI·실제 복수 휴대폰 선택 UI는 실기 확인이 남아 있다.

## Companion 2.1.2의 대기 전송 종료 및 2.1.3 수정 검증 한계

Android 15+ dataSync timeout 또는 서비스 제거 때 여러 전송이 대기 중이면,
실행되지 않은 작업의 finally가 호출되지 않아 URI 권한과 queued 이력이 남을 수
있다. 다음 작업이 공통 취소 상태를 되돌리는 경계도 Store 작업방에서 확인했다.
이는 코드 근거로 확인한 제약이며 이번 일반 전송 실기 성공과 구분한다.

포터블 Android 소스에는 요청별 추적·일회 종료 정리 보강과 회귀 테스트가 있다.
사용자 승인으로 새 서명 APK 2.1.3/versionCode 10에 반영했다. 실기 timeout 및 대기 전송 종료는 아직 확인하지 않았다.
문서 제공자에서 서로 다른 이름이 trim/slash 치환 뒤 같아지는 형제 항목은
2026-10-05의 서명 APK 2.1.4/versionCode 11에서 번호를 붙여 분리한다. 기존 번호 이름을
예약하고 파일 확장자와 폴더 하위 경로를 유지한다. Android 단위 테스트 20개 및 Release
lint/서명 빌드를 통과했으며, 실제 문서 제공자 전송은 아직 미확인이다.

## 네트워크 격리

무선 ADB는 PC와 휴대폰의 직접 로컬 통신이 필요하다. 같은 대역이어도 게스트
Wi-Fi, AP/client isolation, VLAN 또는 인트라넷 차단이 있으면
`adb connect`가 10060 timeout으로 실패한다.

ASUS 공유기는 게스트 네트워크의 인트라넷 접근 허용과 AP 격리 해제를
확인한다. 개발 중 5GHz 초기 연결 실패가 2.4GHz 연결 및 ADB 재시작 뒤
해소됐다. 재현 시 ARP, 5555 포트와 공유기 격리를 먼저 확인한다.

## 무선 준비 지속성

휴대폰 재부팅이나 Android 정책으로 `adb tcpip`가 풀리면 USB로 무선 준비를
다시 실행한다. IP는 비워두면 자동 감지된다.

## Windows 7

자동화된 Windows 7 테스트 환경은 없다. 릴리스 전 회사 PC 실기 확인이
필요하다.

## 여러 휴대폰

v2는 연결된 여러 물리 휴대폰을 동시에 관리한다. 각 휴대폰은 독립된 DeX·
단일창·설정·파일 전송·Companion 세션을 가지며, 같은 휴대폰의 USB와 무선
ADB transport는 하나의 기기로 병합한다. USB 또는 무선 중 사용자가 선택한
연결 방식만 사용하며 반대 방식으로 자동 전환하지 않는다.

현재 자동화 및 실기 기준은 휴대폰 두 대다. 설계상 더 많은 기기도 등록할 수
있지만, PC 자원·ADB 안정성·Samsung 펌웨어에 따른 실사용 상한까지 보장하지는
않는다.

## Android와 One UI 호환

현재 정상 동작을 확인한 휴대폰 기준은 Android 16 / One UI 8.x가 설치된 DeX
지원 Galaxy 기기다. One UI 7.x 이하에서는 가상 디스플레이에 DeX가 원활하게
나타나는지 확인되지 않았으며 검은 Scrcpy/DeX 창이 열릴 수 있다. 기기 모델,
Samsung 펌웨어와 클래식 DeX 구현 여부에 따라서도 결과가 달라질 수 있다.

단일창은 Scrcpy 자체 가상 디스플레이를 사용해 동작할 수 있지만 HID 키보드와
마우스 호환성까지 보장하지 않는다. 특정 기기에서 불안정하면 두 HID 옵션을
끄고 비교할 수 있으나 이를 해당 버전의 공식 지원으로 간주하지 않는다.

## Samsung DeX 저해상도와 DPI

Samsung DeX의 기본 최소 해상도는 1600×900이다. 더 낮은 가상 해상도도
실행될 수 있지만 앱 서랍 상단이 잘리는 등 일부 DeX UI가 정상 배치되지
않을 수 있다. 해상도와 DPI 조합에 따라 DeX가 서로 다른 바탕화면 배치와
배경화면 상태를 사용하는 것도 Samsung DeX 동작이다.

DPI는 120 미만에서 overlay 생성이 실패하므로 DX Manager가 입력을 거부하고
편집 전 값으로 복원한다. 매우 높은 DPI는 생성되더라도 UI가 지나치게 커져
사용하기 어려울 수 있다. 사용자 지정 가로·세로 값은 Android overlay 제한에
맞춰 각각 4096을 넘길 수 없다.

## 비정상 종료 뒤 overlay 잔존

정상 종료에서는 overlay를 제거한다. Windows 세션 종료 중에는 새 ADB 프로세스를
시작하지 않으며, 이미 인증되어 연결된 DX Companion guardian 세션이 있을 때만
overlay 제거와 `절전모드 해제` 복원을 요청한다. 다만 프로세스 강제 종료,
전원 차단, 이미 끊어진 기기, Companion 미설치·미연결 또는 Windows가 정리 시간을
주지 않는 경우에는 Android의 보조 디스플레이나 `절전모드 해제` 상태가 휴대폰에
남을 수 있다. 특히 Windows 7의 실제 종료 순서는 시스템과 ADB 상태에 따라 달라질
수 있으므로 Windows 종료 자체를 정상적인 세션 정리 방법으로 보장하지 않는다.
DX Companion을 사용하거나 개발자 옵션의
`보조 디스플레이 시뮬레이션`에서 임의 항목을 켰다가 다시 끄면 제거할 수 있다.
자세한 복구 절차는 사용자용
[`FAQ_KO.md`](FAQ_KO.md)에 정리했다.

## 외부 Scrcpy

Scrcpy 3.3.4와 4.x의 주요 옵션 차이는 자동 처리한다. 3.3.4에서는
`--keep-active` 대신 `-w`를 사용하고 `--flex-display`를 제외한다.
그 밖의 과거/향후 버전과 사용자가 직접 입력한 추가 인자의 호환성까지
보장하지는 않는다. 기준 번들은 Scrcpy 4.1이다.

## Scrcpy 4.x/SDL3 오른쪽 Shift 호환 보정

Windows용 Scrcpy 4.0/SDL3에서 물리 오른쪽 Shift가 Android로 정상 전달되지
않는 현상을 재현했다. 3.3.4/SDL2에서는 정상이며 `-K`, `-M` 사용 여부와
관계없이 발생했다. 이 4.0 재현 근거를 바탕으로 현재 SDL3 기반 Scrcpy 4.x
클라이언트에도 호환 보정을 적용한다.

DX Manager는 SDL3 Scrcpy가 활성화된 동안 오른쪽 Shift를 왼쪽 Shift로
치환한다. 타이핑은 정상화되지만 Android 앱에서 좌우 Shift를 서로 다른
키로 사용하는 경우에는 두 키를 구분할 수 없다. 다른 Windows 앱과 SDL2
Scrcpy에는 영향을 주지 않는다. Upstream 보고 및 수정 여부를 추적한다.

## 관리형 파일 전송

DX Manager 관리형 전송 대상은 `/sdcard/` 또는 `/storage/emulated/0/` 아래의
하위 폴더로 제한한다. 단독 APK 설치와 그 밖의 ADB 명령은 Scrcpy의 순정
동작을 유지한다. Android 경로의 각 구성요소는 UTF-8 기준 255바이트를 넘을
수 없으며, 충돌 접미사를 붙인 최종 이름도 이 제한을 만족해야 한다.

폴더 전송은 정션·심볼릭 링크 등 재분석 지점을 건너뛴다. 상태창에는 현재
항목과 다음 4개, 원본 크기, 경과 시간과 누적 완료·실패·대기 수를 표시하지만
신뢰할 수 없는 퍼센트와 남은 시간은 표시하지 않는다. 전송 중 Scrcpy 창,
기기 연결 또는 DX Manager가 종료되면 현재 전송을 취소하고 임시 파일·staging
폴더 정리를 시도한다. 이미 기기 연결이 사라졌다면 정리가 실패해 로그만 남을
수 있다.

설정에서 관리형 전송을 끄면 새로 여는 DeX·단일창부터 순정 Scrcpy 파일
드롭을 사용한다. 이미 실행 중인 창의 환경 변수는 바뀌지 않는다. 순정 방식은
일부 Windows 환경에서 한글 등 비ASCII 로컬 파일명을 보존하지 못할 수 있다.

## 앱별 보안 및 다중 디스플레이 제한

일부 금융·게임·스트리밍 앱은 USB 디버깅 또는 개발자 옵션 활성 상태를
감지해 실행을 거부한다. 보호된 화면과 DRM 콘텐츠는 Scrcpy에서 검게 보이거나
캡처가 차단될 수 있으며, 가상·보조 디스플레이 실행 자체를 거부하는 앱도
있다. 이러한 앱 및 Android 보안 정책은 우회하지 않는다.

다중 디스플레이를 완전히 지원하지 않거나 기존 휴대폰 task를 재사용하는 앱은
DeX에서 시작해도 휴대폰 화면에서 열릴 수 있다. 선택 앱 강제 종료가 일부
경우에 도움이 되지만, 앱 자체 정책까지 강제로 변경할 수는 없다.

## Android 정리 앱 권한 부여

공식 DX Companion이 설치된 경우에만 진단 페이지에서 권한을 부여할 수 있다.
번들 설치 기능은 정확한 APK SHA-256과 v2 서명 인증서를 먼저 검사하고 현재
선택된 ADB 기기에만 설치한다. 설치 후에도 `base.apk`의 서명, package와 버전을
다시 확인한다. 패키지 누락·서명 불일치·ADB 미승인 상태에서는 설치 또는 권한
버튼을 비활성화하며 다른 서명의 같은 패키지에는 권한을 부여하지 않는다.
권한 부여 직전과 직후에도 다시 검사하고 사후 검증이 실패하면 즉시 회수한다.

Companion은 Android의 단일 전역 `overlay_display_devices` 설정을 삭제하므로,
DX Manager가 만든 화면과 사용자가 개발자 옵션에서 직접 선택한 보조
디스플레이를 구분할 수 없다. 정리하면 현재 설정된 시뮬레이션 화면이 모두
제거된다. 앱을 삭제한 뒤 다시 설치하면 권한도 사라져 다시 부여해야 한다.
개발자 옵션의 절전모드 해제를 끄는 기능도 제공하며, 타일과 위젯의 기본
동작은 두 항목을 함께 정리하는 것이다.

## 배포

- 설치 프로그램 없음
- Assembly/File version은 `2.0.0.0`
- 앱 아이콘, 제작자/GitHub 링크, MIT 라이선스와 제3자 고지 완료
- README, 사용 설명서와 FAQ의 한국어/영어 스크린샷 배치 완료
- 공개 ZIP에서 개인 설정, PDB, 로그와 테스트 스크린샷 제외 확인
- 자동화 테스트 없이 주요 흐름은 실기 테스트에 의존

## 2026-10-05 final acceptance boundary

메이님이 실기 검증을 완료했고 잘 작동한다고 보고하여2.5.0 최종본으로 준비했다. 위의 이전 미확인 기록을 사용자 확인이 전혀 없다는 뜻으로 읽지 않는다. 사용자가 명시하지 않은 모든 OS/DPI 조합, 강제 Android timeout 또는 드문 ADB Kill 실패를 독립 재현했다고 확대하지 않는다. GitHub 업로드와 바이러스 검사 결과는 현재 대기 상태다.

</details>

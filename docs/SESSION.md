# Session Handoff

## 2026-10-30 — experimental Windows phone-preview proposal by asyouplz

### English

Prepared an opt-in, per-phone hidden DeX preview feature on upstream v2.5.0. Feature integration and regression tests are contributed by [asyouplz](https://github.com/asyouplz); ScrcpyDeX original copyright and Apache-2.0 notices remain in the helper source and bundle. The attributed helper is reproducibly rebuilt and hash-pinned.

Local checks passed: net462 x64 cross-compilation with zero warnings/errors, 29 linked Windows-source regression tests and 18 Java host checks; the native WinForms fixture compiles. The added Windows workflow builds the original solution, runs foundation/120 FPS/feature tests and independently inspects the package. Physical Galaxy/One UI behavior, actual disconnect completion time and Windows 7/11 hardware acceptance remain unverified. This proposal changes no release version, update channel, Companion APK or Mac implementation and publishes no release.

### 한국어

asyouplz가 기여한 Windows 휴대폰 미리보기 숨김 선택 기능을 제안한다. 기본값은 꺼짐이며 기기별로 저장한다. 원저작자 표기는 유지하고 기여 표기는 문서·NOTICE와 커밋에 남긴다. 실제 Galaxy와 종료 시간은 실기 확인 항목으로 유지한다.


## 2026-10-25 authorized Windows 2.5.0 publication

The maintainer authorized the local final commit and GitHub upload. The final EXE has a maintainer-reported clean scan.
Committed the refreshed docs/photos plus a verified official Companion source/APK export.
Merged public main while preserving all Mac implementation/tooling; isolated its .NET 8 proxy/foundation projects
from the tested Windows framework projects. Public Windows checkouts can use the included companion snapshot.
Preserved scanned EXE/APK/ZIP bytes and all Store files. See PUBLICATION_v2.5.0.md for release boundaries.

## 2026-10-24 GitHub documentation and approved image refresh

The maintainer requested local updates to all GitHub-facing documents after code/package checkpoint ccc10a9.
Compared fresh public main2a1ec107450d8755e3691e9fa52aa086ed034e8d,40 document/config paths and six historical releases.
README/full EN-KO guides/35-question FAQs/release notes/current developer docs now reflect2.5.0, fixed archive naming,
120 consent/screen preference rules, daily online and local changes notices, official Companion2.1.6 and real UI paths.
Corrected obsolete None-in-single-window, device-list, apply/restart, hidden legacy switch/timing and capture-transfer directions.

Approved16 actual user captures are linked with truthful initial/connected-state captions; no single-window photo.
Six native Android examples remain. Ten unused old app images were backed up/retired; all user originals are unchanged.
Preserved remote Mac guide/status and verbatim package template/runtime text/notices; code/workflow integration remains separate.
Relative Markdown/HTML links, anchors, FAQ numbering, image integrity/source hashes and protected artifact hashes pass.
No executable/resource/source-code changes, rebuilds, phone installs or new feature-test/antivirus claims.
Final EXE/APK/ZIP hashes remain exactly as in VERIFICATION_v2.5.0.md; source docs/images stay outside the binary ZIP.
No local documentation commit, merge, push, tag, GitHub release or old-asset rename was performed.
Public shared-component source availability, macOS integration, exact-file AV evidence and publication approval remain gates.



## 2026-10-24 local final package and authorized commit

The maintainer authorized a local Git commit and final upload-file preparation, without GitHub publication.
Final archive: E:\vs\dex system\dist\DX-Manager-win-x64.zip; one DX Manager v2.5.0 folder,39 verified files.
ZIP SHA256: EBB91A332B603C4096B1B1FB6D179F4D45B55F5A63FC657F8B989BEA8DE08764.
EXE SHA256: D3342F2A2B80DC5945D069CF408412A4DEDEBB6C0A54C78795485CB5DAD5FC19; identical to the702-check English UI preview, so functional/update checks were not repeated.
First-run consent state, personal settings/logs/captures, source guides/screenshots, test binaries and signing material are excluded.
Required runtime resources, notices and the short package README remain. Official Companion2.1.6 and scrcpy5.0.1 are unchanged.
The old version-named ZIP was backed up under .build-tools/release250-ready and removed from dist to avoid duplicate delivery names.
Existing source config/settings.json was checked as a tracked empty-default template and is not shipped in the ZIP.
An obsolete font-lab helper's personal interpreter path was replaced with python.exe. No product executable changes.
The user chose no single-window photos, diagnostics before the check, the newly captured Companion photos and the default-size connection image.
Full README/guide/screenshot updates are deferred at the maintainer's request. Public-source shared-Companion closure and remote-main/macOS preservation remain pre-publication gates.
No push/tag/merge/release, old GitHub asset renaming, external messages or Store changes.


## 2026-10-24 English UI audit and preview

- The maintainer found Korean initial diagnostics text and clipped English Help text while capturing screenshots.
- Both diagnostics fallback calls and the14px Help font already exist in HEAD8b19906. The preceding header/button patch did not change them; English initial state was missing from the earlier fixture.
- Added EN/KO diagnostics initial/report resources with English-safe fallbacks. The embedded English FAQ's Korean navigation label now reads Korean FAQ.
- Only English Help uses13px instead of14px, before font fitting. Korean Help and other sidebar fonts retain their sizes.
- Reviewed955 resource call sites,647 Store/792 general English resources, matched Korean keys and English embedded help. The only intentional Korean values are language-selector names.
- Filled missing USB/Wi-Fi entries and the wireless-device-mismatch log resource; no connection/input logic changes.
- Added the English UI fixture to the standard verifier: all main modes,7 expanded Settings pages before/after diagnostics,5 Help topics, guide/FAQ, transfers, logs, update outcomes and both themes.5458 resource/control texts scanned. English Help fits1184/1200/1600 widths; Korean font parity checked.
- Release build0 warnings/errors;702 Windows checks passed, including36 English-localization checks. Internal captures visually reviewed.
- Fixed preview updated;9 user files preserved. EXE SHA256: D3342F2A2B80DC5945D069CF408412A4DEDEBB6C0A54C78795485CB5DAD5FC19.
- Prior diagnostics repair remains covered. October19 final EXE/ZIP, official APK and Store hashes unchanged. No commit/push/tag/publication; include both October24 fixes in final packaging.


## 2026-10-24 diagnostics button preview fix

- The maintainer reported a clipped Refresh button after Settings > Diagnostics > Environment check.
- Reproduced with the actual pre-fix UI: full-width header labels covered the reparented header button.
- StoreSettingCard now reserves the same header action space for expanded environment results; the result body keeps its full width. No font/rendering, ADB, Companion, Store or update-notification behavior changes.
- Added long-path, 520/716/900-width, fully visible header button and repeated-click regression checks in both themes.
- .NET Framework4.6.2 x64 Release:0 warnings/errors. Windows666 checks passed (desktop196). Actual UI captures were visually reviewed. These are internal fixtures, not real-phone documentation photos.
- Fixed preview updated in place;9 user files preserved. Preview EXE SHA256: AF99EEA385F8E7CF84FF643AA597258983E7B75522FB226D6B5DE7856A10BF59.
- Existing October19 final EXE/ZIP, Store upload and official APK hashes remain unchanged. The preview fix must be included when the maintainer later requests final release packaging. No commit/push/tag/publication.


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


## 2026-10-01 Reported UI defects

메이님이 9월 30일과 10월 1일에 지적한 UI 문제를 수정했다. 최종 크기 기준 창 중앙 배치,
연결이 끊긴 폰의 선택기 제거 및 시작 비활성화, 설정 선택기의 이름만 표시, USB/Wi-Fi
수직 중앙 정렬, 일반 글자 +2 DIP, 하단 도움말/설정 위치, 긴 옵션/버튼 공간을 조정했다.
둥근 카드/버튼에 실제 Region을 적용했고 설정 펼침은 40px 클릭 영역과 그린 꺾쇠로 바꿨다.
고급 인수 입력칸을 위로 옮기고 접을 때 스크롤 범위를 초기화한다. 메인/설정/도움말/문서
패널의 스크롤은 Win7 호환 GDI 막대를 사용한다. 페이지 전환은 redraw/layout을 묶었다.
사용 설명서/FAQ는 별도 스크롤 패널과 카드/머리말/닫기 버튼을 갖고 포터블 문서를 사용한다.
진단 하위 패널의 테마 갱신 누락을 수정하고 Store 원본의 폰별 둥근 카드(14 padding,
10 radius, 최소 높이 150)를 반영했다. Store 작업트리에는 쓰지 않았다.
회귀 Debug/Release 59/59. 격리 UI에서 중앙 배치, 연결 해제, 이름 표시, 고급 접기,
문서 스크롤 분리와 설정 저장을 확인했다. 실제 화면 캡처는 computer-use 앱 승인이
시간 초과돼 확보하지 못했다. RichTextBox 본문은 DrawToBitmap에서 빠지는 제한이 있으며
해당 빈 캡처를 실제 본문 누락으로 단정하지 않는다. Win7/실제 폰/다른 DPI 및 전환 시
체감 깜빡임은 사용자 확인 필요. 이미지마다 별도 시간 폴더를 사용한다.
빌드는 bin/Release의 검증용이며 커밋/업로드/배포는 수행하지 않았다.

## 2026-10-01 Store UI fidelity correction

메이님의 지시로 일부 GitHub 설정 UI를 섞었던 9월 30일 시도를 Store 소스 기준으로
다시 구성했다. 기준은 Store 8b267c2의 Styles/SettingsWindow/MainPage/help.json이다.
폰트는 같은 Pretendard 원본의 400/600 정적 인스턴스를 임베드하고, OFL 예약 이름
규칙을 지키기 위해 내부 패밀리명만 DX Store UI로 바꿨다. GDI+/WinForms/.NET 4.6.2를 유지한다.
버튼은 상태 라벨의 실제 겹침으로 깨지고 있었으며 배치와 아이콘 단독 표시를 수정했다.
설정 49개 카드·펼침 항목을 재현하고 선택·토글은 범위별 즉시 저장, 주소·경로는
작업/편집/닫기 경계에서 저장한다. 한/영과 Enter 입력 방식은 각각 저장한다.
연결/Companion 선택기는 같은 대상이며, 과거 대상의 상태 응답을 현재 대상에 적용하지 않는다.
도움말 6개 카드·단계·주의 블록과 설명서/FAQ 창, 메시지 유지 규칙, 첫 실행 안내
(onboarding.json 버전 2, 설정 초기화와 독립)을 이식했다.
Debug/Release 회귀 59개 통과. 명령을 차단한 UI 사본에서 페이지·저장·오류 메시지를
검증했다. Windows 7·다른 DPI·실제 휴대폰을 쓰는 새 UI 검증은 아직 남아 있다.
이미지는 .build-tools/ui-review/20261001-시간/ 폴더별로 보존하며 덮어쓰지 않는다.
자세한 사항은 STORE_UI_BACKPORT_2026-10-01.md. Store 파일 및 Git 업로드는 변경하지 않았다.

## 2026-09-30 Windows 7 호환 테마 UI 구현

메이님이 밝음·다크 이미지 시안을 보고 실제 구현을 승인했다. UI는 WinForms 및 GDI+
.NET Framework 4.6.2 API만 사용한다. 네이티브 제목 표시줄을 유지하며 카드 배경에만
알파 색상 혼합을 적용한다. Windows 10/11의 Acrylic/Mica나 투명한 입력창은 사용하지 않는다.
메인: 상단 물리 기기 선택, 기기 상태·시작/중지, 나란한 디스플레이·실행 옵션 카드,
시작 앱 별도 카드, 오른쪽 아래 닫을 수 있는 앱 목록 알림. 작은 화면은 카드가 세로로
배치되고 스크롤된다. 기존 identity별 선택·설정 저장·런타임 명령은 유지했다.
설정: 일반·입력·연결·Companion·경로·진단·정보 메뉴. Companion 카드와 기존 핸들러를
별도 페이지로 옮겼다. 저장 버튼으로 적용하는 기존 동작을 유지했다. 기본 동작을 행으로
표시하고 전체 테마·대비를 조정했다. 도움말의 기본 안내 화면과 로그 화면도 테마를 적용했다.
첫 실행 경고의 확인 저장·실행 차단 기능은 이번 이미지 시안의 테마 구현 범위에 넣지 않았다.
검증: .NET 4.6.2 x64 Release, 회귀 57/57, 명령 실행을 차단한 UI 사본의 밝음·다크·
단일창·920x696 스크롤 레이아웃·도움말·로그·알림 렌더링. 이미지의 기기 이름은 예시이다.
Windows 7 실기, 다른 DPI와 실제 복수 휴대폰의 새 선택 UI는 아직 확인하지 않았다.
사용자는 이 UI 변경 전 기기별 앱 목록 분리 동작을 실기 확인했다.
Store 제품 파일은 변경하지 않았고 commit/push/tag/배포는 하지 않았다.

## 2026-09-30 Companion 2.1.3 서명 APK 반영

메이님이 대기 전송 종료 수정의 APK 반영과 Store 작업방 참고 메시지를 승인했다.
이 결정은 앞선 Store 2.1.2 APK 그대로 재사용 결정을 대체한다.
새 APK는 2.1.3/versionCode 10, minSdk 33/targetSdk 36이며 기존 공식 인증서를 유지한다.
SHA-256: 790A0F830BA182A74D27628072ABB709CB77904690E3AFA8797DEAF480392A62.
포터블 번들과 PC version/hash 검증 및 패키징 계약을 갱신했다.
Android 테스트 13개, lintRelease/assembleRelease, 공식 v2 서명·권한·패키지 검증과
.NET 4.6.2 x64 Release/회귀 57개가 통과했다. 실기 timeout 재현·업데이트 설치는 미확인이다.
Store 등록 대기 산출물과 작업 트리는 변경하지 않는다. 다음 Store 작업 때 참고하도록
문제 원인·변경 파일·새 APK 정보를 메시지로 전달한다. Git 커밋·업로드는 하지 않았다.

마지막 갱신: 2026-09-30

## 2026-09-30 이번 역이식 변경분 감사

메이님이 보고한 공통 앱 목록 버그를 Store 구현과 대조해 수정했다. 조회 결과는
물리 identity별로 캐시하고 기기·모드 전환 시 해당 목록을 복원한다. 늦은 A 응답은
A 캐시에만 저장한다. 최근 앱은 기기별 프로필에 저장(schema 27)하며 출처를 알 수
없는 기존 전역 최근 앱 데이터는 저장해 두되 여러 기기에 자동 배정하지 않는다.
완료 시 불러온 개수를 포커스를 옮기지 않는 오버레이로 5초간 표시한다.

감사 범위는 로컬 v2.0.1 이후 이번 변경·추가 파일과 필요한 기존 경계뿐이다.
stale 무선 transport를 실제 연결·identity로 재검증하고 새 USB 정책·수정 endpoint를
보존한다. 진단 로그의 scoped/raw IPv6·이름·identity 마스킹과 카드/font 정리를
보강했고, Companion의 Pretendard 라이선스를 배포 구성에 넣었다.

Android의 이전 소스 전용 수정과 실제 서명 APK 동작을 구분했다. Store 작업방이
확인한 다중 대기 요청 timeout 문제는 요청별 소스 추적기로 보강했다. Debug 검증용
소스 수정이며 기존 서명 2.1.2 APK에는 포함되지 않는다. 새 APK는 만들지 않았다.
배포 반영에는 새 서명 버전과 PC hash/version 계약의 별도 결정이 필요하다.

메이님은 일반 Release DeX·파일 전송·공유 저장소 최상위 경로·캡처·독립 미니바가
정상이라고 확인했다. 새 앱 목록 수정은 재확인해야 한다. 격리 실행에서 나타난
설정 임시 파일 문제는 사용자 지시로 감사에서 제외했다.
상세 근거는 PORTABLE_BACKPORT_AUDIT_2026-09-30.md에 기록했다.
검증은 .NET 4.6.2 x64 Debug/Release와 회귀 57개, Android 소스 테스트 13개·
lintDebug·assembleDebug다. Store 제품 작업 트리와 기존 APK는 유지했다.

## 2026-09-29 포터블 역이식 현재 상태

로컬 공개 `v2.0.1` 태그 `2a4aff2`에서 분기한
`feature/portable-backport-v2.0.1-20260928` 작업 폴더에서 공통 버그를
먼저 처리했다. Store 작업 폴더와 Store APK는 계속 읽기 전용으로 두었다. 무선
mDNS 작업은 다중 기기 진단 뒤, WinForms UI 개편은 맨 마지막이다.

포터블 v2.0.1에서 코드로 확인한 전송 오류 세 가지를 보강했다. 공유 저장소 루트
경로가 Download로 바뀌는 문제, 휴대폰→PC 하위 폴더에서 Windows 금지문자
치환 후 이름이 충돌하는 문제, 긴 최종 파일명 뒤에 붙는 임시 파일 접미사
문제다. 미니바 단축키는 live
Scrcpy 프로세스의 PID와 HWND 및 실제 foreground를 확인하며, 전체 창 캡처는
스크린 복사 직전에 HWND 소유 PID를 다시 확인한다. `ProcessRunner`와 관리형
전송의 출력 읽기 대기에는 2초 상한을 두었다.

포터블 설정 스키마를 26으로 올려 최근 사용자 지정 해상도 5개를 저장한다.
목록 밖 해상도가 현재 슬롯이나 앱 프로필에 활성값으로 남아 있으면 UI에 임시
선택 항목으로 복원한다. 단일창 기본 앱이 이미 선택된 상태로 `LoadRunSettings`에
복원돼도, 기기·모드 전환과 앱 목록 갱신 뒤 해당 기기의 package 프로필을 확인해
적용한다. 슬롯 값이 이미 프로필과 일치하면 중복 저장하지 않는다.

PC→휴대폰 상태창은 전송 세션의 Scrcpy PID/HWND를 통해 같은 프로세스의 창을
찾는다. HWND가 늦게 생성되면 다시 확인하며, 표시 중 타이머에서 대상 창의 위치를
따라간다. 세션 대상 창이 사라지면 마지막 위치를 유지한다.

이전 GitHub판 제작 채팅의 설계 기록을 확인한 뒤, DeX·단일창·wake-up
Scrcpy와 직접 전송 ADB의 `Process.Start()` 직전에 종료 상태를 다시 확인하고
`RequestShutdown()`과 짧은 시작 잠금으로 직렬화했다. 기존 세션 잠금과
Scrcpy 창 준비 대기에는 이 잠금을 적용하지 않는다.

.NET Framework 4.6.2 참조 어셈블리를 사용한 x64 Debug/Release 빌드와 다중 기기·
전송 회귀 52/52이 통과했다. 앱 목록 응답은 요청 당시 캡처한 물리 identity가 현재
선택 기기와 다르면 폐기한다. 진단 화면은 연결된 모든 기기의 version query를 사용하고,
보고서에도 각 Android/One UI 버전을 기록하되 기기 이름·identity·serial·IP·경로를
가린다. Windows 7/11 실제 기기의 파일 전송·프로필 적용·미니바·
상태창 위치·캡처 검증은 남아 있다. 직접 ADB 전송의 `Kill()` 실패 뒤 유한 대기와 실제 Windows
종료·취소 중 새 프로세스 차단은 추가 검증이 필요하다. 자세한 순서는
`TODO.md` 첫 절에 기록했다.

DX Companion 기준은 Android 13+ (`minSdk 33`), 2.1.2/versionCode 9다. Store에서
사용한 서명 APK를 `DXDisplayCleanup/bundled/DX-Companion-v2.1.2.apk`로 복사했다.
SHA-256 `D5B09491B2DDEE0358EC0A678994256E814A9434250650E11052A930A21D9854`,
공식 인증서 지문과 package·버전을 검사하고 PC 런타임 검증값 및 Windows/Android
패키징 스크립트를 이 APK에 고정했다. 새 서명 APK는 만들지 않는다.

포터블 Android 소스는 API 33/versionCode 9이며 Store 2.1.2의 최근 전송 이력,
연결 경과시간, Light/Dark 화면·위젯, Guardian cleanup 검증·재시도 동작을 반영했다.
Android 15+ `dataSync` timeout callback과 일반 큐 완료 후 URI 해제가 있다.
다중 요청 종료·URI grant 보강의 소스/서명 APK 구분은 위의 2026-09-30 기록을 따른다.
DXMR 프레이밍은 v1 그대로다. trim/slash 형제 이름 충돌은 기존 APK의 남은 제약이다.

포터블 PC→휴대폰 완료·실패·취소 뒤 선택 기기로 Store APK의
`RECORD_TRANSFER_HISTORY` broadcast를 보낸다. .NET Release 빌드와 회귀 테스트 52/52이
통과했다. Android 13+의 문서 제공자 전송, Guardian 복구, PC 양방향 이력은 실제 기기에서
확인해야 하며 자세한 순서는 `TODO.md`에 남겼다.

Android 11+ mDNS 코드도 구현했다. connect/pairing service parser와 wireless alias
분류를 추가하고, saved endpoint 실패 뒤 stable serial identity가 일치하는 mDNS connect
service만 선택한다. ADB 연결 뒤 phone identity를 다시 확인한 다음 host·port를 갱신하며,
페어링 직후 endpoint를 발견하지 못하면 사용자가 수동 IP·connect port를 입력할 수 있다.
복수 기기·동적 port 변경·multicast 차단의 실제 회귀 검증은 남아 있다.

아래 내용은 v2.0.1 출시 당시의 인수인계 기록이다.

## Git

- 저장소 작업 사본: 현재 Codex 작업 폴더의 `v12-docs`
- 원본 저장소: `E:\vs\dex system`
- 브랜치: `feature/v2-multi-device`
- 공개 태그: `v2.0.0` (`cec74a9`)
- 공개 태그 뒤 대표 이미지 갱신: `e8e47e6`
- 현재 작업: v2.0.1 DeX 설정 표시 회귀 수정과 GitHub 업로드 패키지 준비 완료

새 작업에서는 먼저 `docs/HANDOFF.md`를 읽는다. 공개 v2.0.0 이후 v2.0.1의 DeX
설정 표시 회귀 수정이 반영됐으며, 차기 작업은 `TODO.md`의 미완료 항목으로
관리한다. 현재 브랜치와 HEAD는 이 문서가 다시 바뀔 수 있으므로 `git status`와
`git log`로 확인한다.

## 2026-08-23 v2.0.1 DeX 설정 표시 회귀 수정

프로그램 시작 시 UI가 물리 휴대폰 identity를 알기 전에 공통 기본 설정을 먼저
표시하고, 첫 휴대폰이 기존 초기 컨텍스트를 재사용하면 기기별 설정을 다시 읽지
않는 문제를 수정했다. 첫 identity 결속 뒤 선택 기기의 DeX 해상도·DPI·비트레이트·
FPS와 실행 옵션을 다시 표시한다. 여러 휴대폰이 시작부터 연결된 경우에도 최초
선택 전 공통 기본값을 기존 기기 프로필에 저장하지 않는다.

Windows 앱 버전은 2.0.1이다. Android 소스와 프로토콜은 변경하지 않았으며 번들
DX Companion은 기존의 검증된 2.0.0(versionCode 6)을 유지한다.

최종 Release 재빌드는 경고 0, 오류 0이며 다중 기기 회귀 테스트 39개를 모두
통과했다. GitHub 업로드용 ZIP은 64개 항목이고, PDB·사용자 설정·로그·스크린샷·
서명 비밀 파일을 포함하지 않는다. 최종 산출물은 `E:\vs\dex system\dist`에
배치했다. ZIP SHA-256은
`5F9C5A6AF6199D38458F6266869DDBACC58722A3A915696FF02935CF8965B2C1`이다.

## 2026-08-22 v2.0.0 공개 및 작업 이관

DX Manager v2.0.0과 DX Companion 2.0.0을 GitHub에 공개했고, 영어·한국어
README·사용 설명서·FAQ·릴리스 설명 및 다중 기기/진단/Companion 스크린샷을
현재 기능에 맞게 갱신했다. 공개 태그 뒤 대표 이미지에 실제 DeX 아이콘이 보이는
최종 스크린샷을 반영한 커밋이 `e8e47e6`이다.

다음 작업이 긴 대화 기록 없이도 안전하게 이어지도록 현재 제품 기준점, 저장소
구조, 다중 기기·전송·Companion·Windows 종료 불변 조건, 검증 명령, 실기 확인
범위와 차기 후보를 `docs/HANDOFF.md`에 통합했다. `AGENTS.md`와 개발 문서의
“v2 준비 중” 표현도 공개 완료 상태로 바로잡았다.

이관 시점의 제품 소스는 공개 v2.0.0과 동일하다. 새로 합의된 기능 후보는 Android
11+ 무선 ADB 연결 endpoint의 mDNS 자동 발견이며, 페어링 포트와 연결 포트를
분리하고 동일 모델 복수 기기를 추측으로 선택하지 않는 상세 조건을 `TODO.md`에
기록했다. 구현은 아직 시작하지 않았다.

Windows 종료 중 `adb.exe` 네이티브 오류창이 반복되는 문제 때문에 실제 종료
경로를 일반적인 Alt+F8·트레이 종료와 완전히 분리했다. `WM_QUERYENDSESSION`을
받는 즉시 새 프로세스 실행을 차단하고, 검증된 DX Companion 2.0.0과 미리 열어 둔
기기별 인증 loopback 소켓에만 overlay 제거와 절전모드 해제 원래 값 복원 요청을
보낸다. 이 경로에서는 새 ADB 실행, reverse 제거, adb server 종료 또는 자식
프로세스 강제 종료를 하지 않으며 나머지 프로세스 수명은 Windows에 맡긴다.
Companion이 설치되지 않았거나 서명·권한·세션 검증이 완료되지 않은 휴대폰은
Windows 종료 중 정리를 건너뛴다. Alt+F8과 트레이의 정상 종료는 기존 ADB 정리와
프로세스 소유권 정리를 그대로 유지한다.

Companion 감시 소켓만 끊겨서는 정리하지 않는다. 설정된 USB 또는 Wi-Fi 연결까지
사라졌을 때만 기본 5분 유예를 시작하며 앱에서 즉시·1분·5분·10분·30분 또는 자동
정리 안 함을 선택할 수 있다. 같은 인증 세션이 다시 연결되면 예약 정리를 취소한다.
전송 방식을 판별하지 못한 경우에도 자동 정리하지 않는다. Windows 종료 명령을
받은 경우에만 유예 없이
overlay와 저장된 절전모드 해제 원래 값을 복원한다. Companion Release APK는
versionName 2.0.0, versionCode 6이며 SHA-256은
`7CD40017789E22440DCA0291AB0C45ADB564A19D8A623E669F373395536B880F`이다.

5단계 후속 보강으로 설정 창이 메인 기기 탭과 registry 변경을 실시간으로 따라가게
했다. 기기별 USB·무선 라디오 선택은 실제 연결에 따라 바뀌지 않는 강제 정책이며,
현재 감지된 transport는 별도 상태 문구로 표시한다. USB 정책은 무선을, 무선 정책은
USB를 자동 대체로 사용하지 않는다. 선택한 방식이 사라지면 해당 기기의 세션·전송·
ADB reverse를 정리하고 그 방식이 다시 나타날 때까지 연결 대기로 유지한다. 탭을
바꿔도 설정 창은 닫히지 않고 대상 휴대폰 정보와 기기별 진단을 새로 읽는다.
휴대폰→PC 전송은 공통 수신 경로 아래에 휴대폰 표시 이름 하위 폴더를 자동으로
만들어 분리 저장하며, DeX·단일창 scrcpy 제목에도 같은 표시 이름을 붙인다.
.NET Framework 4.6.2 x64 Release 빌드와 33개 다중 기기 독립 테스트가
경고·오류·실패 없이 통과했다.

v2 1단계 기준 커밋은 `3534be8`, 2단계 커밋은 `b74f370`이다. 2단계에서는 `AdbService`의 전역
대상 serial과 `ANDROID_SERIAL` 설정을 제거하고 모든 기기 명령이 명시적
serial을 받도록 전환했다. 무선 연결 서비스의 `SelectedSerial`은 기존 v1의
선택 정책만 보존하며 ADB 실행 상태로 사용하지 않는다. Release 재빌드와
다중기기 기반 독립 테스트가 경고·오류·실패 없이 통과했다.

3단계에서는 물리 기기별 런타임 레지스트리와 독립 서비스 묶음을 추가했다.
DeX, 단일창, Companion reverse·토큰, 양방향 파일 전송 큐와 화면 전원 상태가
transport serial을 통해 해당 물리 기기 런타임에만 기록된다. 각 서비스 묶음은
고유 ID로 한 물리 기기에 한 번만 결속되며 같은 휴대폰의 USB·무선 전환은 같은
결속을 유지한다.

4단계에서는 메인 화면에 물리 기기 선택 UI를 추가했다. 각 항목에는 기기 이름과
USB·Wi-Fi·연결 해제 상태가 표시되고, 탭을 선택하면 해당 물리 기기의 독립
DeX·단일창·Companion·양방향 파일 전송 서비스 묶음으로 UI가 전환된다. 비선택
기기의 Scrcpy 세션과 미니바는 유지하며, 전역 단축키·키보드 후킹만 현재 탭으로
옮긴다. USB↔무선 transport 변경 시 같은 런타임을 유지하고 이전 reverse·전송을
정리하며, 프로그램 종료 시 생성된 모든 기기 런타임을 순회한다. 화면 끄기·
절전모드 해제 상태도 현재 탭이 아니라 모든 기기 런타임을 합산한다. Release 빌드와
31개 다중 기기 독립 테스트는 통과했다. 실제 휴대폰 두 대를 연결한 실기 검증에서
한 기기의 DeX와 다른 기기의 단일창을 동시에 실행하고 탭별 상태 복원, 양방향
파일 전송, 각 기기의 개별 연결 해제 격리를 확인했다. 기기별 DeX·단일창 실행
설정과 Companion 설치·진단도 서로 공유되지 않는다. 두 기기가 연결된 상태에서
전체 정상 종료하면 두 overlay와 ADB reverse, 소유 프로세스가 모두 정리된다.

5단계에서는 설정의 무선 ADB 페이지에 대상 휴대폰 선택기를 추가했다. 기본 대상은
메인 화면에서 선택한 탭이며, 사용자는 연결된 다른 물리 휴대폰으로 바꿀 수 있다.
USB로 무선 준비할 때는 선택한 휴대폰의 정확한 USB serial만 사용하고, 그 기기에서
감지한 IP와 그 identity에 저장된 IP·포트만 사용한다. USB·무선 모드, 자동 재연결도
물리 identity별로 저장한다. 기존 전역 `Connection` 설정은 이전 설정 파일 이관과
v1 호환을 위해 남기되 신규 동작의 기준으로 공유하지 않는다. 자동 재연결은 저장된
모든 기기별 무선 프로필을 서로 다른 endpoint로 처리한다. 이후 연결 선택을 강제
정책으로 고정해 USB 분리 시 저장된 무선 주소로 자동 전환하거나 그 반대로 전환하지
않게 했다. Release 빌드와 31개 독립 테스트가 통과했으며, 다음 실기 확인 대상은
두 휴대폰의 USB·무선 정책을 서로 다르게 저장한 뒤 선택 transport만 사용하고,
분리·재연결 시 반대 transport로 넘어가지 않는지 여부다.

v1.2.0 구조 분리 전 기준은 공개 태그 `v1.1.0` (`7f7a59e`)이다.
폼 분리 커밋은 `b0c88e2`, 파일 전송 구조 분리 커밋은 `4bc1c67`이다.
별도 Cleaner UI의 비공개 변경은 로컬 브랜치
`local/cleaner-ui-private-20260726`의 `14d81ef`에만 보존되어 있으며
공개 브랜치에 합치거나 push하지 않는다.

v1.1.0 작업 전 문서 커밋은 `f9d96fa`, 복구 태그는
`pre-v1.1.0-20260721`이다. 공개 배포와 push는 사용자 실기 확인 뒤 진행한다.

새 세션에서는 실제 `git status --short --branch`와 `git log`를 다시 확인한다.
현재 작업이 커밋되지 않았다면 사용자 변경과 함께 그대로 보존한다.

## 현재 구현 상태

- Windows 11 USB/무선 DeX와 단일창 3개 동시 실행 확인
- 64비트 Windows 7 SP1/.NET Framework 4.6.2 유선 핵심 흐름 확인
- Scrcpy 4.1과 Scrcpy 폴더의 ADB를 기본 사용, 필요 시 legacy ADB fallback
- 연결 상태와 기기 이름 확인 후 실제 시작 명령 직전에 0~60초 대기
- 연결 해제/재연결 시 세션, 화면 OFF, stay-awake 정리
- DeX overlay 너비/높이/DPI 일치 시 재사용, 불일치 시 제거 후 재생성
- 정상 종료 시 관리 기기의 overlay를 생성 주체와 관계없이 제거
- SC1F2 KeyUp이 없는 환경에서도 한영 보정 반복 동작
- 일부 한국어 노트북의 `VK_HANGUL + extended scan 0x38` 한영키를 추가로
  인식하되 `VK_RMENU` AltGr는 통과시켜 브라질·유럽 특수문자 입력과 분리
- Scrcpy 4.0/SDL3에서 재현한 근거에 따라 SDL3 기반 4.x 오른쪽 Shift를
  왼쪽 Shift로 치환
- DPI 120 미만 입력 거부와 입력 확정 시 안내
- 무선/USB 전환 중 Scrcpy 종료와 자동 숨김 타이머 경합 방어
- 기본/고급 설정 재정리와 환경 점검 표 레이아웃 수정
- 제작자/GitHub 링크, MIT 라이선스, 제3자 고지와 파일 속성 완료
- README/설명서/FAQ용 한국어 10장·영어 8장 스크린샷 배치 완료
- 한국어/영어 FAQ 23문항을 독립 문서로 작성하고 README와 설명서에서 연결
- 사용자 지정 해상도 가로·세로 4096 상한과 DPI 120 하한 위반 시 이전 값 복원
- 초기화 직후 현재 선택한 모드의 실행 옵션이 다시 덮어써지지 않도록 UI 재동기화
- 공개 패키지를 `dist\DX Manager`와 버전별 x64 ZIP으로 만드는 스크립트 추가
- 자동 실행, 트레이 시작, 자동 숨김과 선택 키 보정을 끈 v1 기본값으로 정리
- 저장소 샘플 설정을 `AppSettings.CreateDefault()`와 정확히 일치시켜 개인 설정 제거
- scrcpy, ADB, SDL3, FFmpeg, libusb, dav1d, zlib과 MinGW 런타임 고지 및 라이선스 원문 포함
- GitHub 메인 README와 분리된 HTML 없는 포터블 패키지 전용 README 추가
- DeX·단일창 Scrcpy 파일·폴더 드롭 중 세션 대상 폴더 push만 처리하는
  `DXMAdbProxy.exe`와 전역 FIFO 관리형 전송 추가
- ASCII 임시 이름과 Base64 UTF-8 최종 이름 복원으로 Windows 7 SP1~11의
  한글·Unicode 파일명을 보존하고 충돌 시 `(1)`, `(2)` 접미사 사용
- 현재 항목과 다음 4개, 크기·경과 시간·완료·실패·대기 수를 보여주는 독립
  이동 상태창, 취소와 세션 종료 연동 추가(퍼센트·남은 시간은 표시하지 않음)
- 휴대폰 대상 폴더 설정, 폴더 구조·빈 폴더 보존, staging 최종 반영과
  재분석 지점 건너뛰기, 최종 이동 커밋 표식과 응답 중단 복구 추가
- 요청 registry, background ADB script 입력, 대상 폴더 snapshot과 유휴 첫
  세션의 중단 `.part` 정리로 취소·타임아웃·설정 저장·강제 분리 경합 보강
- 관리형 전송 기본값은 켜짐이며 설정에서 끄면 새 DeX·단일창부터 Scrcpy
  순정 파일 드롭 사용
- ADB 공통 `1.0.41` 문구 대신 실제 `Version ...` 빌드 값을 설정·진단에 표시
- 현재 휴대폰 확인 기준을 Android 16 / One UI 8.x로 명시하고 One UI 7.x
  이하의 검은 DeX 창 가능성을 문서화
- overlay 제거 명령을 잘못된 sentinel 문자열 저장이 아닌
  `settings delete global overlay_display_devices`로 통일
- 설정 경로 페이지의 줄바꿈 라벨이 입력칸 높이를 늘리거나 가로 스크롤을
  만들지 않도록 카드·행 레이아웃을 정리
- 진단 페이지에서 별도 Android 정리 앱의 package와 설치 `base.apk` v2 서명
  인증서를 검증한 뒤 권한을 부여하고 결과를 재확인하도록 실제 연동
- 캡처와 드롭 파일의 휴대폰 저장 폴더에 Unicode ADB 찾아보기 추가
- `DXDisplayCleanup` Android 앱 구현: 상태 확인, 설정 삭제 후 재검증, 메인
  정리 버튼, 빠른 설정 타일, 홈 위젯, 한국어/영어 UI
- Android 앱은 `WRITE_SECURE_SETTINGS`를 정리 기능에 한정해 사용하고,
  네트워크 권한은 인증된 로컬 파일 전송과 guardian 연결에만 사용하며 임의
  shell 실행은 제공하지 않는다. package ID와 공개 서명 지문을 문서화했다.
- 각 DeX·단일창 HWND/PID를 따라가는 미니 컨트롤바와 왼쪽/오른쪽 위치,
  툴팁, 접기/펴기, 활성화·최소화·앞뒤 순서 연동
- Android 패키지별 단일창 해상도·DPI·스트리밍·실행 옵션 프로필 저장,
  자동 적용, 덮어쓰기와 삭제
- 휴대폰 폴더 탐색창의 마우스 휠 라우팅과 사용자 지정 해상도 UI 보완
- Android 앱을 DX Companion으로 확장해 가상화면과 절전모드 해제를
  각각 또는 함께 정리하고, 타일·2 × 1 위젯의 정리 범위를 설정 가능

2026-08-02 v1.3.0 작업에서 DX Companion의 휴대폰→PC 파일·폴더 전송과 UI를
완성한 뒤, 서명된 APK를 포터블 ZIP에 포함하고 진단 페이지에서 현재 선택된
기기에만 설치·업데이트·재설치·권한 부여·삭제하도록 구현했다. 번들 APK의
SHA-256과 v2 인증서를 설치 전에 확인하고 설치 뒤 package·versionCode·서명과
권한을 다시 확인한다. 삭제 전 해당 serial의 수신 세션과 ADB reverse를
정리한다. .NET Framework 4.6.2 x64 Release 빌드는 경고 0, 오류 0으로 통과했다.
진단 UI의 실제 설치·업데이트·재설치·삭제는 아직 실기 확인 전이다.

Companion의 PC 수신 준비 표시는 저장된 세션 값만 신뢰하지 않고 ADB reverse를
통해 DX Manager 수신기와 토큰 인증 상태를 주기적으로 확인한다. 휴대폰 연결이
끊기거나 DX Manager가 비정상 종료되어 해제 broadcast를 받지 못해도 다음 확인
때 연결 대기로 전환하며, 공유 메뉴 전송 직전에도 같은 확인을 수행한다.

일부 한국어 노트북이 한영키를 `VK_HANGUL + extended scan 0x38`로 보고하는
경우를 추가 지원했다. `scan 0x38`만 같거나 `VK_RMENU`인 오른쪽 Alt/AltGr는
보정하지 않는다. 판정 행렬에서 전용 SC1F2와 노트북 한영키는 참, 브라질
AltGr·왼쪽 Alt·Kana형 다른 scan은 거짓임을 확인했고, .NET Framework 4.6.2
x64 Release 빌드는 경고 0, 오류 0으로 통과했다. 실제 노트북 한영키와 브라질
키보드 회귀는 해당 하드웨어에서 추가 확인이 필요하다.

`Package-Release.ps1 -SkipBuild`로 만든 개발 후보 ZIP은 59개 항목이며
`tools\companion\DX-Companion.apk`를 정확히 한 개 포함한다. PDB, settings.json,
로그·스크린샷, signing.properties, keystore와 `.gitkeep`은 포함되지 않았다.
Companion APK SHA-256은
`3876D4B7F0CCE6EC3C6CE9F930959757ED32668B3BDAE1D34F744A894039A452`,
후보 ZIP SHA-256은
`528F10E22171C4EFCE9865B6810E8CCF02B447D4D45ED4B8B0F56322008B74BA`이다.

2026-08-06 공개 후보를 다시 만들었다. DX Manager와 DXMAdbProxy는 모두
x64, .NET Framework 4.6.2, 파일 버전 1.3.0.0이며 Release 재빌드는 경고 0,
오류 0으로 통과했다. Android `testDebugUnitTest`와 `lintRelease`, 번들 APK의
v2 서명·RSA 4096·공개 인증서 지문 검증도 통과했다. ZIP은 59개 항목이고
필수 Scrcpy/ADB/proxy/Companion/문서 파일을 포함하며 PDB, settings.json,
로그, 런타임 스크린샷, signing.properties와 keystore는 포함하지 않는다.

2026-07-25 DX Manager x64 Release를 .NET Framework 4.6.2 참조 어셈블리로
재빌드해 오류 0을 확인했다. 설정창 경로/ADB와 진단 페이지를 실제 실행해
가로 스크롤 제거, 두 줄 안내 높이와 권한 카드 배치를 확인했다.

`DXDisplayCleanup`은 JDK 17, compile/target SDK 36, min SDK 24와 Gradle
8.14.5로 `testDebugUnitTest`, `lintRelease`, `assembleRelease`를 통과했다.
Release APK는 v2 서명, RSA 4096, package
`io.github.mazemei.dxdisplaycleanup`, 인증서 SHA-256
`AD615803C63760439750C36801E8152AB8664C60EE481EF1473F1DF5E80733BE`로
검증했다. 실제 Android 16 / One UI 8.x 휴대폰에서 APK 설치, 설치 APK 인증서
검증, 권한 부여 전 `Ready`와 부여 후 `Granted` 상태를 확인했다. 휴대폰 폴더
찾아보기는 `/sdcard/DCIM`에서 한글·영문 폴더를 정상 표시했다. 사용자가 앱
본체의 가상화면·절전모드 해제 개별/동시 정리, 빠른 설정 타일과 2 × 1 위젯을
실제 기기에서 확인했다. Windows 11 집 PC와 Windows 7 회사 PC에서도 v1.2.0
핵심 기능과 새 UI가 정상 동작함을 확인했다.

2026-07-22 .NET Framework 4.6.2 참조 어셈블리로 v1.1.0 x64 Debug와
Release를 경고 0, 오류 0으로 재빌드했다. 실제 Android 16 기기에
한글·Unicode 이름을 관리형 경로로 전송해 원래 이름과 크기를 확인했으며,
proxy의 일반 ADB 명령 stdout/stderr/종료 코드 전달도 확인했다.
2026-07-25 다시 만든 `DX-Manager-v1.1.0-win-x64.zip`은 57개 항목이며 PDB,
사용자 설정, 로그, 테스트 스크린샷과 `.gitkeep`이 없고 Scrcpy 4.1 및
`DXMAdbProxy.exe`가 포함된 것을 확인했다. ZIP SHA-256은
`153D6001BD89B9E0BF5BED235F656C7E08689E09A13C27E21A2BBA1A3E4259EF`이다.

Android 로컬 배포 후보 `DX-Companion-v1.1.0.apk`의 SHA-256은
`2817913CC4987EBF46805B108E23F3EDF105AEF519BAAFA69324496B687F5592`,
3개 항목 APK ZIP의 SHA-256은
`9152DCC484B0078B515D9D9267582A6ECE6FA6A88BC3ADDEAC305ADEE4016C1D`다.
Android 산출물은 아직 GitHub에 게시하지 않았다.

2026-07-17 .NET Framework 4.6.2 참조 어셈블리로 x64 Release 재빌드가
경고 0, 오류 0으로 통과했다. `DX-Manager-v1.0.0-win-x64.zip` 56개 항목을
검사해 사용자 `settings.json`, config 폴더, PDB, 로그, 테스트 스크린샷,
임시 파일과 `.gitkeep`이 포함되지 않은 것을 확인했다.

2026-07-17 GitHub 저장소를 public으로 전환하고 `v1.0.0` 태그와
`DX Manager v1.0.0` Release를 게시했다. Release 자산은
`DX-Manager-v1.0.0-win-x64.zip` 하나이며 공개 API에서 업로드 상태와 크기를
재확인했다.

2026-07-13 현재 .NET Framework 4.6.2 참조 어셈블리로 x64 Debug/Release
빌드가 모두 경고 0, 오류 0으로 통과했다.

2026-07-15 변경 후에도 같은 .NET Framework 4.6.2 참조 어셈블리로 x64
Debug/Release 재빌드가 모두 경고 0, 오류 0으로 통과했다. 패키징 스크립트로
`dist\DX Manager`와 `DX-Manager-v1.0.0-win-x64.zip`을 생성하고, PDB·런타임
설정·로그·스크린샷 및 `.gitkeep`이 포함되지 않은 것을 확인했다.

2026-07-14 연결 해제 로그에서 고정 기기가 없을 때 target serial을 비운 뒤
즉시 복구하는 1초 주기 상태 반복을 확인했다. 선택 서비스가 기기 없음
상태에서도 고정 serial을 유지하도록 수정하고 별도 복구 처리를 제거했다.
연결 해제 상태의 조용한 감시와 같은 휴대폰 재연결을 실기 재확인했다.

2026-07-14 휴대폰 두 대를 USB로 연결한 경우와 두 휴대폰의 USB/무선 ADB가
동시에 네 개의 transport로 표시되는 경우를 실기 확인했다. 처음 선택한 물리
휴대폰만 유지하고 다른 휴대폰은 무시했으며, 고정된 같은 휴대폰의 USB↔무선
전환은 정상적으로 세션을 정리하고 다시 실행했다.

## 이번 작업의 v1 기기 정책

- 앱이 처음 선택한 휴대폰을 종료까지 고정한다.
- 다른 휴대폰이 추가되거나 고정 기기가 분리되어도 다른 폰으로 전환하지 않는다.
- `ro.serialno`/`ro.boot.serialno`/Android ID가 같으면 USB와 무선 ADB 주소가
  달라도 같은 휴대폰으로 인정한다.
- 다중 휴대폰 선택과 동시 제어는 v2 후보 기능이다.

## 다음 확인

1. 두 휴대폰의 휴대폰→PC 전송과 연결 상태 전체 정상 종료 정리 실기 회귀
2. 기기 탭 폭·스크롤·연결 상태 표시를 Windows 7/11에서 시각 확인
3. Scrcpy 4.1에서 오른쪽 Shift 호환 보정 필요 여부 확인
4. 공개 Release 사용 피드백과 새 이슈 확인
5. Scrcpy 4.0/SDL3 오른쪽 Shift 재현 내용을 upstream에 보고

빌드·커밋·배포 전 `bin\Debug`, `bin\Release`의 `logs`, `screenshot` 테스트
파일을 비운다. 실기 확인하지 않은 흐름은 문서나 보고에서 확인 완료로 쓰지 않는다.

## 2026-08-09 v2 4단계 실기 검증

- Galaxy S26 Ultra와 Galaxy S20 FE를 USB로 동시에 연결해 서로 다른 물리 기기
  런타임으로 등록되는 것을 확인했다.
- S26 Ultra에서는 DeX, S20 FE에서는 단일창을 동시에 실행했다. 기기 탭을
  전환하면 각 탭이 `DeX 실행 중`, `단일창 1 실행 중` 상태로 정확히 복원됐다.
- PC→휴대폰 파일 전송이 두 기기 모두 해당 serial로 전달되는 것을 확인했다.
- 휴대폰→PC 파일 전송도 두 기기 모두 각자의 reverse·수신 토큰과 선택된 PC
  저장 폴더를 사용하며 다른 기기의 수신 세션과 섞이지 않는 것을 확인했다.
- S20 FE를 분리해도 S26 Ultra의 DeX와 화면 OFF 보조 동작이 유지됐고, 다시
  연결한 뒤 S26 Ultra를 분리해도 S20 FE의 단일창과 보조 동작이 유지됐다.
- 각 물리 기기 identity가 DeX·단일창 3개·앱 프로필·마지막 성공 설정을 따로
  저장한다. 한 기기 탭의 해상도·DPI·실행 옵션 변경이 다른 기기 탭으로 따라가지
  않으며 저장 후 재실행해도 분리가 유지된다.
- DX Companion 설치 후 상태 확인은 느린 기기도 수용하도록 최대 20초 재조회하며,
  실제 설치된 공식 package·버전·서명이 확인되면 설치 명령 시간 초과만으로 실패
  처리하지 않는다. 두 기기의 설치 전·후 진단 상태와 폰→PC 전송을 확인했다.
- S26 Ultra의 DeX와 S20 FE의 단일창을 동시에 실행한 뒤 정상 종료했다. 두 기기의
  `overlay_display_devices`는 모두 `null`, ADB reverse 목록은 모두 비어 있었고
  DX Manager·scrcpy·DXMAdbProxy 프로세스도 남지 않았다. S26 Ultra의
  `stay_on_while_plugged_in`은 실행 전 값 `0`으로 복원됐으며, S20 FE의 값 `7`은
  실행 전부터 사용자가 켜 둔 값이라 변경하지 않았다.
- 물리 연결 해제 직후 정리 명령의 `device not found`는 연결이 먼저 사라진 정상
  타이밍 경고이며, 다른 기기로 명령이 잘못 전달되거나 세션이 함께 종료된 흔적은 없다.
- 런타임 등록과 탭 선택 로그에 기기 이름·serial·USB/Wi-Fi를 함께 기록하고,
  기기별 Scrcpy·화면 OFF·전원·파일 전송 로그에는 `[serial]` 접두사를 붙였다.
  Scrcpy 서버 전송 성공 문구는 stderr로 출력되더라도 INFO로 분류한다.
- 설정상 정상 상태인 Enter 변환 비활성화 안내는 반복 WARN 대신 최초 1회 INFO로
  기록한다. 기존 단일 대상 감시 로그는 v2 전체 기기 정책과 혼동되지 않도록
  `기본 연결 감시` 범위임을 명시한다.
- 실기 로그에는 ERROR가 없었고, 연결 해제·보호 버퍼·화면 없는 화면 OFF 보조
  Scrcpy에서 발생하는 예상 경고만 확인됐다.

## 2026-08-07 v2 다중 기기 1단계

공개 v1.3.0 기준에서 `feature/v2-multi-device` 브랜치를 만들었다. 아직 기존
`DeviceMonitorService`, `AdbService.TargetSerial`, DeX·단일창 실행 흐름에는 연결하지
않고 물리 기기와 ADB transport를 분리하는 모델 및 thread-safe 레지스트리만
추가했다. 같은 identity의 USB·무선은 하나의 휴대폰으로 병합하고, 같은 모델명인
서로 다른 identity는 분리한다. 알려진 transport가 잠시 offline이 되어 identity를
조회하지 못해도 기존 관계를 유지한다.

외부 테스트 패키지가 필요 없는 `DexManager.MultiDeviceTests`를 솔루션에 추가했다.
병합·분리·transport 선택·임시 identity·상태 이벤트·transport 제거·방어적 복사·
표시 이름과 identity 보존을 포함한 11개 검증이 통과했다. .NET Framework 4.6.2
참조 어셈블리로 전체 x64 Release Rebuild도 경고 0, 오류 0으로 통과했다.

다음 단계는 이 레지스트리를 바로 UI에 노출하는 것이 아니라, 먼저 전역
`TargetSerial`과 프로세스 전역 `ANDROID_SERIAL` 의존을 제거하고 모든 기기별
명령에 대상 serial을 명시하는 작업이다.

## 2026-08-07 v2 다중 기기 2·3단계

2단계에서 `AdbService.TargetSerial`과 프로세스 전역 `ANDROID_SERIAL`을 제거했다.
기기별 ADB 명령은 빈 serial을 거부하고 항상 `-s "SERIAL"`을 사용한다. DeX,
단일창, 화면 전원, 캡처, Companion과 파일 전송도 작업 시작 또는 세션 생성 시
captured serial만 사용하도록 유지했다. 기준 커밋은 `b74f370`이다.

3단계에서 `DeviceRuntimeSessionRegistry`와 `DeviceRuntimeServiceFactory`를 추가했다.
물리 기기별 런타임 스냅샷은 DeX, 단일창 슬롯, Companion, PC↔휴대폰 전송과
화면 전원 복구 상태를 독립적으로 보존한다. 서비스 factory는 Scrcpy·DeX·단일창·
화면 OFF·가상 디스플레이·양방향 전송을 독립 묶음으로 만들고, 고유 instance ID를
물리 기기에 1:1로 결속한다. 프로세스 전체에서 공유하는 것은 대상 상태를 갖지 않는
Scrcpy 시작 직렬화기뿐이다.

## 2026-08-07 v2 다중 기기 4단계 UI

메인 화면 제목 오른쪽에 물리 기기 탭을 추가하고, 기기 이름과 현재 USB·Wi-Fi·
연결 해제 상태를 표시한다. 각 탭은 기기별 `DeviceRuntimeServiceSet`을 선택하며,
전환 시 비선택 기기의 DeX·단일창·전송·미니바는 그대로 유지한다. Windows 전역
단축키와 저수준 키보드 후킹만 선택 탭으로 이동한다.

물리 기기 snapshot 변경으로 각 기기의 서비스 묶음을 생성·결속하고, 같은 identity의
USB↔무선 전환은 기존 묶음을 유지한다. 이전 transport의 Companion reverse와 전송은
정리한 뒤 새 serial로 다시 연결한다. 연결 해제는 해당 기기 큐와 세션만 정리하며,
앱 종료는 생성된 모든 컨텍스트를 순회한다. 비선택 기기에서 전송 이벤트가 발생해도
이벤트를 만든 coordinator/receiver 기준으로 상태창을 연결한다.

.NET Framework 4.6.2 x64 Release 빌드는 경고 0·오류 0으로 통과했고, 두 기기
서비스 결속과 USB→Wi-Fi 전환 격리를 추가한 총 23개 테스트가 통과했다. 휴대폰 두
대를 이용한 실제 동시 실행·전송·분리·종료 검증은 아직 남아 있다.

## 2026-08-09 v2 자동 시작과 단일 기기 탭 정리

기존 자동 시작은 현재 선택 컨텍스트만 `StartDexAsync()`를 호출하도록 제한되어 있어
두 휴대폰이 함께 발견되면 첫 번째 선택 기기만 시작됐다. 이를 기기별 저장 설정과
`DeviceRuntimeServiceSet.Dex`를 직접 사용하는 자동 시작 경로로 분리했다. 프로그램
시작 전에 연결된 기기와 실행 중 새로 연결된 기기 모두 대상이며, 연결 세대와 serial을
재확인해 대기 중 분리·transport 변경으로 오래된 시작 작업이 실행되지 않게 했다.

기기 탭은 현재 실행에서 물리 기기 두 대 이상이 확인되기 전까지 숨긴다. 두 번째
기기가 연결되면 나타나고 이후 한 기기가 분리돼도 재연결 대상과 세션 상태를 보여주기
위해 프로그램 종료까지 유지한다. 재실행 시 한 대만 연결돼 있으면 다시 숨겨진다.

.NET Framework 4.6.2 x64 Release 본체는 컴파일 오류 없이 빌드됐고 31개 다중
기기 회귀 테스트가 통과했다. Rebuild 정리 단계에서 연결 중인 ADB DLL 3개가 사용
중이라 삭제되지 않았다는 경고가 있었지만 C# 컴파일과 실행 파일 생성은 완료됐다.

## 2026-08-11 다중 기기 사이드바와 표시 순서

기기 선택 UI를 제목 오른쪽의 가로 탭에서 왼쪽 사이드바의 세로 영역으로 옮겼다.
한 대만 사용하는 실행에서는 영역 전체를 숨기고, 두 번째 기기가 확인되면 연결
방식과 상태를 포함한 두 줄 항목으로 표시한다. 세 대 이상은 제한된 영역 안에서
스크롤하며, 비선택 기기의 실행 세션은 기존과 같이 유지한다.

프로그램 시작 시 이미 여러 휴대폰이 연결돼 있으면 Galaxy 모델 세대를 기준으로
최신 기기를 위에 배치하고 초기 선택도 그 순서를 따른다. 실행 중 휴대폰이 차례로
연결되면 먼저 연결된 휴대폰의 위치를 유지하고 새 휴대폰을 아래에 추가한다. serial
문자열 순서에 의한 UI 흔들림을 막는 표시 순서 모델과 회귀 테스트를 추가했다.

연결된 snapshot에서 확인한 표시 이름은 기기 UI 컨텍스트에도 별도로 보존한다.
따라서 실행 중 휴대폰이 분리되어 현재 `PhysicalDeviceInfo`가 사라져도 사이드바는
identity나 serial 대신 마지막으로 확인한 휴대폰 이름을 계속 표시한다.

.NET Framework 4.6.2 x64 Release 빌드와 33개 다중 기기 회귀 테스트가 경고·오류·
실패 없이 통과했다.

## 2026-08-11 Windows 종료 ADB 정리

사용자가 프로그램 메뉴로 종료하는 정상 경로와 Windows 세션 종료 경로를 분리했다.
정상 종료는 각 휴대폰의 화면 전원·절전모드 해제·overlay·ADB reverse를 복원한 뒤
`adb kill-server`를 실행하고, 마지막에 새 프로세스 실행을 차단한다.

회사 Windows 7 실기에서 오류 대화상자가 약 10회에서 2~3회로 감소했지만 완전히
사라지지 않았고, 빠른 종료 경로에서는 overlay와 `절전모드 해제`도 복원되지 않는다는
결과가 확인됐다. Windows 종료 또는 작업 관리자 종료도 먼저 런타임과 기기 감시의
신규 작업을 차단한 뒤 최대 5초 동안 일반 종료의 기기별 정리를 실행하도록 수정했다.
정리가 끝나거나 제한 시간을 넘으면 전역 프로세스 종료 gate를 닫고, 현재 선택된 ADB
실행 파일과 절대 경로가 정확히 같은 `adb.exe`만 종료한다. 다른 경로의 Android
Studio·시스템 ADB는 종료하지 않는다. 또한 자식 프로세스에 상속되는 Windows 오류
모드로 세션 종료 중 네이티브 ADB 오류 대화상자가 반복 표시되지 않게 했다.

새 프로세스 차단과 실행 중 프로세스 취소 회귀 테스트를 추가했다. .NET Framework
4.6.2 x64 Release 빌드는 경고 0·오류 0이며 총 35개 다중 기기 테스트가 통과했다.
실제 Windows 7 종료 중 네이티브 ADB 오류 창이 사라지고 overlay·`절전모드 해제`가
복원되는지는 회사 PC에서 다시 실기 확인한다.

일반적인 정상 종료 뒤에도 분리된 ADB 서버가 남아 설치 폴더 삭제를 막는 현상을
추가로 보완했다. `Application.Run()` 반환 후 선택 ADB, 번들 scrcpy·동봉 ADB,
`DXMAdbProxy`의 절대 경로가 일치하는 잔존 프로세스만 두 번 확인해 종료한다. 이름은
같지만 다른 폴더에서 실행한 프로세스를 보존하는 회귀 테스트를 추가했고, Release
빌드와 총 36개 다중 기기 테스트가 경고·오류·실패 없이 통과했다.

실제 Windows를 반복해서 종료하지 않고 경로를 확인하려고 RC 단계에서 설정의 진단
페이지에 넣었던 `Windows 종료 정리 테스트`는 실제 종료와 완전히 같을 수 없고 일반
사용자에게 필요하지 않아 최종 UI에서 제거했다. 실제 종료 경로는 새 프로세스를 막고
인증된 Companion 연결만 사용하는 구조를 유지한다.

## 2026-08-09 전체 구조 감사와 저위험 분리

v2 다중 기기 기능 구현 뒤 소스 크기, 비동기 진입점, 빈 예외 처리, 전역 mutable
상태와 역할 중복을 다시 확인했다. UI 테마와 언어의 정적 상태는 프로세스 전역 UI
정책이고, 확인된 비이벤트 `async void` 진입점은 내부에서 예외를 기록하므로 이번
작업에서 동작을 바꾸지 않았다. 파일 전송과 프로세스 drain의 빈 catch도 종료·취소
중 발생하는 예외를 의도적으로 무시하는 경로임을 확인했다.

`MainForm.Devices`에서 snapshot 조정과 transport 전환·자동 시작을
`MainForm.DeviceConnections`로 옮기고 연결 시각·분리 표식·시작 대기 보조 로직은
`MainForm.DeviceConnectionState`로 분리했다. 구독되지 않던 예전 단일 기기
DeviceConnected/DeviceDisconnected 처리기와 전환 보조 코드는 제거했다.
`AppSettings`의 직렬화 DTO·열거형은 `AppSettings.Types`로 옮겼고, 커스텀 입력
컨트롤은 기본 입력, 숫자·단축키 입력, 드롭다운 팝업의 세 파일로 나눴다.

## 2026-08-18 v2.0.0 공개 후보

다중 기기 기능과 Windows 11·Windows 7 실기 확인을 바탕으로 Windows 앱 버전을
2.0.0으로 확정했다. 물리 기기별 독립 DeX·단일창·설정·USB/Wi-Fi 정책·Companion·
양방향 전송, 한 기기 연결 시 선택 영역 숨김, 기기 표시 이름 보존과 scrcpy 제목
표시를 사용자 문서에 반영한다.

DX Companion은 연결 손실 자동 정리 시간을 즉시·1분·5분·10분·30분·자동 정리
안 함 순서로 정리하고 기본값을 5분으로 변경한 2.0.0(versionCode 6)이다. 번들
검증 SHA-256은
`7CD40017789E22440DCA0291AB0C45ADB564A19D8A623E669F373395536B880F`이다.

최종 v2.0.0 진단 페이지에는 현재 선택된 휴대폰의 모델, 연결 방식, Android,
SDK, One UI와 보안 패치를 조회하는 기기별 버전 진단을 추가했다. 호환성 결과는
실행을 막지 않는 참고 정보다. 같은 페이지에서 환경·선택 기기 런타임·연결 기기
요약·최근 경고와 오류를 serial, IP, 토큰과 사용자 경로를 가린 텍스트 보고서로
저장할 수 있다. RC 검증용 Windows 종료 모의 테스트는 최종 UI에서 제거했다.

최종 Windows x64/.NET Framework 4.6.2 Release 빌드는 경고 0·오류 0으로
통과했고 다중 기기 회귀 테스트 39개가 모두 통과했다. DX Companion은 Android
단위 테스트 7개, `lintRelease`, v2 서명과 RSA 4096 인증서 검증을 통과했다.
공개 후보 ZIP은 파일 55개와 폴더 4개로 구성되며 PDB·로그·스크린샷·사용자
설정·서명키를 포함하지 않는다.
패키징 스크립트는 로컬 .NET 4.6.2 targeting pack이 없는 환경에서도 명시적인
`-TargetFrameworkRootPath` 또는 `DXM_TARGET_FRAMEWORK_ROOT`를 사용할 수 있게
보강했고, 해당 경로를 이용한 전체 빌드·패키징까지 재검증했다.

공개 후보 실기 실행 중 여러 저장 요청이 같은 `settings.json.tmp`를 공유해
임시 파일이 먼저 이동될 수 있는 경합을 확인했다. 설정 저장을 고유 임시 파일과
프로세스 간 파일 잠금으로 직렬화하고, 8개 독립 서비스의 동시 저장을 검증하는
39번째 회귀 테스트를 추가했다. 재패키징한
`DX-Manager-v2.0.0-win-x64.zip`은 파일 55개·폴더 4개이며
SHA-256은
`D874021B8C3AC0B4DA7C69CBDFB4492DDE197426F954C8EB639796F26A287EBE`이다.

최종 RC2는 Windows 11 실기에서 다중 기기 연결·실행·전송과 실제 Windows 종료를
반복 확인했고 별도 문제를 재현하지 못했다. 공개 UI에 있던 Windows 종료 모의
테스트는 제거했으며, 기기별 버전 진단과 개인정보를 가린 진단 보고서 저장 기능을
포함한 상태로 2.0.0 배포 후보를 확정했다.

실기 생성한 두 기기의 진단 보고서와 전체 로그를 대조해 기기별 버전·런타임·전송
상태가 올바르게 분리되는 것을 확인했다. 공개 공유 안전성을 위해 기기 표시 이름과
로컬 절대 경로도 가리고, ADB 버전에는 버전 번호만 기록한다. PC→휴대폰 상태는 실제
의미에 맞게 활성 전송 세션 수와 대기 항목 수를 별도 행으로 표시한다.

Windows Release 빌드 출력 폴더에 이전 Companion APK가 남아 공식 RC2 해시와
불일치하면서 재설치가 안전하게 차단되는 상황을 확인했다. 일반 Debug·Release
빌드 뒤에도 Android Release 출력의 최신 서명 APK를 `tools\companion`에 자동
동기화하고, 원본이 없으면 오래된 출력 APK를 제거하도록 빌드 대상을 추가했다.

이 상태를 DX Manager 2.0.0 공개 릴리스 기준점으로 확정한다. 이후 발견되는 버그와
개선 사항은 2.0.0 릴리스 내용에 섞지 않고 차기 버전 변경으로 관리한다. 정식
릴리스 패키지의 생성·확인·전달 위치는 `E:\vs\dex system\dist`이며, C 드라이브의
Codex 작업 폴더 산출물은 개발 및 검증용으로만 사용한다.

## 2026-10-01 Follow-up UI corrections

General text is now +1 DIP over the Store source sizes (the earlier +2 adjustment was reduced).
Enabled labels have a measured safe width. Setting card widths follow each page's actual client
width after scrollbar changes. The content viewport has an additional 12 DIP of footer clearance.
Device diagnostics use a right-aligned 160x40 Refresh button in the header; the summary's old
58-pixel minimum was reduced to 22, bringing the phone cards up. Help topic cards are 74 DIP
high and fit the default window without vertical scrolling.
The log buttons overlapped the description label by approximately five pixels. Their toolbar
was moved up and brought to the front, and explicit button bounds replaced the flow layout.
RichEdit keeps its native scrolling and hit testing while its non-client gutters are painted
with the current theme. The scrollbar painter was checked using real control scroll metrics.
Panel scrollbars cover the whole native gutter instead of repeatedly hiding/restoring it.
The background bitmap is cached by size/theme, and child redraw composition is enabled on
buffered panels using the Windows 7-compatible style. Actual perceived scrolling smoothness
and removal of the user's live-screen white strip still require a fresh runtime check.
Advanced options use a separate gray placeholder (examples: --no-audio --video-codec=h264).
The Text value remains empty; the placeholder cannot enter saved settings or launch arguments.
Guide/FAQ scroll offsets are now explicitly retained when switching tabs.
The isolated harness passed label/menu/button geometry, non-overlap, placeholder separation,
independent document scrolling, settings persistence and diagnostic rendering. New screenshots
are under .build-tools/ui-review/20261001-201242. They use fixture phone data.
## 2026-10-02 Settings editor geometry and consistent scrollbars

The user's live screenshots exposed a regression in StoreSettingCard.Reflow: the stacked branch
permanently overwrote every editor width, including toggles. Wider layouts did not restore their
source dimensions. Each editor's intended rectangle is now registered before adding it to the
host and restored during reflow. Only wide diagnostic flow content is stretched. The earlier
preview checks omitted the narrow-to-wide layout transition; they were insufficient UI evidence.
Wheel scrolling updated AutoScrollPosition without refreshing the custom thumb. Panel wheel
paths now refresh it, and native window observations capture other programmatic changes.
Scrollbar positioning uses WinForms ScrollProperties.Maximum-LargeChange+1 rather than
DisplayRectangle.Height-ClientSize.Height, which omitted 16 pixels of bottom padding in the
reproduced settings case (205 instead of 221). End-of-scroll checks confirm the last card fits.
A single stable thin scrollbar now covers panel and RichEdit gutters. Native log scrollbar
repainting was removed, eliminating two painters that could show different hover appearances.
Both axes, empty horizontal gutters, help/docs, environment checks, notices and license viewer
use the shared painter. Dropdown indicators use the same thin rounded thumb and theme color.
Settings content ends at the footer boundary; log buttons and description have matching bottom
bounds without overlapping. Help topic text is centered as a group; icons are 22 px and text
starts at x44. Advanced arguments moved up 6 DIP. Its preview surface starts in front of the
native editor; a native cue banner also handles the empty focused editor without changing Text.
The cue API's Vista+ support and focus behavior were checked in Microsoft documentation:
https://learn.microsoft.com/en-us/windows/win32/controls/em-setcuebanner
Validation: final Release-linked harness includes editor shrink/restore, visible toggles, log
scroll offset changes, thumb synchronization, placeholder separation, and end-of-scroll bounds.
The 20261002-011420 folder records the exact tested executable SHA-256 in verified-build.txt.
Computer-use captured an actual desktop fixture showing restored controls and a dark thin gutter.
The fixture closed before injected wheel testing; no successful real wheel gesture is claimed.
Offscreen DrawToBitmap still renders native gutters incorrectly and is not live-screen proof.
Windows 7, non-96 DPI, and connected-phone asynchronous refresh remain user validation items.
No Store worktree edits, commit, push, tag or distribution packaging were performed in this task.
## 2026-10-02 Single-window preflight and shared rendering polish

Single-window app selection is checked before ApplyRunSettings, operation-state changes,
device delay, managed-screen tracking and the try/finally wake schedule. Missing selection
shows a persistent notice and returns immediately. DeX remains app-optional. The primary
button uses an ExecutionBlocked paint state rather than Enabled=false for this condition,
retaining click feedback while preventing actual start work. App selection restores styling.
The isolated harness checked immediate task completion, unchanged log entries and wake-timer
state, DeX's optional app selection, and gray clickable styling with selection recovery.
Rounded panels and buttons no longer use binary rounded window regions that cut antialiased
edge pixels. They repaint their actual parent surface and use inset contours. Shared scrollbar
layers repaint their parent surface at the same coordinates rather than a flat color strip.
Error notices use a neutral dark rose / pale light rose palette, a matching border and opacity
230/255; labels preserve the painted background. Native title bars remain unchanged.
The two bundled executable paths now share the storage-folder field's starting edge and use
496-pixel fields. Diagnostic body layout owns its current width/height and handles empty,
one-phone and multiple-phone results without the fixed snapshot/minimum-height regression.
Environment result labels can use a full-width measured body. Companion bundle/install rows
are removed from presentation; verification logic remains, and status/refresh are consolidated.
Dark-theme mail links use a readable lavender color.
Release-linked UI harness passes the above checks plus previous settings/scroll tests. Debug
and Release regression suites each pass 59. Preview artifacts are immutable per timestamp;
20261002-201154/verified-build.txt records the exact executable hash. Notice and scrollbar
layers are separately rendered to compensate for DrawToBitmap child-order/native-gutter limits.
These are rendered control previews with fixture device data, not live phone test evidence.
Computer-use live capture was attempted but app access approval timed out. Windows 7, other
DPI, live device refresh and real single-window/DeX launching require user validation.
No Store worktree edit, commit, push, tag, GitHub upload or release packaging was performed.
## 2026-10-02 Resolution selection and stable settings width

The custom editor action now follows the three fixed presets, with a separator and saved
custom dimensions underneath. Accepting the editor stores the dimensions in memory and selects
the matching numeric entry immediately. Recent numeric entries retain custom-resolution semantics
for persistence; cancel restores the previous dimensions. Mode-switch persistence was checked.
The topmost dropdown closes before dispatching selection. Its deferred focus callback skips
disabled owners, so it cannot steal focus from a modal editor. The editor activates on Shown,
selects the width number initially and retains select-all on a click in either numeric field.
Settings reserve the native scrollbar gutter even when no scrollbar is present. The viewport
extends to the right edge while cards retain one stable width derived from outer viewport width.
Close-button right bounds match card right bounds. Expansion no longer changes card widths.
The final Release-linked harness passed ordered resolution entries, immediate numeric display,
mode-switch persistence, initial/click numeric selection, invariant expansion width, nested child
right bounds and footer alignment, plus previous checks. Debug/Release regressions each passed 59.
Control-rendered snapshots: .build-tools/ui-review/20261002-205452. Real desktop modal activation,
other DPI and Windows 7 remain user validation items. No Store edit or Git publication occurred.
## 2026-10-02 Custom resolution heading versus editor action

Clarified by the user: after 4K, show a non-selectable "사용자 지정 해상도" heading;
saved numeric custom entries follow; the final normal menu entry "사용자 지정..." opens
the editor. The heading and command are distinct records. Keyboard arrows skip headings;
mouse hover/click and committed selection cannot choose a heading. The numeric selected
resolution still updates immediately and persists across mode changes.
Release build, 59 regression tests, and the extended UI harness pass. Snapshot:
.build-tools/ui-review/20261002-210817/resolution-order.png. No extra button control was added.
## 2026-10-03 Main notice state, status copy and review onboarding

The theme-refresh path still reapplied the old opaque notice label and green error border.
It now uses the same ApplyDesktopNoticeTheme method as initial display. Notice corners restore
the underlying app-card surface, including its parent gradient, rather than the form background
alone. Missing-app notices carry their own reason, originating slot and physical identity;
selecting an app in that context closes this notice while unrelated errors remain persistent.
Idle authorized DeX says "DeX 실행 준비됨". Single-window slots distinguish "준비됨" with an
app and "대기중" without one. Actual app selection refreshes the indicator. Running, failed,
offline and unauthorized states keep their existing handling.
The display/options/start-app descriptions are hidden and section labels use a fixed-height,
vertically centered box matching their icon's center; AutoSize cannot override that alignment.
PortableOnboardingService.RepeatNoticeForUiReview is enabled at the user's request. Startup shows
the consent notice on every process launch until the user requests otherwise, without deleting
acknowledgement or general settings. A lifecycle test verifies repeated display after persisted
acceptance and unchanged acknowledgement data. This is a review-build preference, not a new UI
setting; it must not be silently disabled by later tasks.
The user's confirmed Galaxy S26 Ultra / SDK37 (Android17) / OneUI9.0 environment has a separate
UserVerifiedEnvironment informational classification. Matching is scoped to that display name,
SDK and version; other models and SDK/OneUI combinations remain unverified. This changes no
launch or permission gates and relies on the user's reported physical test.
Verification: .NET4.6.2 x64 Debug/Release, 60 regressions each. The final executable-linked
UI harness passed notice transparency after theme changes, corner pixels against the actual
underlying card, targeted dismissal versus unrelated errors, exact idle status copy, section
alignment, and scoped compatibility matching. Previews: 20261003-011841; verified-build.txt
records the executable hash. These are rendered fixtures, not fresh physical-phone evidence.
No Store edits, APK changes, commits, push, tag or release packaging were performed.
## 2026-10-05 USB launch priority and Companion 2.1.4

User authorized both changes in the existing portable v2.0.1 backport. No Store code/package or submission was changed.
Observed authorized USB/Wi-Fi transports now determine the main badge: USB, Wi-Fi, or USB + Wi-Fi, with independent vector icons and measured text width. Dark icon contrast follows theme updates.
New manual DeX/single-window launches select authorized USB first, then Wi-Fi for that physical identity. Wireless profiles still configure endpoints/reconnect. Existing sessions retain captured serials when USB appears; launch-in-progress contexts are pinned too.
Transport loss stops only sessions captured on that serial through serialized service methods. USB loss cannot trigger automatic Wi-Fi restart, including deferred cleanup with AutoStart enabled. A new explicit start clears the manual-start requirement. All observed authorized endpoints update availability without resetting their start-delay clock on every registry refresh; reconnected USB can be used while another Wi-Fi session remains running.
Companion sibling segments are allocated before traversal. Trim/slash collisions get numbered names, existing natural suffix names are reserved, and directories propagate their unique path to descendants. Original URIs, content, top-level root IDs and DXMR v1 are unchanged.
Signed APK: 2.1.4/versionCode11, minSdk33, package io.github.mazemei.dxdisplaycleanup, SHA256 95DAF4E734FD36FE07AD8388FE8E4C98D96D1EFA0A8E359D8C8E3E27383B6536. Certificate unchanged: AD615803C63760439750C36801E8152AB8664C60EE481EF1473F1DF5E80733BE.
Fresh checks: Android20 tests, Release lint/assemble, package identity/permissions/certificate/hash; .NET4.6.2 x64 Release and62 foundation tests; independent connection UI/service harness.
Final fixture previews: .build-tools/ui-review/20261005-201135. Harness uses the actual compiled controls, blocks phone commands, and starts owned sleeping helper processes to check DeX/single USB termination, Wi-Fi preservation and automatic restart suppression. This is not real-phone evidence.
The older broad UI harness stopped at its independent-input-mode fixture assertion; it was not counted as passing. The scoped connection harness above passed. Real devices, document providers, Windows7 and high DPI still need field checks.
Store follow-up is documented in STORE_COMPANION_FOLLOWUP_2026-10-05.md for a later update. No automatic APK install, commit, push, tag or GitHub Release was performed. RepeatNoticeForUiReview remains enabled.

## 2026-10-05 reconnect auto-start correction

The user supplied a fresh log: startup automatic USB DeX succeeded at20:32:20; after disconnect, USB returned at20:32:42 and deferred cleanup ran, but no automatic launch followed. The previous portable patch left RequireManualStartAfterTransportLoss sticky across reconnections; that was a regression introduced by this chat.
Suppression now records its lost transport serial and lasts only while the same connection episode retains a fallback transport. A restored authorized original serial or a newly connected physical context clears it. Immediate USB-loss -> remaining Wi-Fi auto-start remains blocked. USB reconnect and full disconnect/reconnect resume the existing auto-start preference; OFF remains OFF.
Automatic launch now captures SelectLaunchTransport, with its own identity/generation/authorized-serial validation, so USB is preferred even if another existing Wi-Fi session keeps the runtime context pinned to Wi-Fi. Session cleanup/presentation still uses the captured/context transport.
Wi-Fi vector control changed from18x20 to22x18 DIP, with matching label spacing; USB and combined badges remain measured and tested in light/dark.
Verification: .NET4.6.2 x64 Release,62 foundation regressions, and the scoped reconnect fixture. The fixture terminates owned USB DeX/single helpers while preserving Wi-Fi; checks offline USB cannot resume; reaches the auto-launch gate on restored USB and a fresh Wi-Fi connection; and confirms auto-start OFF. Phone commands remain blocked, so this is control/state/owned-process evidence, not physical DeX confirmation.
Final previews: .build-tools/ui-review/20261005-204533, with verified-build.txt and verification.txt. Previous preview folders remain unchanged. Actual reconnect execution on the user's phone is still pending. APK2.1.4, repeat review consent, Store files and Git history are unchanged.
## 2026-10-05 Fluent icon preview and mini-bar connection status

User authorized a portable icon trial, with Store changes conditional on later acceptance. The prior reconnect fix was confirmed working by the user before this icon request.
31 Microsoft Regular assets from pinned source a563cf9166f4f91aa617557ed272612b7f0a2f72 are embedded as transparent256px masks. Original SVGs/provenance and MIT license are retained. Runtime rendering uses only .NET4.6.2/System.Drawing and current theme tint. Product logo and original large phone illustration remain intact. Manual small-icon renderer is retained as a fallback, with internal comparison flag; original affected source snapshots: .build-tools/fluent-apply-20261005-212848/before.
The official System Icons catalogue has Keyboard Mouse but no standalone physical Mouse. The Mouse resource retains the two official mouse contours from Keyboard Mouse20Regular, with a centered viewBox; the original combined SVG is preserved. This is a documented derivative, not a newly drawn contour or a claim of an official standalone Mouse glyph.
A forwarded human request added mini-bar connection indicators. Each per-device MiniControlBarManager receives the registry's physical snapshot and updates its own existing forms, and new bars inherit cached observed authorized USB/Wi-Fi flags. Expanded bars show USB, Wi-Fi or the pair; collapsed44px behavior and command target handles remain unchanged. Disconnected/unauthorized transports are not shown as connected. The command buttons also use Fluent assets with their prior actions retained.
Fresh Release build,62 foundation regressions, all31 embedded-resource/tint/transparency checks, all settings pages/help and light/dark renders passed. Owned-process reconnect regression and mini-bar status changes, unauthorized/disconnect, collapsed size and per-manager isolation passed. These fixtures block physical phone commands.
Final valid previews: .build-tools/ui-review/20261005-215353;44 PNGs were decoded/verified. Earlier trial directories with failed/empty file exports are not final previews. This environment's direct GDI disk export produced empty files without surfacing an encoder error, so the final harness encoded to memory and exported bytes through PowerShell. Production icon resources are separately valid PNGs.
Live mini-bar following and connection icons on the user's real sessions remain a field check. Windows7/high-DPI confirmation remains pending. Existing session/autostart/ADB behavior, APK2.1.4 and repeated consent remain unchanged. No Store code edits, phone APK install, commit, push, tag or GitHub release.
The final icon preview also caught hidden-form badge layout using Control.Visible (which includes parent visibility). Header geometry now uses explicit observed-transport flags, so tray-hidden connection updates do not overlap the icon and label when shown again. A hidden/show regression covers USB/Wi-Fi/both in both themes. Auto-start fixture assertions count relevant start markers rather than unrelated asynchronous cleanup log entries.

## 2026-10-05 Store 통합 인계 문서화

사용자 요청으로 STORE_UPDATE_HANDOFF_2026-10-05.md를 작성해 Store docs에도 동일하게 보관하고 README/TODO/SESSION에서 찾을 수 있게 했다. APK2.1.4/2.1.3 수정, Fluent31종/파생 마우스, 미니바 및 최종 실행/재연결 요구와 UX 비교 항목을 포함한다. Store 제품 코드·APK·제출물은 이번 문서 작업에서 변경하지 않았다.

## 2026-10-05 v2.5.0 final local release

The user reported completing physical validation successfully and requested the final version2.5.0 and a local Git commit. This acceptance covers the tested user environment; no claim is added for every OS/DPI or an unreproduced ADB Kill failure.
First-run review repetition is now disabled; CurrentNoticeVersion2 and existing config/onboarding.json are retained. Fresh/unaccepted profiles show consent; accepted profiles skip it on later launches, and general settings reset does not delete acknowledgement. The lifecycle regression was updated accordingly. Windows Assembly/File version is2.5.0.0, ProductVersion2.5.0; Companion remains the unchanged officially signed2.1.4/code11.
Windows builds now take the pinned bundled APK, so a fresh source checkout does not require local Android build output or signing secrets to produce the verified Windows payload. Older local signed APKs remain on disk but are ignored; only the current2.1.4 bundle is selected for Git. Icon provenance uses relative SVG paths.
Package-Release.ps1 supports explicit output/folder paths and no longer clears source-runtime user logs/captures. The final package is made from explicit payload files, excludes personal config/onboarding/logs/PDB/signing secrets, and preserves older release folders.
Release62 tests passed, including persistent first-run consent. Package version and ZIP-entry executable/APK hashes match;63 payload files and required Scrcpy/legacy ADB/proxy/licenses are verified. User virus scan and explicit instruction are required before GitHub upload. No push, tag, external upload or publication has been performed.
Final folder: E:\vs\dex system\dist\DX Manager v2.5.0
Final ZIP: E:\vs\dex system\dist\DX-Manager-v2.5.0-win-x64.zip
ZIP SHA256: B2064404BB45B65FA863558C82898C5B1BFFAC46CB7035BA5E55CEA2892765E0
EXE SHA256: BACEA6027137EC0ACE770133034F258EF36F68CA977FD03FF9395F58717D3632
APK SHA256: 95DAF4E734FD36FE07AD8388FE8E4C98D96D1EFA0A8E359D8C8E3E27383B6536
Checksums: E:\vs\dex system\dist\DX-Manager-v2.5.0-SHA256.txt
Local commit is the final action of this task; its hash is recorded by Git rather than embedded into its own commit.

## 2026-10-06 scrcpy5.0 company test

메이님 요청과 회사 Windows7 확인에 따라 독립 테스트 ZIP을 만들었다. 기존2.5.0 실행 파일/최종 ZIP과 Companion2.1.4는 해시가 유지된다. 01-software/02-auto는 디코딩 옵션만 다른 기본값이며 레거시 ADB를 포함한다. 공식5.0 파일 일치·프리셋 상속·Windows11 버전/도움말 검사·135개 ZIP 파일/개인정보 제외를 확인했다. 실제Win7/휴대폰 스트리밍 검증은 회사 확인을 기다린다. 상세 경로/해시는 SCRCPY_5_TEST_2026-10-06.md. 이번 테스트는 새 커밋/업로드 없이 준비했다.

## 2026-10-06 independent font lab

회사Win7 SP1/FHD/100%에서 메인·설정·도움말 글자 획이 깨지고 로그 본문만 정상이라는 사용자 보고가 있다. 원인은 미확정이며 실제 회사 화면 대신 그림판 예시를 받았다. 본체 재빌드 대신 독립 DXFontLab.exe를 요청받아 현재 Pretendard/GDI, 맑은 고딕/GDI, 정수 픽셀 크기, GDI+ 두 방식, 패널 합성 제외를 비교하도록 준비했다. 메인/도움말 크기·실제 페인팅 코드·글꼴/아이콘 리소스를 재사용한 샘플이며 휴대폰 명령/설정은 없다. Windows11에서36조합/실제 글꼴 선택/전환/캡처 및37 PNG 검증을 통과했다. 회사Win7 결과 대기. 결과는 임시 기록 후 복사하며 종료 전에 메모장에 저장한다. 상세: FONT_LAB_2026-10-06.md. 기존2.5.0 EXE/ZIP 해시, Store/APK와 제품 소스는 보존했고 커밋/업로드하지 않았다.

## 2026-10-07 second font test

회사Win7 실제 기록/캡처에서 맑은 고딕 B는 정상, 현재 Pretendard를 쓴 A/C/D/E/F는 깨짐으로 보고됐다.96DPI·ClearType·GDI 등록/글꼴 선택은 정상이며 작은 GDI+ 회색 AA가 특히 나쁘고 큰 글자는 회복된다. 내장 및 공식 정적 Pretendard 모두 힌팅/gasp가 없으므로 변환 중 유실로 단정하지 않는다. 사용자 요청으로 Font Lab2를 만들었다: 현재/공식 원본, gasp0x000A 표시 설정 보정, 보정 파일의 GDI+ 회색 AA, 현재 파일의 GDI+ AntiAlias(보정 제외), 맑은 고딕 기준. 수정 글꼴은 OFL 이름 변경과 원형/레이아웃 byte 보존 및 체크섬 검증을 수행했다. Windows11에서7조건42화면/49 PNG·실제 글꼴 선택 검증, ZIP23파일/8EXE 해시 확인. 본체/Store/APK/최종2.5.0은 그대로이며 Win7 품질은 내일 확인할 항목이다. 경로/해시/사용법: FONT_LAB2_2026-10-07.md. 커밋/업로드 없음.

## 2026-10-08 F2 product UI preview

사용자 회사 결과: A2~D2 깨짐, E2/F2/G2 정상이며 F2의 UI를 선호했다. 승인받아 실제 포터블 UI의 라벨/커스텀 텍스트 표시·측정을 F2 AntiAlias로 연결했다. 실행 버튼은 Store17px SemiBold/20px 아이콘/10px 간격을 한 그룹으로 중앙 정렬한다. 도움말 강조 상자는 내부 기본100px 높이를 제거해 검증된 한글/영문 주제의 아래 여백12px로 맞췄다. Release/62회귀/실제 UI53 PNG·숫자 편집·경로 선택/undo·드롭다운 검증 통과. Native TextBox 편집과 RichTextBox 본문은 Windows 렌더링을 유지하므로 통합 Win7/IME 품질은 추가 실기 확인 대상이다. 별도 F2 프리뷰를 만들고 최종2.5.0/Store/APK는 보존했다. Scrcpy는 기존4.1이며5.0 테스트는 분리돼 있다. 상세 경로/해시: F2_UI_PREVIEW_2026-10-08.md. 커밋/업로드 없음.
## 2026-10-08 fixed preview / contrast / authorized dist cleanup

메이님 지시대로 미니바는 제외하고 메인/설정 아이콘20px, 캡처/Wifi 매핑, Store 수준 글자 대비를 적용했다. Win7/8은F2, Win10/11은ClearType 표시/측정으로 분기했다. Release62 및 실제 UI53 PNG·편집/드롭다운·OS정책/아이콘 검증 통과. 고정 scripts/Build-PortablePreview.ps1이 DX Manager Preview 폴더를 관리 목록으로 덮어쓰며 설정/로그를 보존하고 ZIP을 만들지 않는다. 재실행에서4개 개인 설정파일 보존/62payload 일치 확인. 승인된 dist34항목을 경계/재분석지점 점검 후 삭제했다. 남은 것은 DX Companion(내부GitHub2.1.4 포함), DX Manager Store, 원본 DX Manager v2.5.0, DX Manager Preview. 오래된 설정/캡처는 .build-tools/dist-cleanup-20261008/user-data에 보관했고 과거F2설정은 고정폴더로 옮겼다. 원본2.5.0/Store 보존. 상세: FIXED_PREVIEW_2026-10-08.md. 커밋/업로드 없음.
## 2026-10-08 ClearType regression correction

메이님이 현대 Windows에서 큰 한글 제목의 획이 거칠어진 실제 화면을 보고했다. 이전 요청에서 추가한 ClearTypeGridFit 분기가 원인 후보이며, 안티앨리어싱을 끈 변경은 아니었다. 해당 픽셀 맞춤 분기를 제거하고 모든 지원 OS의 UiText 표시/측정을 검증된 F2 AntiAlias로 복원했다. 아이콘20px·색 대비·Store 캡처/Wifi 매핑·버튼 정렬·도움말 높이는 유지한다. Release와 실제 UI/경로 선택·undo/드롭다운/경고 상자 검증 통과. 고정 Preview 폴더만 갱신했고 ZIP은 만들지 않았다. 최신 EXE SHA256: 56D2D1AB00A4A2372F09E4E8CE4793528C4E60B655930AF8826ABF9F20AE1555. 이전 FIXED_PREVIEW 문서의 OS별 분기는 이 기록으로 대체한다. 집 PC에서 실제 제목 모양 재확인 필요. 커밋/업로드 없음.
## 2026-10-11 local checkpoint

메이님이 마지막 프리뷰에 반영된 버전을 로컬 Git에 커밋하도록 요청했고, 새 브랜치는 만들지 말고 커밋만 하라고 확인했다. 현재 코드의 Release 실행 파일과 고정 프리뷰 실행 파일 해시는56D2D1AB...로 일치한다. F2 AntiAlias 복원·20px 아이콘/대비·Store 실행 버튼/도움말 높이·고정 프리뷰 및 기존 테스트 도구/문서를 현재 브랜치의 체크포인트로 저장한다. 버전2.5.0 유지, Store/APK 및 미커밋 사용자 데이터/빌드 산출물은 포함하지 않는다. 새 브랜치/태그/푸시/업로드를 하지 않으며, 실제 커밋 해시는 Git 기록에서 확인한다.

## 2026-10-12 shared official Companion migration
User-approved GitHub workroom migration: official Android source/APK now in E:\vs\DX Companion Shared.
2.1.5/code12 bytes/signature unchanged; common metadata and build/package wrappers integrated.
Old source archived, signing materials preserved. See SHARED_COMPANION_2026-10-12.md.
No product UI, submission, public upload or commit performed by this migration.

## 2026-10-15 official2.5.0 acceptance and update notifications

User authorized the official port after reporting the private test works. scrcpy5.0.1 full runtime,
explicit auto decoding, accepted non-AI UI/tray/slot/TLS/wireless fixes and shared official APK2.1.5
are included. Foreground accessibility/app caches/AI Enter/WebAPK rules/private identity are excluded.
Official protocol1 and legacy Scroll Lock remain. Updates run automatically once per local day,
with failed attempts persisted, a5-second overall background deadline, no retry loop, manual About
button, themed banner/tray/manual result and full selected scrcpy version diagnostics.
See OFFICIAL_250_UPDATE_2026-10-15.md for scope, verification, fixed output and field boundaries.
No Store/private/shared-Android edits, commit or publication.

Official2.5.0 fixed preview completed. Final Release0 warnings/errors;475 checks passed.
Actual GitHub client returned Current in373ms. Both themed manual dialogs/main native banner,
About action and full scrcpy diagnostics inspected. All16 runtime files match;11 preserved
data/artifact hashes and Store status unchanged. No AI tracking types or private APK/ports.
EXE SHA256: 49A409F2255971FBDF25252A5A1AA7A1BB4D915E5C8B3FA29E8B626407D2FBB2
Current delivery is E:\vs\dex system\dist\DX Manager Preview\DXManager.exe.
Offline actual-dialog preview is under .build-tools/official250/update-ui-preview.
No commit/ZIP/upload. Final phone/Windows-shutdown validation and virus scan remain user checks.
## 2026-10-16 About update action placement

User requested the manual update button beside third-party notices. The About footer now
places both 200x40 actions in one row with a12px gap, notices first and update check second.
No update-check behavior changed: automatic available updates use the in-main banner
(and a tray notification when hidden); manual checks use the themed result dialog.
Automatic Current/Failed results show no result dialog. A previously known, undismissed
newer release can retain its banner while offline.
Release x64/net462:0 warnings/errors; existing actual-assembly desktop/UI checks176 passed.
Light/dark About screenshots inspected. Fixed Preview replaced;6 configuration hashes
preserved. EXE SHA256: 9FB35D153B8F515AC7616997DF810993B0FD0ABCFCF1025606818B8F96D239B4.
No new dist folder/ZIP, commit or publication. Store/private/shared Android unchanged.

## 2026-10-16 Automatic update banner lower-right placement

User prefers the same lower-right area as action/error notices. The persistent update banner
now uses the existing32/28 desktop-scaled right/bottom margins. While another notice is visible,
it sits12 desktop-scaled pixels above it and returns when that notice closes or is hidden.
It remains a nonmodal child panel: uncovered main controls remain usable. There is no expiry
timer; X hides/dismisses this release, while the GitHub action only opens the release page.
Release x64/net462:0 warnings/errors; existing actual-assembly desktop/UI checks176 passed.
Native main-window light/dark captures inspected. Fixed Preview replaced;6 configuration
hashes preserved. No Store/shared Android/private product changes, commit or publication.
EXE SHA256: 58E53660113F94349B159181B1B8F2FD4FA33EC69B401CC24D12F47D3626241B
## 2026-10-16 final2.5.0 distribution / Companion2.1.6

User accepted key input and requested the final portable build and APK fix. Shared official2.1.6/code13
now stops idle protection after PC disconnect when overlay/stay-awake are verified inactive; startup
and stale-callback guards preserve live/new sessions. Android34 and signed Release/lint passed
(lint0 errors/70 warnings). Official signature/package/protocol1 unchanged; phone notification check pending.
Final Windows Release0 warnings/errors;505 checks passed, including30 real consent UI checks.
The GUI fixture now carries the product's executable config to match .NET4.6.2 runtime semantics.
Fresh39-file2.5.0 distribution has no docs/images, saved consent/settings, logs, PDBs or private test tools.
Previous final (including user data) backed up under .build-tools/final250/previous-final; fixed Preview
updated with6 config hashes preserved and obsolete managed docs/images removed. No ZIP/commit/upload.
EXE SHA256: BBFFA0F93E82B7D535B4E049DD0DFB0E8A3C21A81FFD101F370550AFE13FC918
APK SHA256: 904491111341D46458BB9CE9B342D9210A20BB923BC0F605484D6AF7B90DFCC9
Current delivery: E:\vs\dex system\dist\DX Manager v2.5.0\DXManager.exe.
Details and physical verification boundaries: FINAL_250_2026-10-16.md.
## 2026-10-16 upload archive

User requested confirmation of an upload-ready ZIP after the folder-only final build.
Created E:\vs\dex system\dist\DX-Manager-v2.5.0-win-x64.zip (20,790,867 bytes).
SHA256: 5DD69CD55157CA27DD9027BE5955DE60AAAEBA01B9BF1ABFF5B21A9F11616791
All39 ZIP files match the verified final manifest; Companion2.1.6 is included.
No personal settings, saved consent, logs, external guides/screenshots or developer files.
The archive uses a single DX Manager v2.5.0 root folder. No GitHub upload/commit performed.
Evidence: .build-tools/final250/upload-archive.json. Existing final/preview user state was not modified.
## 2026-10-16 local GitHub documentation audit

User explicitly stopped publication and then requested a complete GitHub document/screenshot review.
No commit/push/tag/release or remote page edits are authorized by the scan result or this documentation task.
Public main snapshot2a1ec107450d8755e3691e9fa52aa086ed034e8d:40 document/configuration paths,
6 historical release descriptions, no wiki. Full read-only originals are in .build-tools/documentation-250.
Updated README, both full guides, both FAQs and developer/source/resource guidance for2.5.0/5.0.1/2.1.6.
Preserved historical release/license facts. Current local link/image/anchor check:40 Markdown files,
104 local references,7 image references,0 issues before this status update.
All9 provided Store originals at E:\vs\소스\스토어판 매뉴얼용 스샷 were visually inspected; app screenshots
need current portable captures. Existing6 Android-setting examples can remain;17 old app images are
kept on disk but detached from current instructions. No binaries or image pixels were changed.
Open publication preparation: new screenshots, standalone public shared-Companion dependency/setup,
and preservation of remote-only macOS material during future integration.
See DOCUMENTATION_AUDIT_2026-10-16.md and SCREENSHOT_PLAN_v2.5.0.md for exact file actions and shot names.

# Session Handoff

마지막 갱신: 2026-08-31

## macOS 포터블 배포 작업

- 현재 통합 브랜치: `dev-mac-integrated` (upstream v2.0.1 merge commit 전)
- 원 PR #3 작업 브랜치: `codex/macos-portable-release` (역사적 기록)
- `scripts/Package-Mac-Release.sh`가 `osx-arm64`와 `osx-x64`용 DX Manager와
  ADB proxy를 self-contained single-file로 publish한다.
- 공식 scrcpy 4.1 arm64/x86_64 정적 아카이브를 고정 SHA-256으로 검증해
  ADB와 scrcpy-server를 함께 포함한다.
- `DX-Manager-v2.0.0-macos-arm64.zip`과
  `DX-Manager-v2.0.0-macos-x64.zip`, 각 `.sha256` 파일을 생성한다.
- GitHub Actions workflow는 `macos-15`와 `macos-15-intel`에서 빌드·95개
  테스트·39개 다중 기기 테스트·package smoke test를 각각 수행하도록
  구성했다. PR에서는 artifact를 보관하고, `v*` 태그에서는 정확한 네 파일과
  체크섬을 검증한 뒤 Release 초안을 만든다. 첫 원격 run 결과 확인은 남아 있다.
- Apple Silicon ZIP은 native 실행 검증을 통과했다. Intel ZIP은 Mach-O x64
  검사와 이 Mac의 Rosetta에서 DX Manager, proxy, scrcpy 4.1, ADB 37.0.0
  실행을 확인했다. 실제 Intel Mac의 Galaxy DeX 실기는 남아 있다.
- macOS `Q` 종료가 no-op이던 조건을 제거하고 실행 중인 서비스 정지와 DeX
  overlay cleanup 완료를 기다리도록 수정했다. CLI `--dex`/`--stop-dex`도 기기
  감시가 첫 snapshot을 읽은 뒤 동작하며 시작·자연 종료 대기의 `Ctrl+C`를 실제
  정리 경로에 연결한다.
- DeX 시작·overlay 제거 직전에 live stable identity를 다시 읽는다. 같은 endpoint의
  다른 휴대폰에는 정리 명령을 보내지 않고, USB transport가 사라져도 같은
  identity로 검증된 Wi-Fi transport가 남아 있으면 그 경로로 정리한다. 보류된
  cleanup은 identity별 독립 항목으로 보존하고 새 overlay 설정이 성공한 뒤에만
  완료 처리한다.
- 최종 소스 기준 .NET 8 Release 빌드는 경고 0·오류 0, xUnit 95개와 다중 기기
  회귀 테스트 39개는 모두 통과했다.
- 자동 artifact는 아직 Apple Developer ID 서명·notarization이 없으므로 최초
  실행 시 Gatekeeper 승인이 필요할 수 있다.

## macOS 크로스플랫폼 변환 작업 현황 (v2.0.0 macOS Edition)

- **1단계 (아키텍처 분리 및 코딩)**:
  - `DexManager.Core` (.NET 8.0): 플랫폼 중립적 비즈니스 로직, 모델, 런타임 세션 레지스트리, ADB 커맨드 빌더, 양방향 파일 전송 코디네이터 분리 완료
  - `DexManager.Mac` (.NET 8.0): macOS 전용 ANSI 컬러 콘솔 호스트 (`InteractiveHost`), TUI 대시보드, 네이티브 플랫폼 서비스 (`MacPlatformService`, `MacPathProvider`, `MacCaptureService`, `MacKeyboardService`, `MacAutoStartService`) 구현 완료
  - `DexManager.AdbProxy` (.NET 8.0): Named Pipe 기반 Scrcpy 파일 드롭 가로채기 및 관리형 전송 중계 구현 완료
- **2단계 (테스트 및 검증)**:
  - `DexManager.Tests` (net8.0, xUnit 2.5.3): 95개 단위/통합 테스트 100% 통과
  - `DexManager.MultiDeviceTests` (net8.0): 39개 다중 기기 세션 격리 회귀 테스트 100% 통과
- **3단계 (리팩터링, 품질 개선, 문서화)**:
  - C# 12 / .NET 8 최신 문법(파일 범위 네임스페이스, 패턴 매칭, 레코드, 컬렉션 식, 식 본문 멤버) 적용
  - 불필요한 레거시/더미 파일(`UnitTest1.cs`) 정리 및 코드 정리
  - Release 빌드 시 경고 0개, 에러 0개 유지 (`/warnaserror` 통과)
  - macOS 전용 가이드 문서 `docs/MACOS_GUIDE.md` 작성 및 `CHANGELOG.md` 갱신
  - 최종 릴리스 빌드 및 테스트 스위트 검증 완료

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
서명 비밀 파일을 포함하지 않는다. upstream에서 검증한 ZIP SHA-256은
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

# Decision Log

## Experimental hidden phone preview — 2026-10-30

Keep the existing overlay mode as the default. The new per-phone option requests a Samsung loopback wireless DeX display while scrcpy continues to carry PC video/input/audio. A separate authenticated helper lease permits cleanup to start after a 15-second heartbeat loss even when ADB is unavailable. This uses Samsung internal APIs and requires per-firmware physical testing. Integrate the helper under Apache-2.0 notices without changing the project license or Companion permissions. Feature integration and lifecycle controls are contributed by asyouplz.


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

## 2026-09-30 - 앱 목록은 물리 기기 소유로 보관한다

Store판의 identity별 캐시·화면 projection 구조를 참고한다. 앱 조회 결과는 시작 시
캡처한 기기에 저장하고 선택 기기를 바꿔도 원래 기기 소유를 유지한다. 기기·모드
전환은 현재 기기의 목록과 저장된 시작 앱을 다시 구성한다. 같은 identity의 USB와
Wi-Fi는 같은 캐시를 사용한다. 전체 목록 캐시는 현재 실행에만 유지하고 성공적으로
시작한 최근 앱은 기기별 프로필에 저장한다(schema 27). 이전 전역 최근 앱은
출처를 추측해 여러 기기로 복제하지 않고 원래 저장 데이터로 보존한다.
완료 메시지는 Store처럼 5초 동안 포커스를 옮기지 않고 표시한다.

## 2026-09-30 - 수정 소스와 재사용한 Companion APK를 구분한다

기존 Store 2.1.2 APK를 그대로 사용하는 사용자 결정은 유지한다. 감사에서 확인한
다중 요청 timeout·서비스 종료 수명주기 문제는 포터블 Android 소스와 Debug
검증에 반영했다. 이 소스 수정이 고정된 서명 APK hash에 포함됐다고 기록하지 않는다.
실제 배포 반영에는 새 서명 버전과 PC version/hash 검증값 갱신을 함께 결정해야 한다.
문서 제공자의 trim/slash 이름 충돌에 대한 예전 소스 전용 완료 표시는 철회한다.

## .NET Framework 4.6.2

64비트 Windows 7 SP1과 오프라인·폐쇄망 환경을 지원하기 위한 최소 대상
버전으로 4.6.2를 유지한다. Windows 7 SP1에 4.6.2가 기본 포함되는 것은
아니므로 사용자 문서에서 별도 설치 가능성과 공식 오프라인 설치 경로를
안내한다.

.NET Framework 4.x는 제자리 업데이트이므로 4.7.2 또는 4.8이 설치된
PC에서는 같은 빌드가 설치된 최신 4.x 런타임으로 실행된다. 4.7.2 전용 API가
필요해지기 전에는 최소 요구 사항만 높이는 재대상 지정을 하지 않는다.

## OS별 ADB

Windows 10 미만은 legacy ADB를 사용한다. Windows 10 이상은 선택한 Scrcpy
폴더의 ADB를 사용하고, 해당 ADB를 실행할 수 없을 때 legacy ADB로
대체한다.
PATH 의존은 배포 환경 차이를 만들기 때문에 금지한다.

## 키 보정은 scan code 우선

AHK의 `SC1F2::Send +{Space}`에 맞춰 전용 한영키는 scan code로 감지하고
SendInput scan-code 조합을 우선한다. `VK_HANGUL`이 오른쪽 Alt 위치의
extended `scan 0x38`로 들어오는 일부 한국어 노트북도 지원하되, scan 값만으로
판정하지 않는다. 따라서 같은 scan을 사용하는 `VK_RMENU` 오른쪽 Alt/AltGr는
브라질·유럽 키보드의 특수문자 입력을 위해 그대로 통과시킨다. ADB는
fallback으로 남겼다.

## Scrcpy 4.0 오른쪽 Shift 호환

Windows에서 Scrcpy 3.3.4/SDL2는 오른쪽 Shift가 정상 동작하지만
Scrcpy 4.0/SDL3는 같은 물리 입력을 처리하지 못하는 현상을 재현했다.
Windows 저수준 후크에는 `vk=0xA1`, `scan=0x36` 누름/뗌이 모두 들어오므로
DX Manager의 감지 실패는 아니다.

SDL3 기반 Scrcpy 창이 활성화된 경우에만 원본 오른쪽 Shift를 차단하고
SendInput scan code 왼쪽 Shift로 치환한다. 오른쪽 Shift를 다시 보내는
보정은 SDL3의 같은 처리 경로를 반복하므로 사용하지 않는다. 이 우회로 인해
Android에서는 좌우 Shift를 구분할 수 없지만 일반 타이핑 기능을 우선한다.
4.0에서 확인한 근거는 그대로 보존하되 현재 보정 적용 범위는 SDL3 기반
Scrcpy 4.x 클라이언트로 표현한다.

## 외부 Scrcpy 버전 호환

지정한 Scrcpy의 `--version` 결과에서 Scrcpy와 SDL 주 버전을 감지한다.
3.3.4는 잠자기 방지에 `-w`를 사용하고 `--flex-display`를 제외한다.
4.0 이상은 `--keep-active`와 `--flex-display`를 사용한다. 버전 감지에
실패하면 번들 기준인 Scrcpy 4.1 동작을 유지한다.

## Unicode 파일 전송은 제한된 Scrcpy ADB proxy 사용

Windows OLE 드롭을 전역 후킹하거나 Scrcpy 창 위에 별도 드롭 창을 겹치지
않는다. Scrcpy의 기존 드롭 UX는 유지하고, DeX·단일창 프로세스가 호출하는
ADB만 작은 proxy로 연결한다. proxy는 세션을 시작할 때 고정한 휴대폰 대상
폴더의 파일·폴더 push만 DX Manager의 관리 큐로 전달하며 나머지 명령은 실제
ADB에 그대로 넘긴다.
DX Manager 자체 ADB, 무선 연결, wake-up과 화면 상태 경로는 proxy를 사용하지
않는다.

로컬 Unicode 경로를 실제 ADB에 안전하게 전달하고 Android shell quoting을
피하기 위해 ASCII 임시 이름으로 push한 뒤 최종 UTF-8 이름을 Base64로 전달해
휴대폰에서 복원한다. 폴더는 숨은 staging에 구조 전체를 완성한 뒤 최종 이름으로
이동하며 재분석 지점은 따라가지 않는다. 한 기기의 같은 파일·폴더 이름은
덮어쓰지 않고 `(1)`, `(2)`로 충돌을 피한다. 전송은 전역 FIFO로 직렬화하고
scrcpy 세션이 끝나면 해당 전송도 취소한다. 최종 이동 중에는 취소를 잠시
막고 기기 측 P/C 커밋 표식을 사용해 ADB 응답 중단 뒤에도 이동 결과를
재확인한다. 확정할 수 없는 결과의 표식과 임시 경로는 즉시 지우지 않고 다음
유휴 세션 준비 시 DX Manager 전용 잔여물로 정리한다.

관리형 전송은 한글·Unicode 파일명 문제를 기본적으로 해결하기 위해 켜 둔다.
사용자가 순정 동작을 원할 수 있으므로 설정에서 끌 수 있으며, 변경은 이미
실행 중인 프로세스를 바꾸지 않고 새로 여는 DeX·단일창부터 적용한다.

## ADB 실제 빌드 버전 표시

`Android Debug Bridge version 1.0.41`은 서로 다른 platform-tools에도 같은
값이므로 사용자에게 유용한 버전 식별자가 아니다. 설정, 환경 점검과 로그는
그 다음 `Version ...` 줄을 엄격히 파싱해 실제 빌드 값을 표시한다. 해당 줄을
파싱할 수 없으면 잘못된 공통 버전을 대신 표시하지 않고 버전 확인 불가로
처리한다.

## 일반 텍스트 입력은 네이티브 편집 엔진

경로와 추가 인자처럼 자유 편집이 필요한 필드는 커스텀 둥근 프레임 안에
테두리 없는 WinForms TextBox를 배치한다. 외형은 DX Manager가 그리되
선택, 드래그, 가로 스크롤, 실행 취소와 IME는 Windows에 맡긴다. 숫자와
단축키처럼 동작이 제한된 필드는 기존 커스텀 입력기를 유지한다.

## 최대 display ID 선택 제거

DeX와 단일창을 함께 쓰면 display가 여러 개다. 생성 전후 차집합과
메타데이터로 선택하고, 모호하면 추측하지 않고 실패한다.

## 단일창은 Scrcpy display

단일창은 overlay DeX를 추가하지 않고 Scrcpy `--new-display`로 만든다.

## 다중 Scrcpy 화면 전원 조정

한 창 종료가 나머지 창을 깨우거나 검게 만들지 않도록 전체 세션 요청으로
판단한다. 화면 OFF 재적용은 Scrcpy 시작과 직렬화한다.

## 앱 이름과 패키지 함께 저장

재시작 뒤 창 제목이 패키지명으로 바뀌지 않도록 둘 다 저장한다.

## 세션 로그

상시 누적 대신 현재 실행 로그만 표시하고 요청할 때 파일로 저장한다.

## 무선 연결

`adb tcpip` USB bootstrap과 Android 11 페어링을 모두 지원한다. USB 준비
시 Wi-Fi IP를 자동 감지하고 직접 입력은 보조 수단으로 둔다.

## v1은 최초 휴대폰 한 대에 고정

DX Manager v1은 여러 휴대폰을 동시에 제어하지 않는다. 한 실행에서 처음
선택한 휴대폰의 `ro.serialno`, `ro.boot.serialno`, 필요 시 Android ID를
기기 식별값으로 보존하고 앱 종료 전까지 다른 휴대폰을 무시한다.

USB serial과 무선 `IP:PORT`는 같은 휴대폰에서도 다르므로 ADB 주소만으로
고정하지 않는다. 실제 기기 식별값이 같으면 USB/무선 전환과 재연결을 같은
휴대폰으로 인정한다. 다중 휴대폰 선택 또는 동시 제어는 v2 범위로 둔다.

## DeX overlay 재사용과 종료 정리

기존 overlay가 있다는 사실만으로 제거하지 않는다. 실제 display의 너비,
높이, DPI를 숫자로 비교해 세 값이 모두 설정과 같으면 기존 display ID를
등록해 재사용한다. 하나라도 다르면 `settings delete global
overlay_display_devices`로 설정 항목을 삭제한 뒤 현재 설정으로 다시 생성한다.

정상 종료에서는 생성 주체나 이전 세션 여부를 구분하지 않고 연결된 관리
기기에 `settings delete global overlay_display_devices`를 한 번 실행한다. 실패는 로그로
남기되 앱 종료는 계속한다.

## Android 정리 앱 권한은 package와 인증서를 함께 검증

같은 package ID만으로는 신뢰하지 않는다. 번들 APK는 고정된 파일 SHA-256과
APK Signature Scheme v2의 단일 인증서 SHA-256을 모두 검사한다. 설치는 자동으로
실행하지 않고 사용자가 진단 페이지에서 명시적으로 눌렀을 때 현재 선택된 기기에만
수행한다. 설치 후 휴대폰의 `base.apk`를 임시로 가져와 package·버전·공식 인증서를
다시 검사한다. 모두 일치할 때만 `WRITE_SECURE_SETTINGS`를 부여하며 직전·직후에도
재검증한다. 사후 검증 실패 시 권한을 즉시 회수한다.

서명이 다른 같은 package는 덮어쓰거나 권한을 부여하지 않는다. 번들보다 새 버전이
설치된 경우도 자동으로 낮추지 않는다. 삭제는 사용자가 명시적으로 승인한 현재
선택 기기에만 수행하며 해당 기기의 파일 수신 세션과 ADB reverse를 먼저 정리한다.

Companion은 범용 설정 편집기나 shell 도구로 만들지 않는다. 복구는
`overlay_display_devices` 조회·삭제와 절전모드 해제 끄기로 제한한다. 파일 전송은
사용자가 Android 공유 메뉴나 폴더 선택기에서 고른 항목만 현재 DX Manager의
기기별 수신 세션으로 보낸다. Android 전역 overlay 설정 하나를 다루므로 DX
Manager 생성 화면과 사용자가 직접 만든 시뮬레이션 화면을 구분하지 않는다.

## 2026-09 - GitHub 포터블 Companion은 Android 13+ 지원

메이님이 새 Companion의 하한을 Android 13 (API 33)으로 정했다. 포터블판은
Store에서 사용한 2.1.2/versionCode 9 APK를 그대로 번들링하며 별도 Release APK를
만들지 않는다. 복사한 바이너리의 package·minSdk·version·공식 인증서·SHA-256을
확인해 PC 설치 검증 계약과 패키징 스크립트를 함께 고정한다. URI 권한과
`dataSync` timeout 처리는 포터블 소스에서도 검증한다. DXMR 휴대폰→PC 소켓
프로토콜은 별도 기능이 추가돼도 v1을 유지한다.

## 2026-09 - mDNS endpoint는 기기 identity 검증 뒤 저장

Android 11+ `_adb-tls-connect._tcp` 발견과 `_adb-tls-pairing._tcp`는 별개다.
파서는 connect 후보만 연결 endpoint로 쓰며 첫 후보를 임의로 고르지 않는다. 저장
profile의 stable serial identity와 mDNS instance가 일치해야 하며, ADB 연결 뒤 실제
휴대폰 identity도 다시 맞을 때만 동적 host·port를 저장한다. mDNS를 쓸 수 없거나
결과가 모호하면 저장 주소와 수동 IP·연결 포트 경로를 유지하고, 페어링 포트와
연결 포트를 혼동하지 않는다.

## 휴대폰 경로는 Android 표기 유지

PC 경로와 같은 입력칸·찾아보기 버튼 구성을 사용하되 휴대폰 경로는 Android의
`/sdcard/...`와 `/` 구분자를 유지한다. 폴더 목록은 UTF-8 NUL 구분 데이터를
Base64로 받아 Windows 7에서도 한글·Unicode 이름을 보존한다.

## 기기 연결 후 시작 대기

ADB 연결 상태와 휴대폰 이름 확인은 즉시 수행한다. 설정된 0~60초 대기는
DeX/단일창의 실제 시작 명령 직전에만 적용한다. 기본값은 1초이며 화면 OFF
재적용용 Scrcpy에는 별도 대기를 적용하지 않는다.

## 2026-07 - 제품명은 DX Manager

배포 시 Samsung DeX 브랜드와 제품명이 혼동되지 않도록 프로그램 브랜드를
DX Manager로 바꿨다. 기능 설명의 `DeX 모드`, `DeX 시작`은 실제 지원
기능명이므로 유지한다. 내부 namespace와 저장소 폴더명은 불필요한 위험을
피하기 위해 `DexManager`를 유지한다.

## 2026-07 - 영어 기본 리소스와 한국어 위성 리소스

UI 문자열은 `.resx`로 관리한다. Windows UI 언어가 한국어면 한국어,
그 외에는 영어를 자동 선택하며 설정에서 수동 지정할 수 있다. 기술 로그는
문제 분석의 일관성을 위해 당분간 기존 언어를 유지한다.

## 2026-07 - 실행 버튼 단순화

동일한 크기의 시작/중지와 설정 적용 버튼이 나란히 있던 구조를 제거했다.
시작/중지 버튼 하나를 기본 동작으로 두고, 실행 중 설정이 바뀐 경우에만
작은 `변경사항 적용` 링크를 표시한다.

## 2026-07 - Android와 One UI는 확인 기준으로 표기

Samsung의 DeX 구현은 Android, One UI, 기기 모델과 펌웨어에 따라 달라질 수
있다. 사용자 문서에는 광범위한 최소 버전을 추정해 지원한다고 쓰지 않고,
현재 실기에서 정상 동작을 확인한 Android 16 / One UI 8.x를 기준으로 적는다.
One UI 7.x 이하에서는 원활한 동작을 확인하지 못했고 검은 DeX 창이 나타날
수 있음을 함께 알린다.

## 2026-08 - v2는 물리 기기와 ADB transport를 분리

v2의 관리 단위는 ADB serial 문자열이 아니라 물리 휴대폰이다. 한 물리 휴대폰은
USB serial과 하나 이상의 무선 `IP:PORT` transport를 동시에 가질 수 있다.
`ro.serialno`, `ro.boot.serialno`, 필요 시 Android ID로 얻은 안정적인 기기
식별값이 같을 때만 같은 물리 휴대폰으로 병합한다. 모델명이나 표시 이름이 같다는
이유로 서로 다른 휴대폰을 병합하지 않는다.

각 transport는 serial, USB·무선·에뮬레이터 구분, ADB 상태를 별도로 보존한다.
명시적으로 선택한 정상 transport가 있으면 이를 우선하고, 별도 선택이 없으면
정상 USB, 정상 무선 순서로 선택한다. 일시적으로 identity를 조회할 수 없을 때는
기존에 확인한 serial→identity 관계를 재사용하고, 한 번도 확인하지 못한 serial만
`transport:<serial>` 임시 식별값으로 분리한다.

이 레지스트리는 먼저 기존 v1 실행 흐름과 연결하지 않은 독립 기반으로 도입한다.
다음 단계에서 모든 ADB·Scrcpy·Companion 명령이 대상 identity와 transport serial을
명시하도록 전환한 뒤에 다중 기기 UI와 동시 세션을 연결한다.

## 2026-08 - ADB 실행은 전역 대상 없이 명시적 serial만 사용

`AdbService`는 선택된 기기를 보관하지 않는다. 기기별 명령은 호출 시점에 받은
transport serial로 항상 `adb -s "SERIAL" ...`를 구성하며, 빈 serial을 전달하면
실행 전에 거부한다. 프로세스 전역 `ANDROID_SERIAL`도 설정하지 않는다. 따라서
동시에 서로 다른 기기의 명령이 만들어져도 마지막으로 선택한 기기나 환경 변수에
의해 대상이 바뀌지 않는다.

기기 탐색, `adb devices`, 서버 시작·종료, 무선 `connect`·`disconnect`·`pair`처럼
특정 기기 shell에서 실행되지 않는 ADB 명령만 serial 없이 실행할 수 있다. 오래된
Windows 7 ADB를 깨우기 위한 scrcpy 보조 실행도 아직 대상을 알 수 없는 탐색 상황에
한해 serial 없는 실행을 허용한다.

`WirelessAdbService.SelectedSerial`은 v1 호환 선택 정책이다. 이 값은 실행 서비스의
전역 target이 아니며, UI 작업을 시작할 때 문자열을 캡처하여 이후 서비스 호출에
명시적으로 넘긴다. DeX·단일창 세션과 종료 정리는 시작 시 캡처한 serial을 사용한다.
파일 전송 취소와 Companion detach도 요청한 serial이 현재 작업 serial과 일치할 때만
적용한다. 기기별 장기 수명 상태 자체는 다음 런타임 세션 단계에서 분리한다.

## 2026-08 - 무선 ADB 설정도 물리 기기별로 저장

복수 휴대폰 환경에서 IP·포트·USB/무선 모드·자동 재연결을 전역 설정 하나로
공유하지 않는다. 각 설정은 안정적인 물리 identity를 키로 하는
`DeviceWirelessConnectionProfile`에 저장한다. 설정의 연결 페이지는 작업 대상
휴대폰을 명시적으로 선택하며 기본값은 메인 화면의 현재 기기 탭이다.

USB로 무선 준비할 때는 승인된 USB 장치가 하나인지 추측하지 않고 선택한 물리
기기의 USB transport serial을 정확히 전달한다. 입력하거나 저장한 주소로 연결이
실패하고 USB에서 감지한 주소가 다를 때만 감지 주소로 한 번 재시도한다. 따라서
한 휴대폰의 저장 IP가 다른 휴대폰의 `adb tcpip` 준비에 사용되지 않는다.
연결 성공 뒤에도 endpoint에서 물리 identity를 다시 조회하며, 선택한 identity와
다르면 설정 저장과 transport 전환을 거부한다.

기존 전역 `ConnectionSettings`는 이전 설정 파일 마이그레이션과 v1 실행 흐름의
호환을 위해 보존한다. 신규 기기별 프로필은 해당 endpoint가 실제 그 휴대폰의
transport로 확인됐거나 최초 선택 기기의 레거시 설정일 때만 이 값을 씨앗으로
사용한다. 자동 재연결은 저장된 모든 기기별 무선 프로필을 중복 endpoint 없이
순회한다.

기기별 `Mode`는 현재 우연히 보이는 transport를 따라가는 표시값이 아니라 사용자의
강제 연결 정책이다. 설정 화면의 USB·무선 라디오 버튼은 항상 이 저장값을 표시하고,
실제 감지된 USB·무선 연결은 별도의 상태 문구로 보여준다. USB 정책에서는 무선으로,
무선 정책에서는 USB로 자동 대체하지 않는다. 선택한 방식이 없으면 해당 기기는
연결 대기 상태가 되며 실행 중 세션·전송·ADB reverse를 정리한다. 케이블 유무와
관계없이 USB 정책을 저장할 수 있고, 반대 transport가 이미 ADB 목록에 남아 있어도
명시적으로 다시 선택하기 전에는 사용하지 않는다.

## 2026-08 - 런타임과 실행 서비스는 물리 기기마다 하나씩 둔다

물리 기기마다 DeX 세션, 단일창 슬롯, Companion 연결, 양방향 전송과 화면 전원
복구 상태를 보존하는 런타임 세션을 하나씩 둔다. 연결이 끊겨도 런타임 기록은 즉시
삭제하지 않는다. 중단된 전송, 남은 overlay와 절전모드 해제처럼 나중에 같은 기기가
돌아왔을 때 정리해야 하는 증거가 남기 때문이다.

Scrcpy, DeX orchestrator, 단일창, 화면 OFF, Companion 수신기와 PC→휴대폰 전송
큐도 `DeviceRuntimeServiceSet`으로 묶어 물리 기기별로 독립 생성한다. 서비스 묶음은
고유 instance ID로 런타임에 1:1 결속하며 다른 묶음으로 재결속하지 않는다. 같은
휴대폰의 USB·무선 serial은 하나의 물리 identity로 합쳐지므로 transport 전환 뒤에도
같은 런타임과 서비스 묶음을 사용한다.

여러 Scrcpy 서버를 동시에 밀어 넣을 때의 ADB·기기 부하를 피하기 위한
`ScrcpyLaunchCoordinator`만 프로세스 전체에서 공유한다. 이 공유 객체는 시작 순서만
직렬화하며 대상 기기나 세션 상태를 보관하지 않는다.

## 2026-08 - 기기 선택 UI는 런타임을 선택하되 비선택 세션을 중지하지 않는다

메인 화면 왼쪽 사이드바의 각 기기 항목은 물리 기기 identity 하나와 그 기기의
`DeviceRuntimeServiceSet` 하나를 가리킨다. 선택 변경은 UI 명령 대상을 바꿀 뿐,
다른 기기에서 실행 중인 DeX·단일창·파일 전송이나 미니바를 중지하지 않는다.

저수준 키보드 후킹과 전역 캡처 단축키는 Windows 프로세스 전체 자원이므로 동시에
여러 묶음에서 활성화하지 않는다. 탭을 바꿀 때 이전 컨텍스트의 후킹을 해제하고 새
컨텍스트만 시작한다. 설정 창의 기기 조회 delegate는 현재 선택을 동적으로 읽으므로
탭 전환 시 창을 닫지 않고 대상 휴대폰·무선 연결 상태·Companion 진단만 새로
고친다. 환경 점검 창은 실행 시점의 서비스를 보유하므로 탭 전환 시 닫는다.

연결 방식이 USB에서 Wi-Fi로 바뀌어도 물리 identity와 서비스 instance ID는
유지한다. 다만 이전 transport에 걸린 Scrcpy 세션·전송과 ADB reverse는 정리하고
새 serial로 Companion을 다시 구성한다. 프로그램 종료는 현재 탭만이 아니라 생성된 모든
기기 컨텍스트를 순회하여 DeX·단일창·전송·화면 전원 서비스를 정리한다.

## 2026-08 - 자동 시작은 선택 탭이 아니라 연결된 물리 기기마다 판단한다

`기기 연결 시 DeX 자동 시작`은 현재 화면에 선택된 탭 하나의 동작이 아니다.
프로그램 시작 전에 연결돼 있던 기기와 실행 중 새로 연결된 기기는 각각 자신의
물리 identity, 현재 선택 transport, 연결 세대와 저장된 DeX 설정을 사용해 자동
시작한다. 다른 탭의 아직 저장하지 않은 UI 값을 대신 적용하지 않는다.

연결 대기 중 transport가 바뀌거나 기기가 분리되면 캡처해 둔 연결 세대와 serial이
달라지므로 해당 자동 시작을 취소한다. 여러 기기가 동시에 연결돼도 Scrcpy 시작
직렬화기는 유지해 서버 전송과 창 준비가 겹치지 않게 한다.

기기 선택 영역은 한 대 사용 시 불필요한 UI이므로 프로그램 시작 시 연결 기기가
0~1대면 숨긴다. 실행 중 두 번째 물리 기기가 확인된 순간 표시하며, 사용 중이던
기기의 분리 상태와 다시 연결할 대상을 보존하기 위해 그 실행이 끝날 때까지는
숨기지 않는다. 다음 프로그램 실행에서는 현재 연결 기기 수로 다시 판단한다.

실행 중 휴대폰이 차례로 연결되면 먼저 확인된 휴대폰의 위치를 유지하고 새 휴대폰을
아래에 추가한다. 프로그램 시작 시 이미 여러 휴대폰이 연결돼 있으면 Galaxy 모델의
세대 정보를 해석해 최신 기기를 위에 둔다. 모델 세대를 알 수 없는 경우에는 표시
이름과 안정적인 identity를 보조 정렬값으로 사용하며, serial 문자열 순서만으로
기기 위치를 결정하지 않는다.

## 2026-08 - 구조 분리는 동작 보존형으로 하고 프로세스 수명주기 통합은 보류한다

다중 기기 기능이 안정화된 뒤 큰 파일을 다시 감사해 UI, 연결 수명주기, 설정 모델과
커스텀 컨트롤처럼 파일 이동만으로 책임이 선명해지는 부분은 분리한다. 이 과정에서
현재 구독되지 않는 v1 단일 기기 연결·분리 처리기는 제거한다. 해당 코드는 새 물리
기기 레지스트리 경로와 같은 정리·자동 시작을 별도로 구현하고 있어 다시 연결될 경우
한 기기를 두 번 정리할 위험이 있었다.

반면 `ScrcpyService`와 `SingleWindowService`의 닮은 프로세스 종료 코드는 즉시 합치지
않는다. 코드 양보다 종료 순서, 슬롯별 상태, 사용자 창 닫기와 연결 분리 경합을
보존하는 것이 우선이다. 공통화는 이 경로를 직접 검증하는 테스트가 생긴 뒤 별도
작업으로 수행한다.

## 2026-09 - Windows 종료와 도구 시작은 짧은 잠금에서 직렬화한다

공개 v2.0.1은 DeX·단일창 시작 초기에 종료 상태를 검사하지만 준비 작업 뒤의
`Process.Start()` 직전에는 다시 검사하지 않았다. 이전 제작 기록에는
`Process.Start()`를 세션 잠금 밖에 둔 특별한 실기 사유가 없었다. 기존 세션
잠금을 오래 유지하거나 Scrcpy 창 준비까지 잡는 공용 시작 직렬화기를 종료
요청의 잠금으로 재사용하면 Windows 종료가 지연될 수 있다.

각 서비스는 짧은 시작 잠금에서 종료 상태 최종 확인과 실제 프로세스 시작만
처리한다. `RequestShutdown()`은 같은 잠금에서 종료 상태를 고정한다.
DeX·단일창·wake-up Scrcpy와 관리형 전송의 직접 ADB 실행에 적용하며,
기존 세션별 serial·프로세스·전송 토큰 정리 흐름은 유지한다. 실제 Windows
종료·종료 취소 실기 검증은 아직 남아 있다.

## 2026-09 - 저장된 단일창 앱 프로필은 선택 앱을 복원한 뒤 적용한다

v2.0.1은 사용자가 앱을 직접 바꿀 때 `SelectionChangeCommitted`에서 package
프로필을 적용했다. 저장된 기본 앱이 앱 목록 로드 전부터 선택돼 있으면 이 이벤트가
발생하지 않아 프로필이 첫 표시와 실제 시작값에 적용되지 않을 수 있었다. 제작 채팅의
Git 이력 확인에서는 `fe10609`의 첫 진입·앱 목록 갱신 호출이 `5596f4c`의 기기별 앱
목록 변경 때 빠진 것으로 확인됐다. 당시 Store 코드와 테스트도 최초 선택 적용을
일관되게 보장하지 않았지만, 역이식 계획에는 이미
첫 진입 적용이 완료 항목으로 기록되어 있고 이를 의도적으로 지연 적용으로 바꿨다는
결정 기록은 확인되지 않았다.

포터블판은 설정 복원·단일창 진입·기기 전환·앱 목록 갱신 뒤 현재 기기의 선택 package
프로필을 확인해 적용한다. 슬롯에 프로필 값이 이미 있으면 다시 저장하지 않는다.
사용자 지정 해상도는 전역 최근 5개 목록에 보관하며, 그 목록에서 빠진 활성 슬롯·앱
프로필 값은 현재 UI 선택 항목으로 임시 추가한다. 실제 기기에서 시작 앱과 해상도가
적용되는지 확인은 남아 있다.

## 2026-10-01 UI defect correction

General text roles are increased by 2 DIP at the user's request; headings retain their Store sizes.
Disconnected contexts and profiles remain in memory/configuration, while selectors expose only
currently connected phones. Display names do not replace physical identities or command serials.
Panel scrollbar overlays use GDI and the existing AutoScroll offsets on Windows 7. Documentation
uses separate panels and wheel routing per tab. Native title bars remain under Windows control.
## 2026-10-02 Start preflight and painted corners

Missing app selection blocks only single-window slots, before any start side effects. DeX has no app requirement. The gray button retains click feedback. Painted rounded backgrounds replace hard window regions so antialiasing remains intact; native .NET 4.6.2/Win7 controls and existing device recovery logic are retained.

## 2026-10-05 Launch transport and Companion name collisions

By explicit user request, new DeX/single-window execution chooses authorized USB before Wi-Fi within the selected physical identity, regardless of the wireless profile's reconnect mode. Existing running or starting sessions retain their captured serial; transport selection never rewrites their commands/cleanup target. Losing USB stops its sessions and suppresses automatic Wi-Fi execution until the user presses Start again.
Companion keeps the DXMR v1 contract and Android13 minimum. Name normalization is retained, but a per-parent allocator reserves existing normalized names and assigns unique numbered segments before traversing folder descendants. This addresses the rare provider/API filename edge case without changing ordinary names such as spaces before an extension. A new officially signed APK2.1.4 is bundled into portable; Store is a later update, not a resubmission now.

### 2026-10-05 clarified reconnect behavior

The earlier manual-start-only interpretation after transport loss was too broad. The user's correction requires retaining DeX auto-start on a real reconnect. Suppress only the immediate fallback within a still-connected physical episode; restore automatic execution eligibility when the lost authorized transport returns or a new physical connection begins, subject to the existing feature setting. Auto-start captures the USB-first launch serial independently of an existing session's pinned runtime serial. This supersedes the prior manual-start-only wording above.
## 2026-10-05 Microsoft icon trial

Use a pinned MIT-licensed Fluent UI System Icons subset for portable small icons, with the original renderer retained for comparison/recovery. Keep the large custom phone artwork and logo. Original SVGs are retained; embedded transparent masks avoid Windows-version icon-font dependency and new runtime libraries. Document the standalone mouse as a derivative of Keyboard Mouse rather than an official separately named icon. Mini-bar connection display follows authorized observed transports per physical-device manager and never chooses a launch transport or changes session command targets. Store remains unchanged until the user accepts the appearance.

## Official update checks and private-feature exclusion (2026-10-15)

Port the accepted private UI/lifecycle fixes without its accessibility, app caches, AI keymaps,
package identities or protocol. Use the shared signed official2.1.5 APK and original Guardian1.
Keep runtime files in the source bundle and verify every generated copy against the pinned release.

Automatic update checks run once on the first launch of each local calendar day. Persist each
attempt before networking; offline/error results never cause an automatic retry. If state cannot
be persisted, skip automatic networking. Manual checks are explicit and remain available.
Separate update-check.json preserves existing application settings and schema.
Use a5-second overall background deadline plus request/read limits and a physical-request gate,
so slow DNS/proxy/TLS cannot queue unlimited requests or block the UI/shutdown path.
Only numeric newer stable versions and this repository's HTTPS release links are accepted.

Automatic information uses a themed main banner or tray notification; manual results use an
owner-centered themed dialog. Closing Main-to-tray closes pending dialogs and late results are ignored.
A separate development-only preview demonstrates real dialog classes offline, using example versions.
## 2026-10-16 documentation and publication boundary

The maintainer explicitly clarified that a clean antivirus result is not permission to publish.
README/FAQ/guides and screenshot preparation are local-only until an explicit release instruction.
Keep the exact scanned EXE/APK/ZIP bytes. A GitHub-only documentation edit is not an embedded-help rebuild.
The approved portable branch started from localv2.0.1 and lacks remote-main macOS additions; preserve them
when preparing a future integration rather than replacing remote main with this branch wholesale.
The external canonical Companion remains one maintained source. Its public dependency/snapshot must be
included or obtainable for a standalone source checkout before publishing2.5.0 source; do not restore
an older per-product APK/tree or duplicate ongoing Android maintenance to hide this gap.
Store screenshots were compared with the actual portable UI. New app screenshots will use the current
portable build; Android system-setting examples can remain, and no fixture mock will masquerade as a phone run.

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


## Documentation alignment — 2026-10-24

Use one fixed Windows asset name with a versioned inner folder; old tagged links do not become latest links.
Separate online update availability, local version notes and safety/120 consent. Describe preserved portable config explicitly.
Retain Q1–24 numbering and field-tested recovery/input caveats; add2.5.0 questions without advertising private features.
Use actual reviewed portable/official-Companion captures and honest initial/connected captions. Omit single-window photos as requested.
Current Settings catalog controls determine UI directions; do not invent controls from legacy model fields.
Keep the exact final EXE/APK/ZIP unchanged while updating GitHub docs. Embedded resource changes belong to a separate product build.
Preserve remote macOS text and identify its independent runtime, test and publication state.


## macOS implementation decisions retained from public main

## 2026-08 - macOS는 아키텍처별 self-contained ZIP으로 배포한다

macOS 사용자가 저장소를 clone하거나 .NET, Homebrew, scrcpy와 ADB를 직접
설치하도록 하지 않는다. 원본 Windows 프로젝트가 실행 파일과 도구 폴더 전체를
미리 만든 ZIP으로 배포하는 것과 같은 원칙을 적용한다. Apple Silicon arm64와
Intel x64는 하나의 universal ZIP으로 합치지 않고 각각 해당 RID로 DX Manager와
ADB proxy를 self-contained publish한다.

.NET single-file 내부 압축은 사용하지 않는다. macOS arm64 self-contained
실행 파일을 같은 추출 캐시에서 연속 실행할 때 내부 압축을 켠 빌드에서
`System.AccessViolationException`을 반복 재현했고, 압축만 끈 같은 빌드는
연속 실행을 통과했다. 실행 파일은 계속 single-file로 publish하며 최종 ZIP의
표준 압축은 유지한다.

저장소의 개발용 `tools`에는 x86 전용 파일이 있을 수 있으므로 공개 패키징에서
그 폴더를 무조건 복사하지 않는다. 공식 scrcpy 4.1 macOS 정적 아카이브를
아키텍처별 URL과 SHA-256으로 고정해 넣고, 번들 도구를 PATH와 Homebrew보다 먼저
사용한다. GitHub Actions도 실제 아키텍처가 일치하는 `macos-15`와
`macos-15-intel`에서 별도로 빌드·실행 검증한다.

DeX overlay를 만들거나 제거하기 직전에는 선택 화면에 남은 identity만 신뢰하지
않고 해당 ADB transport에서 안정적인 물리 identity를 다시 읽는다. 이미 알고 있던
안정 identity와 다르면 같은 Wi-Fi endpoint를 다른 휴대폰이 재사용한 것으로 보고
작업을 중단한다. 처음 관찰한 identity가 임시 `transport:*` 값이면 live 조회로
안정 identity를 얻은 경우에만 세션을 시작한다.

세션을 시작한 transport가 사라졌더라도 같은 안정 identity의 다른 authorized
transport가 연결돼 있으면 그 현재 serial에서 overlay reset을 수행한다. 예를 들어
USB로 시작한 뒤 USB가 끊기고 같은 휴대폰의 Wi-Fi ADB가 남은 경우에는 Wi-Fi
transport의 live identity를 다시 확인한 뒤 정리한다. 같은 endpoint의 다른
identity나 identity를 확인할 수 없는 연결에는 정리 명령을 보내지 않는다.

연결이 끊겨 overlay를 제거하지 못한 기록은 serial 하나를 키로 덮어쓰지 않고
물리 identity와 lease를 함께 가진 독립 항목으로 보존한다. 같은 identity의 새
transport에서 `EnsureVirtualDisplay`가 성공하거나 명시적 reset이 성공한 뒤에만
해당 보류 기록을 완료 처리한다. 시작 전, identity 미확인 상태 또는 다른 기기에
명령이 전달된 상태에서는 보류 기록을 삭제하지 않는다.

Apple Developer ID와 notarization 자격 증명은 저장소에 넣지 않는다. 인증서가
구성되기 전 자동 artifact에는 Developer ID 서명·공증이 없음을 문서에 표시하고
Gatekeeper를 자동으로 우회하지 않는다.

## 2026-10-25 public source integration

Keep the Windows .NET Framework proxy/foundation projects intact. Preserve the public .NET 8 Mac versions in sibling .Mac directories, with Mac solution/package/workflow references updated. Preserve all remote-only Mac code/tools and the scanned Windows release bytes. Export a pinned official Companion snapshot while keeping one canonical Android source.

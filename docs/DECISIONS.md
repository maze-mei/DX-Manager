# Decision Log

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

# Technical Notes

## 주요 구조

`Program.cs`가 설정을 읽고 서비스를 조립한다.

- `PathService`: OS/설정별 ADB 선택
- `AdbService`: target serial을 포함한 ADB 실행
- `WirelessAdbService`: USB 준비, TCP/IP, 페어링, 재연결
- `VirtualDisplayService`: DeX overlay와 ID 탐색
- `DexOrchestrator`: DeX 실행/정리
- `ScrcpyService`: DeX Scrcpy
- `SingleWindowService`: 단일창 3개
- `ScrcpyLaunchCoordinator`: Scrcpy 시작 직렬화
- `ScreenOffService`: 남은 세션의 화면 OFF 재적용
- `KeyMappingService`: Scrcpy 활성 상태 키 보정
- `CaptureCoordinator`: F8 캡처

## ADB

모든 명령은 선택한 `adb.exe`의 절대 경로로 실행한다.

1. 수동 모드면 지정 ADB 사용
2. Windows 10 미만이면 `tools\adb\legacy\adb.exe`
3. Windows 10 이상이면 선택한 Scrcpy 폴더의 `adb.exe` 사용
4. Scrcpy 폴더의 ADB가 없거나 실행되지 않으면 legacy ADB 사용

프로세스 환경 변수 `ADB`도 같은 절대 경로로 설정한다.

일부 Windows 7 환경에서는 `adb start-server` 직후 ADB 프로세스가 반복
종료되거나 USB transport를 정상적으로 잡지 못할 수 있다. DX Manager는
기본적으로 ADB로 먼저 깨우고, 실패 시 설정에 따라 Scrcpy 기반 wake-up을
사용해 실제 push/shell/stream 경로까지 열어 ADB 연결을 초기화한다.

현재는 ADB 상태와 기기 이름을 즉시 확인한 뒤, DeX/단일창 실제 시작 명령
직전에 `ConnectedStartDelayMs`를 적용한다. 범위는 0~60초, 기본값은 1초다.
화면 OFF 재적용용 Scrcpy에는 이 대기를 적용하지 않는다.

## 무선 ADB

- 승인된 USB 장치가 정확히 하나일 때 준비한다.
- IP가 비어 있으면 `adb -s SERIAL shell ip route`의 `wlan` 경로에서
  `src` IPv4 주소를 우선 찾는다.
- `adb -s SERIAL tcpip PORT` 후 USB를 꽂아둔 채 `adb connect`한다.
- 성공 시 target serial을 `IP:PORT`로 바꾼다.
- 자동 재연결은 최소 5초 간격이다.
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
`none`으로 제거한 뒤 다시 만든다. 정상 종료 시에는 생성 주체와 무관하게
관리 기기의 overlay를 `none`으로 정리한다.

## 단일창

단일창은 DeX overlay가 아니라 Scrcpy가 직접 만든다.

```text
--new-display=WIDTHxHEIGHT/DPI
--start-app=PACKAGE
--window-title "APP NAME"
```

슬롯마다 프로세스와 설정을 따로 관리한다. 강제 종료는
`--start-app=+PACKAGE`이며 기본값은 꺼짐이다. 앱 이름과 패키지를 함께
저장해 명령에는 패키지, UI와 창 제목에는 이름을 쓴다.

## Scrcpy와 화면 상태

옵션은 각 프로필 설정에 따라 붙는다.

- HID 키보드 `-K`
- HID 마우스 `-M`
- 화면 끄기 `-S --no-power-on`
- Scrcpy 4.0 잠자기 방지 `--keep-active`
- Scrcpy 3.3.4 잠자기 방지 `-w`
- Scrcpy 4.0 단일창 동적 크기 `--flex-display`

시작 시 `scrcpy --version`을 실행해 Scrcpy와 SDL 주 버전을 저장한다.
Scrcpy 3.3.4에서는 지원하지 않는 `--keep-active`와 `--flex-display`를
전달하지 않는다. Scrcpy 4.0/SDL3 출력은 UTF-8, 3.3.4/SDL2와 구형 ADB의
로컬 경로 출력은 Windows 기본 코드 페이지로 읽는다.

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
- 한영키는 `scanCode == 0xF2`로 감지한다.
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

## 캡처, 숨김, 설정

F8 캡처는 Scrcpy를 전면으로 가져오고 오버레이를 표시한다. 캡처 직전
오버레이를 숨기고 약 100ms 기다린다. 전체 캡처는 client area만 찍는다.

자동 숨김은 설정 시간 동안 입력이 없으면 Scrcpy와 모든 UI를 숨겨 트레이로
보낸다. 이후 입력만으로 자동 복구하지 않는다.
Scrcpy 프로세스가 종료되는 순간과 타이머가 겹쳐도 프로세스 참조를 한 번만
고정해 읽으며, 타이머 예외는 UI 스레드 밖으로 전파하지 않고 로그만 남긴다.

- 설정: 실행 폴더의 `config\settings.json`
- 현재 스키마: 18
- DPI 입력 범위는 120~640이며 120 미만은 입력 확정 시 120으로 복원한다.
- 사용자 지정 해상도는 프리셋과 별도로 보존
- 성공적으로 자동 실행한 앱은 최근 20개까지 공통 목록으로 보존
- 로그는 현재 실행 세션만 유지하고 수동으로 저장
- 경로/추가 인자 필드는 커스텀 프레임 안의 네이티브 TextBox를 사용해
  드래그 선택, 긴 문자열 가로 스크롤, `Ctrl+Z`와 IME를 지원

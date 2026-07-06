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
3. Windows 10 이상이면 번들 modern ADB와 외부 Scrcpy 폴더의 ADB를
   비교해 외부가 같거나 더 최신일 때 외부 ADB 사용

프로세스 환경 변수 `ADB`도 같은 절대 경로로 설정한다.

## 무선 ADB

- 승인된 USB 장치가 정확히 하나일 때 준비한다.
- IP가 비어 있으면 `adb -s SERIAL shell ip route`의 `wlan` 경로에서
  `src` IPv4 주소를 우선 찾는다.
- `adb -s SERIAL tcpip PORT` 후 USB를 꽂아둔 채 `adb connect`한다.
- 성공 시 target serial을 `IP:PORT`로 바꾼다.
- 자동 재연결은 최소 5초 간격이다.
- 페어링 포트와 실제 연결 포트는 다를 수 있다.
- 페어링 코드는 저장하거나 로그에 남기지 않는다.

## DeX display ID

DeX는 `overlay_display_devices`로 만든다. 생성 전후 `dumpsys display`
스냅샷의 차집합을 구하고, 후보가 여러 개면 해상도/DPI로 좁힌다.
여전히 모호하면 실패시키고 후보를 로그에 남긴다. 최대 ID fallback은 없다.

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
- 잠자기 방지 `--keep-active`
- 단일창 동적 크기 `--flex-display`

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
- Enter 변환 기능의 시작 상태는 일반 Enter다.
- Scroll Lock으로 일반 Enter/Shift+Enter를 전환한다.
- SendInput 실패 시 ADB keycombination fallback이 있다.
- 오른쪽 Alt는 한영키와 분리한다.

Win32 `INPUT` 구조체 정렬을 임의로 바꾸면 32비트 Windows에서 SendInput이
실패할 수 있다.

## 캡처, 숨김, 설정

F8 캡처는 Scrcpy를 전면으로 가져오고 오버레이를 표시한다. 캡처 직전
오버레이를 숨기고 약 100ms 기다린다. 전체 캡처는 client area만 찍는다.

자동 숨김은 설정 시간 동안 입력이 없으면 Scrcpy와 모든 UI를 숨겨 트레이로
보낸다. 이후 입력만으로 자동 복구하지 않는다.

- 설정: 실행 폴더의 `config\settings.json`
- 현재 스키마: 16
- 사용자 지정 해상도는 프리셋과 별도로 보존
- 성공적으로 자동 실행한 앱은 최근 20개까지 공통 목록으로 보존
- 로그는 현재 실행 세션만 유지하고 수동으로 저장

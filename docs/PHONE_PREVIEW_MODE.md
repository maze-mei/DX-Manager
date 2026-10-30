# Hide the phone DeX preview (Windows, experimental)

## English

Feature integration, per-device lifecycle and regression tests contributed by
[**asyouplz**](https://github.com/asyouplz). Samsung loopback activation follows
ScrcpyDeX; its original copyright and Apache-2.0 notices are preserved.

This optional Windows mode keeps Samsung DeX in the PC window while allowing
normal home-screen and app use on the phone, without the simulated-display
preview. It is off by default and saved separately for each physical phone.
The implementation is present; actual Galaxy/One UI behavior remains unverified.

### Use

1. Select the target phone in DX Manager and stop DeX.
2. Enable **Hide phone preview** (experimental) in the DeX launch options.
3. Start DeX. To return, stop DeX, disable the option and start again.

The bundled, hash-checked helper runs through the selected absolute-path ADB.
The physical phone identity is rechecked before transfer and execution; an
unverified or reassigned endpoint is rejected.
It requests a Samsung wireless DeX connection to the phone's own `127.0.0.1`;
scrcpy carries the actual PC video, audio and input over the selected USB or
wireless ADB connection. No root, additional APK, Java installation or Companion
permission change is required. The helper checks for Samsung Android 12/API 31
or newer and the required internal APIs; those checks do not prove compatibility.
The activation method follows [ScrcpyDeX](https://github.com/Aureliano021/ScrcpyDex).
Source, Apache-2.0 attribution and reproducible build instructions are in
`DXLoopback/` in the source repository; helper notices accompany the bundled JAR.

### Screen settings and termination

- The phone-screen-off preference is retained but ignored by this DeX mode.
  Additional scrcpy power-control arguments are rejected. A hidden DeX or 120 FPS session also suppresses screen-off requests from other
  DX Manager windows on the same physical phone. Phone auto-lock remains independent.
- Samsung negotiates the display mode. Resolution/DPI controls are disabled,
  and their stored values return when the option is disabled.
- Another active HDMI, wireless DeX or simulated display must be stopped by
  its owner first. Unsupported firmware or activation failure produces an
  explanation and stops; there is no automatic switch to the overlay workflow.
- The PC sends a session-specific heartbeat every 3 seconds. Stop, control-channel
  EOF or loss of that signal for 15 seconds starts phone-side cleanup of the
  matching DeX connection. This covers cable loss and host-process termination
  without needing a new ADB command after disconnection. Each session has its
  own token and helper channel; another phone's session is preserved.
- The 15 seconds is a cleanup trigger, measured from the last heartbeat, rather
  than a guaranteed firmware completion time. Local process exit does not prove
  phone cleanup. An unconfirmed result is logged when available; after a cable
  loss or host crash the PC may be unable to receive that diagnostic. Check the
  phone's DeX/Smart View disconnect controls if a connection remains.
- The helper disconnects only when its own generated display name and address
  still match, and deletes only its own temporary JAR. Battery, heat, audio,
  sleep/wake and long-running behavior require physical testing. No performance
  improvement is claimed. The existing Companion recovery behavior is separate.

### Physical acceptance checklist — not yet performed

Record Windows version, Galaxy model, Android and One UI versions, chosen ADB
transport, and time from disconnection to actual DeX disappearance.

| Scenario | Required observation | Status |
| --- | --- | --- |
| Windows 11 / Windows 7 SP1 | Native build and actual DeX operation | Unverified |
| Hidden mode on | PC shows Samsung DeX; phone home/apps work without preview | Unverified |
| Hidden mode off | Existing overlay behavior and saved screen/DPI settings return | Unverified |
| USB / wireless disconnect | Phone attempts cleanup, nominally within about 15 s | Unverified |
| Stop / close scrcpy / exit manager / kill manager | Only owned DeX session ends | Unverified |
| Repeated disconnect/reconnect and startup cancellation | No delayed old start or stale helper | Unverified |
| Two phones and simultaneous single-app window | Other sessions, input and transfers remain isolated | Unverified |
| Keyboard, mouse, audio, file transfer, lock/sleep/wake | Usable operation; limitations recorded | Unverified |

## 한국어

이 기능의 DX Manager 통합, 기기별 수명주기 처리와 회귀 검사는
[**asyouplz**](https://github.com/asyouplz)가 기여했습니다. ScrcpyDeX 기반의
삼성 연결 방식은 원저작자 저작권과 Apache-2.0 표기를 유지합니다.

Windows에서 선택적으로 사용하는 실험 기능입니다. PC의 DeX 창은 유지하면서
휴대폰에서는 작은 DeX 미리보기 없이 홈 화면과 일반 앱을 사용할 수 있도록
구현했습니다. 기본값은 꺼짐이며 선택은 물리 휴대폰별로 저장합니다. 실제
Galaxy와 One UI에서의 동작 확인은 아직 필요합니다.

### 사용 순서

1. DX Manager에서 대상 휴대폰을 선택하고 DeX를 중지합니다.
2. DeX 실행 옵션의 **휴대폰 미리보기 숨김** (실험 기능)를 켭니다.
3. DeX를 시작합니다. 기존 방식으로 돌아가려면 중지한 뒤 옵션을 끄고 다시 시작합니다.

선택된 휴대폰의 고유 식별값을 전송 전과 실행 직전에 다시 확인하고, 다른
기기이거나 확인되지 않는 연결에서는 시작하지 않습니다. 번들 보조 프로그램이 휴대폰 내부 주소 `127.0.0.1`로 삼성 무선 DeX 연결을
만듭니다. 실제 PC 화면·오디오·입력은 선택된 USB 또는 무선 ADB를 통해
기존 scrcpy가 처리합니다. 추가 APK·루팅·Java 설치나 Companion 권한 변경은
필요하지 않습니다. Android 12/API 31 이상 삼성 기기와 내부 API 존재를
검사하지만, 이 조건만으로 실제 호환성을 보장하지 않습니다.

### 화면 설정과 자동 종료

- 휴대폰 화면 끄기 설정은 지우지 않고 이 모드에서만 적용하지 않습니다.
  화면 전원을 제어하는 scrcpy 추가 인수는 거부합니다. 휴대폰의 자동 잠금이나
  숨김 DeX 또는 120 FPS 실행 중에는 같은 휴대폰의 다른 DX Manager 창에서 요청하는 화면 끄기도 적용하지 않습니다. 휴대폰 자체 자동 잠금은 별도입니다.
- 해상도와 DPI는 삼성 DeX가 협상합니다. 기존 입력은 비활성화하고 저장값을
  보존하며, 옵션을 끄면 다시 사용할 수 있습니다.
- 다른 HDMI·무선 DeX·보조 화면이 사용 중이면 직접 중지해야 합니다.
  지원되지 않는 펌웨어나 시작 실패는 이유를 표시하고 중단하며 기존 방식으로
  자동 전환하지 않습니다.
- PC는 3초마다 생존 신호를 보냅니다. 중지 요청이나 제어 연결 종료를 감지하거나
  마지막 신호로부터 15초가 지나면 휴대폰 보조 프로그램이 자신의 DeX 연결을
  정리합니다. 케이블 분리 후 PC가 새 명령을 보낼 수 없어도 동작하도록 구성했습니다.
- 15초는 정리 시작 기준이며 삼성 시스템의 실제 종료 완료 시간은 실기로
  측정해야 합니다. PC 프로세스 종료만으로 휴대폰 정리 성공을 판단하지 않습니다.
  정리 미확인 응답을 받으면 로그로 남기지만 케이블 분리·강제 종료 후에는
  응답 자체를 받지 못할 수 있습니다. 연결이 남으면 휴대폰의 DeX/Smart View
  연결 해제 화면에서 확인합니다.
- 기기·세션별 토큰과 연결을 분리하고 자신이 생성한 이름과 주소가 일치할 때만
  종료합니다. 다른 휴대폰이나 외부 화면을 일괄 종료하지 않습니다.
  발열·배터리·절전 복귀·장시간 사용은 별도 확인 대상입니다.

위 표의 실기 항목은 모두 미확인입니다. Windows 11과 Windows 7 SP1에서
모델·Android·One UI·연결 방식·실제 종료 시간을 함께 기록한 뒤 호환성을
판정합니다. 자동 테스트와 교차 컴파일 성공은 실기 성공을 뜻하지 않습니다.

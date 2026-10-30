# Experimental loopback DeX helper

## English

DX Manager integration, lifecycle controls and tests by [asyouplz](https://github.com/asyouplz).
Original Samsung activation technique: ScrcpyDeX; see NOTICE and LICENSE.

This small Android `app_process` helper supports the Windows **Hide DeX on phone
(experimental)** mode. It requests a Samsung wireless DeX display connected to
`127.0.0.1`, completes its WFD/RTSP negotiation and discards the dummy RTP traffic.
DX Manager still uses its existing scrcpy process for the visible PC window,
audio and input. The helper does not create an `overlay_display_devices` preview,
install an APK, request root, grant permissions, alter Wi-Fi settings or expose a
network listener to other devices.

This is **not a confirmed Samsung S26 compatibility guarantee**. Samsung hidden
APIs, display status identity and desktop mode state are firmware-dependent.
The helper requires Samsung Android 12/API 31 or newer and the expected Samsung
APIs, but passing that check does not establish device compatibility. A Windows +
Galaxy test must confirm separate phone use, PC DeX, audio/input, cancellation,
cable loss, process exit, repeated starts and no interference with another phone.
Samsung may still encode the local dummy wireless stream alongside scrcpy; heat,
battery use and audio routing must therefore be checked during device validation.

### Host contract

DX Manager pushes the bundled jar to its own session path and holds an ADB shell
with redirected stdin/stdout open:

```text
CLASSPATH=/data/local/tmp/dxm-loopback-<token>.jar app_process / com.dxmanager.loopback.Main <token>
```

`<token>` must be a fresh 32-character lowercase hexadecimal session identifier.
The host sends `PING <token>` every 3 seconds, starting during activation, and
`STOP <token>` on normal shutdown. EOF, malformed control or no heartbeat for
15 seconds expires the lease. The host must keep stdin open, parse output by line,
and never treat a log message or the highest display ID as readiness.

```text
DXM_LOOPBACK_READY <positive display ID>
DXM_LOOPBACK_ERROR <safe_reason>
DXM_LOOPBACK_STOPPED
```

Readiness requires a matched RTSP PLAY response, the uniquely new WIFI display in
a before/after snapshot, and the active Samsung display's exact per-run name and
MAC address. Some firmware does not expose identity until negotiation finishes,
so absent identity is tolerated while negotiating, never when reporting READY.
Activation is bounded to 20 seconds after the connect request. A local lock
serializes this helper on one phone; it does not lock or stop other phone sessions.

Existing external/WIFI/overlay displays, existing or transitioning Samsung
desktop mode, and an occupied TCP port 7236 are refused. Existing virtual app
displays are not reused. There is no disconnect-before-connect, `pkill`, global
scrcpy termination or overlay deletion. Cleanup only asks Samsung to disconnect
when both the active display name and MAC still match this helper, closes owned
sockets, and deletes only `/data/local/tmp/dxm-loopback-<token>.jar`.
If firmware no longer exposes verifiable ownership, no global disconnect is
attempted; recovery must be checked on the phone. The per-device lock file is
intentionally retained: removing a locked file could allow two helpers to run.

### Rebuild and verify

End users receive `tools/loopback/dxm-loopback.jar`; they need neither Java nor an
Android SDK. Rebuilding requires JDK 17 (the checked-in artifact was built with
Eclipse Temurin 17.0.16+8), and Google R8/D8 8.7.18. Source uses Java 8 APIs and
Android reflection, so no SDK jar or Gradle dependency is needed.

```powershell
.\scripts\Build-DexLoopback.ps1 -JavaHome 'C:\build-tools\jdk-17'
```

```bash
DXM_JAVA_HOME=/path/to/jdk-17 bash scripts/Build-DexLoopback.sh
```

Supply `-R8Jar` / `DXM_R8_JAR` for an offline build. Otherwise the script downloads
only the pinned compiler from Google's Maven repository and validates SHA-256:

```text
https://dl.google.com/dl/android/maven2/com/android/tools/r8/8.7.18/r8-8.7.18.jar
58366f77067207c39a17d469de7b05701d2877212a9c55201bcb0af43e59e903
```

The scripts compile with warnings as errors, run 18 host-side protocol/policy
tests, compile DEX with minimum API 31, then build a timestamp-normalized archive
containing only `classes.dex`, `LICENSE` and `NOTICE`. Tests cover bounded RTSP
framing, truncated/duplicate messages, PLAY correlation, loopback-only URLs,
heartbeat/STOP/EOF, ownership and unique display selection. They do **not** emulate
Samsung firmware or confirm actual Windows/device cleanup. The adjacent
`dxm-loopback.jar.sha256` is a lowercase SHA-256 plus the artifact filename.
Use `-OutputDirectory` / `DXM_LOOPBACK_OUTPUT` for a comparison build without
overwriting the bundled artifact. Temporary build folders are printed for audit.

ScrcpyDeX's activation approach is adapted under Apache-2.0. See `NOTICE` and
`LICENSE` for the pinned source, original attribution and changes. This separate
helper's Apache-2.0 license does not replace the main application's MIT license.

## 한국어

이 보조 프로그램은 윈도우의 **휴대폰에 DeX 숨기기(실험적)** 선택 기능에
사용한다. 삼성 무선 DeX를 휴대폰 내부 주소 `127.0.0.1`로 연결하고 초기
통신만 유지한다. 실제 PC 화면·오디오·입력은 기존 scrcpy가 처리한다.
휴대폰의 작은 보조 화면을 만들거나, APK 설치·루팅·권한 부여·Wi-Fi 설정
변경을 하지 않는다. 다른 장치에 통신 포트를 열지 않는다.

**S26에서의 동작을 확인했다는 의미는 아니다.** 삼성 내부 API와 표시되는
연결 정보는 펌웨어마다 달라질 수 있다. Android 12/API 31 이상 삼성 기기와
필요한 API를 확인하지만, 이 조건만으로 호환성을 보장하지 않는다. 실제
윈도우와 갤럭시에서 휴대폰 별도 사용, PC DeX, 음성·입력, 시작 취소,
케이블 분리, 종료, 재시작과 복수 휴대폰 간 간섭 여부를 확인해야 한다.
삼성 쪽에서 내부 무선 영상도 인코딩할 수 있으므로 발열·배터리 소모·오디오
출력 경로도 실기 확인 대상이다.

실행마다 새로운 32자리 소문자 16진수 식별자를 사용한다. PC는 3초마다
생존 신호를 보내고 정상 종료할 때 중지 신호를 보낸다. 입력 연결 종료나
15초 동안의 신호 누락은 세션 종료로 처리한다. 시작 전후 디스플레이 목록,
무선 디스플레이 종류, 해당 실행의 이름과 MAC 주소, RTSP 시작 응답이 모두
일치해야 화면 ID를 반환하며, 가장 큰 ID를 추측해 사용하지 않는다.

이미 외부 화면·보조 화면·무선 DeX 또는 삼성 데스크톱 모드가 실행 중이면
기존 연결을 끊지 않고 새 실행을 거부한다. 종료 때도 현재 연결의 이름과
MAC 주소가 이번 실행과 일치할 때만 연결 해제를 요청한다. 다른 scrcpy나
휴대폰을 일괄 종료하지 않으며, 이번 실행의 임시 JAR만 삭제한다. 펌웨어가
소유 정보를 제공하지 않으면 임의 연결 해제를 하지 않으므로 휴대폰에서
정리 결과를 별도로 확인해야 한다.

사용자는 이미 빌드된 `tools/loopback/dxm-loopback.jar`를 받으므로 Java나
Android SDK를 설치할 필요가 없다. 개발자가 다시 빌드할 때만 위 명령과
JDK 17이 필요하며, 고정 해시를 검증한 D8 8.7.18을 사용한다. 자동 테스트
18개는 통신 형식과 세션 정책 검증이고 실제 삼성 기기 검증을 대신하지
않는다. 원본 ScrcpyDeX의 출처와 Apache-2.0 라이선스는 `NOTICE`, `LICENSE`
및 배포 JAR 안에 함께 포함한다.

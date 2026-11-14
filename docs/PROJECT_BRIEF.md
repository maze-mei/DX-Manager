# DX Manager project brief

## English — Windows v3.0.0

| Area | Contract |
| --- | --- |
| Product | Windows v3.0.0, WinForms, .NET Framework 4.6.2, x64 |
| Intended OS range | Windows 7 SP1/8.1/10/11; legacy ADB on older systems |
| Runtime | scrcpy 5.0.1 with `--hwdec=auto` by default |
| Android components | DX Companion 3.0.0 (code 16), Android 13 or later, and Native DeX helper 13 |
| Delivery | `DX-Manager-win-x64.zip` contains `DX Manager v3.0.0/` and 40 files |
| Product code checkpoint | `224d740` |
| Source integration | Public main's Mac implementation preserved; official Companion snapshot included |
| Verification | [Release checksums and handoff](HANDOFF.md) |
| Other platforms | Store WinUI and macOS .NET 8 use separate projects |

### Behavior to preserve

- Stable physical identity joins that phone's USB/Wi-Fi transports. Each phone owns its runtime/settings/transfers.
- New starts prefer authorized USB; live sessions retain captured serials. USB loss does not silently migrate a window to Wi-Fi.
- DeX and three app slots can coexist; start app is optional for DeX and required for each slot.
- Native DeX is the default for new settings. It allows about 30 seconds to reconnect
  to the same phone. An accepted 120 FPS launch uses simulated display for that session.
- Manual Enter switching is on in new settings. Automatic line breaks use optional
  app switch detection in DX Companion.
- Per-phone/mode/app profile settings preserve nullable prior screen-off state at experimental120 FPS.
- Warn once before every actual120 start, default Cancel, before launch I/O. Keep the phone panel on; no injected activity/forced refresh-rate change.
- A phone with any live120 session defers ordinary screen-off requests; the last120 exit allows ordinary requests to resume.
- Keep process-start shutdown gates short and preserve Unicode/empty-folder/collision-safe transfers.
- Input/capture/mini bars target the explicit focused/owned session. Right Shift compatibility correction remains.
- Main-to-tray closes attached manager dialogs; scrcpy sessions continue. Idle auto-hide also minimizes scrcpy.
- Daily update attempts are persisted before background networking; offline does not loop. Manual checks remain available.
- Separate local changes history and safety consent survive ordinary settings reset; fresh packages contain neither.
- Companion protection may run while the manager is idle/connected. Verified disconnected-and-clean state stops it; remaining recovery work retains the transport/delay policy.
- Normal exit cleans device state. Windows shutdown starts no new ADB helper and uses an established authenticated Companion channel best-effort.

### Release preparation boundaries

The README shows Native DeX settings with sample device data.
The public source includes the verified Companion snapshot and separate macOS projects.
See [Building](BUILDING.md), [known constraints](KNOWN_ISSUES.md) and [current TODO](TODO.md).

## 한국어

현재 기준은 Windows v3.0.0, scrcpy 5.0.1과 공용 DX Companion 3.0.0입니다.
휴대폰별 세션 분리·USB 새 실행 우선·실행 중 serial 유지·단일창 시작 앱 필수,
120 실행 경고·화면 켜짐 우선·이전 설정 복원, Unicode 전송·입력·트레이·정리 규칙을 유지합니다.
네이티브 DeX는 새 설정의 기본 모드입니다. 120 FPS로 실행할 때는 보조 디스플레이를 사용합니다.
Windows 릴리스 파일과 검증 정보는 [인계 문서](HANDOFF.md)에서 확인할 수 있습니다.

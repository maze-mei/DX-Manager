# DX Manager v3.0.1

[Download for Windows x64](https://github.com/maze-mei/DX-Manager/releases/download/v3.0.1/DX-Manager-win-x64.zip)

## English

I fixed Native DeX going black when the phone's screen timeout expired while
connected over Wi-Fi on battery power. Enable **Stay awake** before starting
DeX to keep using it with the phone screen off and the charger disconnected.

I also fixed switching to Native DeX while a simulated secondary display is
still on the phone. DX Manager now removes that simulated display and waits
for cleanup before starting Native DeX. Virtual displays created by scrcpy
remain available.

I updated the English and Korean guides, FAQs and startup messages for these
changes. This release bundles scrcpy 5.0.1 and the shared DX Companion 3.0.0
(code 16). If you already have that Companion version, you can keep using it.

The Windows executable passed antivirus testing.

Extract the **DX Manager v3.0.1** folder and run **DXManager.exe**.

[User guide](https://github.com/maze-mei/DX-Manager/blob/v3.0.1/docs/USER_GUIDE_EN.md) · [FAQ](https://github.com/maze-mei/DX-Manager/blob/v3.0.1/docs/FAQ_EN.md) · [Build from source](https://github.com/maze-mei/DX-Manager/blob/v3.0.1/docs/BUILDING.md)

## 한국어

충전기를 연결하지 않고 Wi-Fi로 네이티브 DeX를 사용할 때, 휴대폰의 화면 자동
꺼짐 시간에 맞춰 DeX 화면도 검게 변하는 문제를 고쳤습니다. DeX 실행 전에
**잠자기 방지**를 켜면 충전기를 빼고 휴대폰 화면을 꺼도 계속 사용할 수 있습니다.

휴대폰에 보조 디스플레이가 남아 있을 때 네이티브 DeX를 실행하면 오류가 나던
문제도 고쳤습니다. 이제 보조 디스플레이를 정리하고 화면이 사라진 뒤 네이티브
DeX를 실행합니다. scrcpy가 만든 가상 화면은 유지합니다.

이 변경에 맞춰 한글과 영어 사용설명서, FAQ와 실행 안내 문구를 정리했습니다.
scrcpy 5.0.1과 공용 DX Companion 3.0.0(code 16)을 포함했습니다.
해당 버전의 Companion 앱이 설치돼 있다면 그대로 사용하시면 됩니다.

Windows 실행 파일은 바이러스 검사를 통과했습니다.

**DX Manager v3.0.1** 폴더를 통째로 풀고 **DXManager.exe**를 실행하면 됩니다.

[전체 사용설명서](https://github.com/maze-mei/DX-Manager/blob/v3.0.1/docs/USER_GUIDE_KO.md) · [FAQ](https://github.com/maze-mei/DX-Manager/blob/v3.0.1/docs/FAQ_KO.md) · [소스 빌드](https://github.com/maze-mei/DX-Manager/blob/v3.0.1/docs/BUILDING.md)

### SHA-256

`DX-Manager-win-x64.zip`

~~~text
ABA052A30E85F2462A239420B6D830177BD4C0E71016D0C702FAE2D2E33CFD1E
~~~

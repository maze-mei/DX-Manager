# DX Manager v3.0.0

[Download for Windows x64](https://github.com/maze-mei/DX-Manager/releases/download/v3.0.0/DX-Manager-win-x64.zip)

## English

I added Native DeX as the default mode for new settings. If the connection
drops, DX Manager gives the same phone about 30 seconds to reconnect and can
resume the session if the DeX display is still available.

- **Experimental 120 FPS.** Native DeX runs at up to 60 FPS. When you choose
  120 FPS and confirm, DX Manager uses a simulated display for that session
  with the phone screen on.
- **Bluetooth audio.** I added an option to restore the Bluetooth audio
  device you were using before starting Native DeX.
- **Enter key.** Scroll Lock switches between Enter and Shift+Enter when
  manual switching is enabled. I also added optional automatic line breaks
  for ChatGPT, Claude, Gemini and Grok through DX Companion, with a toggle
  for each app.
- **Settings and help.** I grouped related settings and updated the English
  and Korean user guides and FAQs.

This release bundles scrcpy 5.0.1 and DX Companion 3.0.0. I adapted code from
[ScrcpyDeX v1.1](https://github.com/Aureliano021/ScrcpyDex/tree/eebbba903b73d54bbd13210096ea28fe31e51200)
by Aureliano Peixoto and contributors for Native DeX under the Apache 2.0 license.

The Windows executable passed antivirus testing.

Extract the **DX Manager v3.0.0** folder and run **DXManager.exe**. If you use
DX Companion, update it under **Settings → Companion**.

[User guide](USER_GUIDE_EN.md) · [FAQ](FAQ_EN.md) · [Build from source](BUILDING.md)

## 한국어

이번 버전에서는 새 설정의 기본 실행 방식으로 네이티브 DeX를 넣었습니다.
연결이 끊기면 같은 휴대폰이 다시 연결될 때까지 약 30초 기다리고, DeX 화면이
남아 있으면 이어서 사용할 수 있습니다.

- **실험적 120 FPS.** 네이티브 DeX는 최대 60 FPS로 동작합니다. 120 FPS를 선택하고
  확인하면 그 세션에서만 보조 디스플레이를 사용하며, 휴대폰 화면을 켜둡니다.
- **Bluetooth 오디오.** 네이티브 DeX를 시작하기 전에 쓰던 Bluetooth 오디오 장치로
  소리를 돌리는 옵션을 넣었습니다.
- **Enter 키.** 수동 전환을 켜면 Scroll Lock으로 Enter와 Shift+Enter를 바꿀 수
  있습니다. DX Companion 앱을 이용해 ChatGPT, Claude, Gemini, Grok에서 앱별로
  자동 줄바꿈을 켤 수 있도록 했습니다.
- **설정과 도움말.** 관련 설정을 한곳에 모으고, 한글과 영어 사용설명서와 FAQ도
  최신 버전에 맞춰 정리했습니다.

scrcpy 5.0.1과 DX Companion 3.0.0을 포함했습니다. 네이티브 DeX에는
Aureliano Peixoto와 기여자의 [ScrcpyDeX v1.1](https://github.com/Aureliano021/ScrcpyDex/tree/eebbba903b73d54bbd13210096ea28fe31e51200)
코드를 Apache 2.0 라이선스에 따라 수정해 사용했습니다.

Windows 실행 파일은 바이러스 검사를 통과했습니다.

**DX Manager v3.0.0** 폴더를 통째로 풀고 **DXManager.exe**를 실행하면 됩니다.
DX Companion 앱을 사용하신다면 **설정 → Companion**에서 업데이트해 주세요.

[전체 사용설명서](USER_GUIDE_KO.md) · [FAQ](FAQ_KO.md) · [소스 빌드](BUILDING.md)

### SHA-256

`DX-Manager-win-x64.zip`

~~~text
BC82B14764A66B49B5B4255C8B54A5E1E6D20AD7F5C566CECB6C6D28F83E8389
~~~

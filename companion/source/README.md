# DX Companion

DX Companion 3.0.0 (code 16) is the shared Android app for the GitHub and
Store editions of DX Manager. It supports Android 13 or later.

The app sends files and folders to the PC, cleans up displays and Stay awake,
and shows the current Native DeX session. Its Quick Settings tile and home
screen widget use the cleanup targets selected in the app. App switch
detection can be enabled for AI app automatic line breaks.

Use **Settings → Companion** in DX Manager to install or update the signed
app and grant cleanup permission. The Android package and official signing
identity stay the same across updates. See [signing](SIGNING.md) for the
certificate fingerprint.

The shared build uses the Android project in this directory. Build with
JDK 17 and Android SDK 36. Run the shared Build-Companion.ps1 with a trusted
signing properties path to create and verify the official APK.

## 한국어

DX Companion 3.0.0(code 16)은 GitHub판과 스토어판에서 함께 사용하는 Android 앱입니다.
Android 13 이상에서 휴대폰에서 PC로 파일·폴더 보내기, 화면과 잠자기 방지 정리,
네이티브 DeX 상태 확인을 지원합니다. AI 앱 자동 줄바꿈을 쓸 때 앱 전환 감지를 켭니다.

DX Manager의 **설정 → Companion**에서 설치·업데이트와 정리 권한 부여를 진행합니다.
서명 정보는 [SIGNING.md](SIGNING.md)에서 확인할 수 있습니다.

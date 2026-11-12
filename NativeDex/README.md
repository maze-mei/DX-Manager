# Native DeX helper

The Windows v3.0.0 helper starts Samsung DeX on the phone and identifies its
display for scrcpy. The application controls its own session through a
token-authenticated local connection. Helper build 13 uses native wire
protocol 7 and phone loopback port 37125.

The Windows app can run Native DeX without the Companion app. The shared
Companion 3.0.0 release adds managed phone-side state, audio and power
integration. It uses a separate guardian protocol for app switch detection.
The native display has a reconnect grace of about 30 seconds after the
connection is lost. Explicit stop uses the owned session's cleanup path.

## Source and license

Seven Java files adapt the activation and context code from
[ScrcpyDeX v1.1](https://github.com/Aureliano021/ScrcpyDex/tree/eebbba903b73d54bbd13210096ea28fe31e51200)
by Aureliano Peixoto and ScrcpyDeX contributors. The original
[Apache License 2.0](vendor/LICENSE) accompanies the source.
[upstream.json](upstream.json) identifies the exact files, hashes and changes.

The package includes [the license](../DexManager/licenses/ScrcpyDex-APACHE-2.0.txt)
and [the modification notice](../DexManager/licenses/ScrcpyDex-NOTICE.txt).
The same texts are in the helper JAR under META-INF. The scrcpy streaming
client has its own license.

## Build

The Windows project includes the pinned helper JAR. To rebuild it, use JDK
17, Android SDK platform 36 and build-tools 36.0.0 with
[scripts/Build-NativeDexHelper.ps1](../scripts/Build-NativeDexHelper.ps1).
The script updates the helper SHA-256 used by the Windows app and
[build-info.json](build-info.json).

네이티브 DeX 화면 생성은 ScrcpyDeX v1.1의 Java 코드 일부를 수정해 사용합니다.
원본 라이선스와 수정 고지는 배포본의 licenses 폴더와 보조 프로그램 JAR에 있습니다.

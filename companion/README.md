# Official DX Companion source snapshot

## English

This is the **3.0.0 / versionCode 16** release snapshot used by DX Manager Windows v3.0.0.
It includes the Android source, generated PC constants, the exact official signed APK and its font license.
The package ID is `io.github.mazemei.dxdisplaycleanup`; minimum Android version is 13 (API 33).

Android code is maintained once in the canonical shared component used by the GitHub and Store editions.
This directory is a generated public snapshot, not a second independently maintained Android project.
Maintainers update the canonical component, then run `scripts/Export-CompanionSnapshot.ps1` and review the resulting diff.
The release manifest pins all 130 source files and the APK; validate with:

~~~powershell
./scripts/Verify-CompanionSnapshot.ps1
~~~

An ordinary Windows source checkout builds using this snapshot when the maintainer's sibling shared directory is absent.
An explicit MSBuild property or environment variable `CompanionSharedRoot` can select a verified component.
No external shared directory is required for a public checkout.

To test or build a personal Android debug APK, use JDK 17 and Android SDK 36:

~~~powershell
cd companion/source
./gradlew.bat testDebugUnitTest lintDebug assembleDebug
~~~

On macOS/Linux use `./gradlew`. The debug build uses a separate `.debug` package and a different signing certificate.
DX Manager deliberately rejects debug APKs for privileged official-Companion operations.
An unsigned release build cannot replace the official signed APK. Private signing inputs are not included.

The APK in `release/DX-Companion.apk` is identical to the one in the Windows ZIP.
See [release.json](release.json), [signing identity](source/SIGNING.md) and [changes](CHANGELOG.md).
The source project's older README/package notes are retained verbatim as historical source material; this page describes the current snapshot.

## 한국어

정식 Companion 3.0.0/code16의 소스·PC 연동 상수·서명 APK를 함께 공개한 릴리스 스냅샷입니다.
원본은 공용 컴포넌트 한곳에서 관리하고 수정한 뒤 다시 내보냅니다. 여기서 별도의 Android 원본을 유지하지 않습니다.
공개 저장소만 내려받아도 Windows를 빌드할 수 있으며 서명키·비밀번호·로컬 설정은 포함하지 않습니다.
일반 사용자는 Windows ZIP에 들어 있는 APK를 설정 > Companion에서 설치하면 됩니다.

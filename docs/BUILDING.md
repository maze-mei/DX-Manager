# Building DX Manager v3.0.0 for Windows

## English

The Windows project uses WinForms, .NET Framework 4.6.2 and x64. Install
MSBuild with the .NET Framework 4.6.2 targeting pack. The source tree
contains the pinned scrcpy 5.0.1 runtime, Native DeX helper JAR and signed
Companion 3.0.0 snapshot.

From the repository root in PowerShell:

~~~powershell
./scripts/Verify-CompanionSnapshot.ps1
./scripts/Sync-EmbeddedDocumentation.ps1 -CheckOnly
MSBuild.exe DexManager.sln /t:Build /p:Configuration=Release /m:1
./scripts/Build-FinalRelease.ps1 -SkipBuild
~~~

The executable is in DexManager/bin/Release. The last command packages
DX-Manager-win-x64.zip in the repository's dist directory, with a
DX Manager v3.0.0 folder inside. Use -OutputRoot for an empty destination
elsewhere. The ZIP includes runtime files, license texts and a short README.
The full guides and FAQ are embedded in the executable.

If MSBuild needs a reference assembly location, pass
/p:TargetFrameworkRootPath= followed by the root that contains
.NETFramework/v4.6.2. Regenerate embedded help with
./scripts/Sync-EmbeddedDocumentation.ps1 after editing the guide or FAQ.

### Native DeX helper and Companion snapshot

The Windows app links the included Companion metadata and uses the matching
APK. The snapshot can be verified without a signing key. Maintainers can
export an approved shared release using Export-CompanionSnapshot.ps1.
[DX Companion snapshot](../companion/README.md)

The native helper's source and Apache 2.0 notices are included. Its optional
rebuild uses PowerShell 7, JDK 17, Android SDK platform 36 and build-tools
36.0.0. Set JAVA_HOME and ANDROID_SDK_ROOT and run
./scripts/Build-NativeDexHelper.ps1 before rebuilding Windows.
[Native helper source and license](../NativeDex/README.md)

The maintainer regression suites use the built Windows executable and
synthetic phones. Verify-OfficialBuild.ps1 takes MSBuild from DXM_MSBUILD
or PATH. For full verification, set DXM_TEST_FOREIGN_APK to a separately
signed local APK test fixture and build the helper classes first. The
fixture stays out of the release ZIP.

macOS has a separate build. [macOS guide](MACOS_GUIDE.md)

## 한국어

Windows판은 WinForms, .NET Framework 4.6.2, x64를 사용합니다. MSBuild와
.NET Framework 4.6.2 대상 지정 팩을 설치하세요. scrcpy 5.0.1, 네이티브 DeX
보조 프로그램 JAR, 서명된 공용 Companion 3.0.0은 소스에 포함돼 있습니다.

저장소 루트에서 다음 명령으로 빌드하고 포장합니다.

~~~powershell
./scripts/Verify-CompanionSnapshot.ps1
./scripts/Sync-EmbeddedDocumentation.ps1 -CheckOnly
MSBuild.exe DexManager.sln /t:Build /p:Configuration=Release /m:1
./scripts/Build-FinalRelease.ps1 -SkipBuild
~~~

실행파일은 DexManager/bin/Release에 생성됩니다. 마지막 명령은 dist에
DX-Manager-win-x64.zip을 만들며, 압축 안에 DX Manager v3.0.0 폴더가 있습니다.
다른 비어 있는 위치에 포장하려면 -OutputRoot를 지정하세요. ZIP에는 실행 파일,
번들 도구, 라이선스와 짧은 README가 들어갑니다. 전체 사용설명서와 FAQ는 앱에서 봅니다.

사용설명서나 FAQ를 수정한 뒤에는 Sync-EmbeddedDocumentation.ps1로 내장 문서를
갱신합니다. 참조 어셈블리 경로가 필요하면 .NETFramework/v4.6.2를 포함하는 루트를
/p:TargetFrameworkRootPath=로 전달합니다.

공용 APK는 포함된 스냅샷과 해시를 사용합니다. 네이티브 보조 프로그램을 다시 빌드할
때는 PowerShell 7, JDK 17, Android SDK 플랫폼 36과 build-tools 36.0.0을 준비합니다.
JAVA_HOME과 ANDROID_SDK_ROOT를 설정한 뒤 Build-NativeDexHelper.ps1을 실행하고
Windows판을 다시 빌드합니다.
[공용 Companion](../companion/README.md) · [보조 프로그램 소스와 라이선스](../NativeDex/README.md)

macOS는 별도 빌드입니다. [macOS 가이드](MACOS_GUIDE.md)

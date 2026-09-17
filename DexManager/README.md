# DX Manager Source and Build Notes

[Repository overview](../README.md) ·
[English user guide](../docs/USER_GUIDE_EN.md) ·
[한국어 사용 설명서](../docs/USER_GUIDE_KO.md)

## Development Environment

- Visual Studio 2019
- .NET Framework 4.6.2 targeting pack
- C# WinForms
- No external NuGet packages

Open `DexManager.sln` from the repository root and build the `Debug` or
`Release` configuration.

## Output

Build output is written to:

```text
DexManager/bin/Debug
DexManager/bin/Release
```

Keep `bin/Release` as the developer build output. From the repository root,
run the packaging script to create the user-facing folder and ZIP:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Package-Release.ps1
```

The script rebuilds Release and writes `dist/DX Manager` and
`dist/DX-Manager-v2.0.1-win-x64.zip`. Use `-SkipBuild` only when the current
Release output has already been verified. `ExecutionPolicy Bypass` applies
only to this process and does not change the system policy.

If the .NET Framework 4.6.2 Developer Pack is not installed system-wide, pass
the directory containing `.NETFramework\v4.6.2` as
`-TargetFrameworkRootPath`, or set `DXM_TARGET_FRAMEWORK_ROOT` for the current
shell.

The application is portable, but `DXManager.exe` is not standalone. A release
package must include:

- `DXManager.exe` and `DXManager.exe.config`
- `tools/scrcpy` and all of its runtime files
- `tools/adb`
- `tools/adb-proxy/DXMAdbProxy.exe`
- the `licenses` directory, including `THIRD_PARTY_NOTICES.md`
- the HTML-free portable `README.md` generated from `docs/PACKAGE_README.md`
- `LICENSE`, the user guides, FAQs, and guide images

The package script uses this allowlist and deliberately excludes PDB files and
runtime `config`, `logs`, and `screenshot` data. It refuses to run while DX
Manager is open, stops the bundled Release ADB server when necessary, and
clears Debug/Release log and screenshot test files before building.

The `config`, `logs`, and `screenshot` directories are created or populated at
runtime. Run the portable package from a user-writable directory; an
`asInvoker` process cannot create these files in a protected installation
folder without explicit permission.

## ADB Selection

DX Manager never relies on an `adb.exe` found through the system `PATH`.

1. A manually configured ADB is used when manual mode is selected.
2. Windows 7 and 8.1 use the bundled legacy-compatible ADB.
3. Windows 10 and later use the ADB beside the selected scrcpy executable.
4. If that ADB is missing or cannot run, the bundled legacy ADB is used as a
   fallback.

All ADB commands are executed with the selected absolute path.

The ADB version shown in Settings, diagnostics, and logs is parsed from the
`Version ...` line returned by `adb version`. The common
`Android Debug Bridge version 1.0.41` line is a protocol banner and is not the
platform-tools build number.

## Managed File Transfer

The `DexManager.AdbProxy` project builds `DXMAdbProxy.exe` into
`tools/adb-proxy`. When managed file transfer is enabled, only newly started
DeX and single-app scrcpy processes receive this helper through their `ADB`
environment variable. DX Manager's own ADB commands, wireless setup, wake-up,
and screen-state commands continue to use the selected real ADB directly.

The helper forwards ordinary ADB commands unchanged. It intercepts managed
file and folder drops to the target selected in Settings (default:
`/sdcard/Download/`), authenticates each request over a per-session named pipe,
and lets DX Manager serialize the transfer. Files are pushed under ASCII
temporary names and finalized on the phone from Base64-encoded Unicode names.
Complete folder trees, including empty folders, are staged and committed under
one collision-safe top-level name. An independent movable status window shows
the active item and up to four waiting items without claiming byte progress
that ADB cannot report reliably on every supported Windows version. Turning
the setting off restores scrcpy's native file-drop behavior for newly opened
windows.

## Compatibility

- Target framework: .NET Framework 4.6.2
- Intended Windows range: 64-bit Windows 7 SP1 through Windows 11
- 32-bit Windows is not supported
- Bundled scrcpy baseline: 4.1

The .NET Framework 4.6.2 target is intentional: it preserves compatibility
with 64-bit Windows 7 SP1 and offline or closed-network PCs. Windows 7 SP1 does
not include 4.6.2 by default, so the runtime may need to be installed
separately. Because .NET Framework 4.x uses in-place updates, systems with
4.7.2 or 4.8 already installed run the same build on the newer runtime and do
not require a downgrade.

64-bit Windows 7 compatibility should be checked on real hardware before each public
release.

## Android companion

The repository also contains `DXDisplayCleanup`, published as **DX Companion**.
Its signed Release APK is bundled under `tools/companion` but is never installed
automatically. From Diagnostics, the user may install, update, grant permission
to, or uninstall it on the currently selected phone. DX Manager verifies the
exact APK hash and APK Signature Scheme v2 certificate before installation,
then rechecks the installed package, version, certificate, and permission.
DX Companion removes a stale simulated display, turns off Developer options'
stay-awake setting, and sends phone files or folders to DX Manager. Build and
signing details are documented in `DXDisplayCleanup/README.md` and
`DXDisplayCleanup/SIGNING.md`.

## Repository Documentation

Internal architecture, decisions, and handoff notes are maintained in the
repository-level `docs` directory. Current code and Git history take
precedence when an internal note becomes stale.

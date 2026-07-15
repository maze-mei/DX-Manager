# DX Manager Source and Build Notes

[Repository overview](../README.md) ·
[한국어 사용 설명서](../docs/USER_GUIDE_KO.md) ·
[English user guide](../docs/USER_GUIDE_EN.md)

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
`dist/DX-Manager-v1.0.0-win-x64.zip`. Use `-SkipBuild` only when the current
Release output has already been verified. `ExecutionPolicy Bypass` applies
only to this process and does not change the system policy.

The application is portable, but `DXManager.exe` is not standalone. A release
package must include:

- `DXManager.exe` and `DXManager.exe.config`
- `tools/scrcpy` and all of its runtime files
- `tools/adb`
- the `licenses` directory, including `THIRD_PARTY_NOTICES.md`
- `README.md`, `LICENSE`, the user guides, FAQs, and guide images

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

## Compatibility

- Target framework: .NET Framework 4.6.2
- Intended Windows range: 64-bit Windows 7 SP1 through Windows 11
- 32-bit Windows is not supported
- Bundled scrcpy baseline: 4.0

64-bit Windows 7 compatibility should be checked on real hardware before each public
release.

## Repository Documentation

Internal architecture, decisions, and handoff notes are maintained in the
repository-level `docs` directory. Current code and Git history take
precedence when an internal note becomes stale.

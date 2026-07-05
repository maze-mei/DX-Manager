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

The application is portable, but `DXManager.exe` is not standalone. A release
package must include:

- `DXManager.exe` and `DXManager.exe.config`
- `tools/scrcpy` and all of its runtime files
- `tools/adb`
- `THIRD_PARTY_NOTICES.md`
- the `licenses` directory

The `config`, `logs`, and `screenshot` directories are created or populated at
runtime.

## ADB Selection

DX Manager never relies on an `adb.exe` found through the system `PATH`.

1. A manually configured ADB is used when manual mode is selected.
2. Windows 7 and 8.1 use the bundled legacy-compatible ADB.
3. Windows 10 and later use the bundled modern ADB, or a compatible newer ADB
   beside a user-selected scrcpy executable.

All ADB commands are executed with the selected absolute path.

## Compatibility

- Target framework: .NET Framework 4.6.2
- Intended Windows range: Windows 7 SP1 through Windows 11
- Bundled scrcpy baseline: 4.0

Windows 7 compatibility should be checked on real hardware before each public
release.

## Repository Documentation

Internal architecture, decisions, and handoff notes are maintained in the
repository-level `docs` directory. Current code and Git history take
precedence when an internal note becomes stale.

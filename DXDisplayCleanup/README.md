# DX Companion source location

## English

The official Android project is now published under [companion/](../companion/README.md).
It is the version-pinned public snapshot of the canonical shared component used by both PC editions.
Current release: **3.0.0 / code16 / guardian 1 and 3 / Android 13+**.

The Windows source build uses the included snapshot when the maintainer's shared directory is absent.
See [Building](../docs/BUILDING.md). Do not restore this old directory as a second editable Android project.
Ordinary users install the verified APK included in the Windows ZIP through Settings > Companion.
Private signing keys, passwords and local build configuration are not published.

## 한국어

정식 Android 소스·APK는 [companion 폴더](../companion/README.md)에 버전을 고정한 스냅샷으로 공개합니다.
개발 원본은 공용 폴더 한곳에서 유지하며 이 옛 경로를 별도 원본으로 되살리지 않습니다.
공개 저장소만 내려받은 Windows 빌드도 동봉 스냅샷을 사용합니다.

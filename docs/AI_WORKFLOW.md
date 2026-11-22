# Work, documentation and release workflow

## English

1. Read AGENTS/current handoff/TODO, then inspect the active branch and changes. Preserve user work.
2. Windows portable, Store WinUI and macOS are separate implementation scopes. Store stays read-only in this workroom.
3. Diagnosis/review does not authorize product edits; documentation changes do not authorize publishing or replacing verified binaries.
4. A clean antivirus result is evidence for that exact file, not commit/push/tag/release approval.

### Windows code verification

Use the approved .NET Framework 4.6.2 x64 Release environment and `scripts/Verify-OfficialBuild.ps1`.
The suite covers device isolation, transfers, update checks, first-run consent,
Native DeX, 120 FPS, and the English and Korean interface.

The maintainer verifier uses fixed paths and test-only roots. See [Building](BUILDING.md) for prerequisites.
Repeat relevant code gates when code/embedded resources change. For packaging an identical verified EXE, confirm byte identity
and payload/runtime/hash/privacy checks instead of claiming a new full feature test.
Distinguish automated/fixture, maintainer real-use and fresh physical-device results.

### Shared Android component

Maintain official Android source in the one shared component; read its own instructions first.
Use the existing official identity/signature and wrapper scripts. Record workroom/chat attribution and version/change history.
Signing inputs never enter Git or a public ZIP. Never substitute the private AI test APK.
A documentation-only pass does not rebuild/install Android.

### Artifacts and personal state

- Fixed preview: overwrite `E:/vs/dex system/dist/DX Manager Preview`, preserving config/logs/captures/receive data.
- `scripts/Build-FinalRelease.ps1` builds a fresh `DX-Manager-win-x64.zip` and
  versioned application folder in the chosen output directory.
- The v3.0.1 ZIP contains 40 files, including the Windows executable, Korean
  resources, ADB proxy, legacy ADB, scrcpy, Native DeX helper, signed Companion
  APK, licenses and short README files.
- Exclude settings, onboarding/update state, private paths/serials/tokens, logs/captures, source guides/images, PDBs,
  signing material and test programs. Vendor runtime images are not guide screenshots.
- Check extracted ZIP hashes against final files, version identity, fresh-consent state and original APK/runtime pins.

Use `DX-Manager-win-x64.zip` for the latest Windows release. When retiring an
old direct download URL, rename the existing asset in place to retain its
download count. Keep the latest download URL on the new release.

### GitHub documents and images

English precedes Korean in bilingual pages. Keep historical release facts/license text unchanged.
Use reviewed captures of the portable UI. Identify sample device data in the caption.
Do not regenerate/edit original screenshot pixels without the user's instruction.

Inventory current public main before replacing its README. Preserve macOS documentation/source/workflow;
do not erase remote-only work by force-pushing this Windows branch.
Check links/anchors/HTML image paths, matching EN/KO question numbers, version/filename/menu facts and final artifact preservation.
Embedded help is a product-resource snapshot; web-document changes alone do not alter the final executable.

### Publication

Before explicit publication: complete docs/photos, public shared-component source availability and main/macOS reconciliation.
Commit only on request. Push/tag/release creation and old GitHub-asset renaming require the maintainer's publication direction.
Never treat “all scans clean” as upload approval. Report remaining gates without pretending a local ZIP is already published.

## 한국어

코드·문서·프리뷰·검증된 배포 파일·공개 GitHub 상태를 구분합니다.
현재 고정 ZIP·버전 폴더 규칙과 개인정보 제외를 지키며, 같은 EXE의 문서 작업 때문에 기능 검증을 반복하지 않습니다.
영어 초기 상태·한글 혼용·잘림과 링크·사진 상태를 점검하고, 사용자 원본과 정식 APK·서명·스토어 작업물을 보존합니다.
공개 소스 의존성과 macOS 보존을 마친 뒤 메이님이 게시를 지시해야 GitHub를 변경합니다.

# Current status — Windows 2.5.0

## Windows v3.0.0 release status

- [x] Prepare the local release source on the published v2.5.0 base.
- [x] Retain the GitHub update check, first-run notice and macOS source.
- [x] Verify the Windows release build, bilingual interface and clean ZIP.
- [ ] Publish the source and Windows ZIP after the maintainer requests upload.

## English — 2026-10-25

### Release scope completed

- [x] Accepted light/dark UI, tray lifecycle, wireless/TLS, transfer and scrcpy 5.0.1 changes.
- [x] Experimental 120 FPS, every-start warning, prior screen-off restoration and same-phone screen priority.
- [x] Daily background update checks, manual check UI and local per-version changes notes.
- [x] Official Companion 2.1.6 with duplicate-share protection and verified-clean guardian stop behavior.
- [x] English diagnostics/FAQ text and Help fit; environment-check header/button overlap fixed.
- [x] 702 Windows checks; final EXE is identical to the tested preview.
- [x] Clean 39-file ZIP with fixed external name and versioned inner folder.
- [x] Maintainer-reported clean scan for the exact final EXE.
- [x] README, EN/KO guides/35-question FAQs, release notes and 16 approved captures refreshed.
- [x] Matching official Companion source/constants/APK included as a verified public snapshot.
- [x] Public macOS implementation preserved in separate .NET 8 projects.
- [x] Explicit maintainer authorization to commit and publish this release.

### Future work

No new feature is required by this release request. Reported issues should be reviewed against [known limits](KNOWN_ISSUES.md).
Future releases still need explicit publication approval. Do not add the private AI foreground-app/keymap experiment
or change the Store product as part of routine portable maintenance.

### Verification boundaries

Automated Windows checks are not universal Windows 7/GPU/DPI/phone/firmware or shutdown-packet guarantees.
macOS build/runtime evidence is separate from Windows verification. See [verification](VERIFICATION_v2.5.0.md).

## 한국어

이번 요청 범위는 최종본·문서·공식 Companion 소스 연결·macOS 보존과 승인된 게시입니다.
새 기능 추가나 Store 변경은 포함하지 않습니다. 실기 미확인 환경은 성공으로 확대하지 않으며
다음 버전의 게시도 메이님의 별도 지시를 따릅니다.

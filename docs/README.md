# DexManager 개발 문서

마지막 전체 정리: 2026-08-22

새 작업에서는 `HANDOFF.md`, `PROJECT_BRIEF.md`, `SESSION.md`, `TODO.md`,
`AI_WORKFLOW.md` 순서로 읽는다. 작업 성격에 따라 `TECH_NOTES.md`,
`DECISIONS.md`, `KNOWN_ISSUES.md`를 추가로 확인한다.

문서와 코드가 다르면 현재 코드와 Git 이력을 우선하고 문서를 갱신한다.

- `HANDOFF.md`: 새 작업용 현재 기준점, 아키텍처, 불변 조건과 검증 절차
- `PROJECT_BRIEF.md`: 목적, 환경, 핵심 기능
- `TECH_NOTES.md`: 구현 구조와 기술 주의사항
- `DECISIONS.md`: 현재 구조를 선택한 이유
- `KNOWN_ISSUES.md`: 알려진 제약
- `TODO.md`: 남은 작업
- `SESSION.md`: 다음 채팅을 위한 현재 상태
- `AI_WORKFLOW.md`: Codex 작업 및 Git 규칙
- `CHANGELOG.md`: 사용자 관점의 큰 이정표
- `RELEASE_NOTES_v2.0.1.md`: v2.0.1 GitHub Release용 영어·한국어 설명
- `RELEASE_NOTES_v2.0.0.md`: 이전 v2.0.0 GitHub Release 기록
- `RELEASE_NOTES_v1.3.0.md`: 이전 v1.3.0 GitHub Release 기록
- `PACKAGE_README.md`: 배포 ZIP 루트에 들어가는 HTML 없는 영어·한국어 안내
- `PACKAGE_README_MACOS.md`: macOS arm64/x64 포터블 ZIP 전용 영어·한국어 안내
- `../DXDisplayCleanup`: 번들 선택형 Android 복구·파일 전송 앱 소스와 빌드 문서

사용자용 README는 저장소 루트의 `README.md`, 영어/한국어 설명서는
`docs/USER_GUIDE_EN.md`, `docs/USER_GUIDE_KO.md`를 사용한다. 자주 묻는
질문은 독립 문서인 `docs/FAQ_EN.md`, `docs/FAQ_KO.md`에서 관리하고 각
사용 설명서와 README에서 링크한다.

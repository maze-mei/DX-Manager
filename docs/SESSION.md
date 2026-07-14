# Session Handoff

마지막 갱신: 2026-07-14

## Git

- 저장소: `E:\vs\dex system`
- 브랜치: `fix/audit-hardening-20260711`
- 마지막 커밋: `1343631 Harden display lifecycle and input handling`
- 현재 작업: v1 배포 문서와 GitHub 첫 공개 Release 준비

새 세션에서는 실제 `git status --short --branch`와 `git log`를 다시 확인한다.
현재 작업이 커밋되지 않았다면 사용자 변경과 함께 그대로 보존한다.

## 현재 구현 상태

- Windows 11 USB/무선 DeX와 단일창 3개 동시 실행 확인
- 64비트 Windows 7 SP1/.NET Framework 4.6.2 유선 핵심 흐름 확인
- Scrcpy 4.0과 Scrcpy 폴더의 ADB를 기본 사용, 필요 시 legacy ADB fallback
- 연결 상태와 기기 이름 확인 후 실제 시작 명령 직전에 0~60초 대기
- 연결 해제/재연결 시 세션, 화면 OFF, stay-awake 정리
- DeX overlay 너비/높이/DPI 일치 시 재사용, 불일치 시 제거 후 재생성
- 정상 종료 시 관리 기기의 overlay를 생성 주체와 관계없이 제거
- SC1F2 KeyUp이 없는 환경에서도 한영 보정 반복 동작
- Scrcpy 4.0/SDL3 오른쪽 Shift를 왼쪽 Shift로 치환
- DPI 120 미만 입력 거부와 입력 확정 시 안내
- 무선/USB 전환 중 Scrcpy 종료와 자동 숨김 타이머 경합 방어
- 기본/고급 설정 재정리와 환경 점검 표 레이아웃 수정
- 제작자/GitHub 링크, MIT 라이선스, 제3자 고지와 파일 속성 완료
- README/설명서용 한국어·영어 스크린샷 6장 저장
- 한국어/영어 FAQ 15문항을 독립 문서로 작성하고 README와 설명서에서 연결

2026-07-13 현재 .NET Framework 4.6.2 참조 어셈블리로 x64 Debug/Release
빌드가 모두 경고 0, 오류 0으로 통과했다.

2026-07-14 연결 해제 로그에서 고정 기기가 없을 때 target serial을 비운 뒤
즉시 복구하는 1초 주기 상태 반복을 확인했다. 선택 서비스가 기기 없음
상태에서도 고정 serial을 유지하도록 수정하고 별도 복구 처리를 제거했다.
연결 해제 상태의 조용한 감시와 같은 휴대폰 재연결을 실기 재확인했다.

2026-07-14 휴대폰 두 대를 USB로 연결한 경우와 두 휴대폰의 USB/무선 ADB가
동시에 네 개의 transport로 표시되는 경우를 실기 확인했다. 처음 선택한 물리
휴대폰만 유지하고 다른 휴대폰은 무시했으며, 고정된 같은 휴대폰의 USB↔무선
전환은 정상적으로 세션을 정리하고 다시 실행했다.

## 이번 작업의 v1 기기 정책

- 앱이 처음 선택한 휴대폰을 종료까지 고정한다.
- 다른 휴대폰이 추가되거나 고정 기기가 분리되어도 다른 폰으로 전환하지 않는다.
- `ro.serialno`/`ro.boot.serialno`/Android ID가 같으면 USB와 무선 ADB 주소가
  달라도 같은 휴대폰으로 인정한다.
- 다중 휴대폰 선택과 동시 제어는 v2 후보 기능이다.

## 다음 확인

1. Windows 7 최신 빌드 회귀 확인
2. README/설명서 스크린샷 배치
3. Release 폴더 정리 및 GitHub 첫 공개 Release 준비

빌드·커밋·배포 전 `bin\Debug`, `bin\Release`의 `logs`, `screenshot` 테스트
파일을 비운다. 실기 확인하지 않은 흐름은 문서나 보고에서 확인 완료로 쓰지 않는다.

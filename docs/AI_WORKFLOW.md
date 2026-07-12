# AI Workflow

## 시작

1. `docs/README.md`, `PROJECT_BRIEF.md`, `SESSION.md`, `TODO.md`를 읽는다.
2. Git 상태와 최근 커밋을 확인한다.
3. 관련 코드와 기술/결정 문서를 읽는다.
4. DexManager가 실행 중이면 빌드 파일 잠금 여부를 확인한다.

## 수정 원칙

- 기존 기능, 사용자 설정, 미커밋 변경을 보존한다.
- 요청 범위의 최소 변경을 한다.
- .NET Framework 4.6.2와 64비트 Windows 7 호환성을 유지한다.
- ADB는 `AdbService`와 선택한 절대 경로로 실행한다.
- Scrcpy 시작 직렬화 규칙을 지킨다.
- display ID가 모호하면 추측하지 않는다.
- 런타임 설정, 로그와 캡처를 Git에 추가하거나 덮어쓰지 않는다.

## 검증

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe' `
  'E:\vs\dex system\DexManager.sln' /t:Build /p:Configuration=Release /m
```

변경에 따라 설정 보존, USB/무선 target serial, DeX/단일창 동시 실행,
화면 OFF/stay-awake 복구를 확인한다. 실기 확인을 못 했으면 명시한다.

## Git과 문서

- diff를 확인하고 한 커밋에 한 목적만 담는다.
- 생성물과 사용자 데이터를 커밋하지 않는다.
- merge, tag, push, GitHub Release는 사용자 확인 없이 하지 않는다.
- 파괴적인 reset/checkout을 사용하지 않는다.
- 설계 변경은 `DECISIONS.md`
- 새 제약은 `KNOWN_ISSUES.md`
- 우선순위는 `TODO.md`
- 세션 종료 상태는 `SESSION.md`
- 큰 이정표는 `CHANGELOG.md`

문서에 추측을 사실처럼 적지 않는다.

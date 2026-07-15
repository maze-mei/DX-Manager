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
- v1에서는 처음 선택한 휴대폰만 앱 종료까지 관리한다. 같은 휴대폰의
  USB/무선 전환은 허용하지만 다른 휴대폰으로 자동 전환하지 않는다.

## 검증

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe' `
  'E:\vs\dex system\DexManager.sln' /t:Build /p:Configuration=Release `
  '/p:TargetFrameworkRootPath=E:\vs\dex system\.build-tools\net462\build' /m
```

변경에 따라 설정 보존, USB/무선 target serial, DeX/단일창 동시 실행,
화면 OFF/stay-awake 복구를 확인한다. 다중 기기 변경은 최초 기기 고정,
같은 기기의 USB/무선 전환, 다른 기기 무시와 원래 기기 재연결을 확인한다.
실기 확인을 못 했으면 명시한다.

공개용 포터블 폴더와 ZIP은 저장소 루트에서 다음 명령으로 만든다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Package-Release.ps1
```

개발 산출물은 `DexManager\bin\Release`, 사용자 배포 산출물은
`dist\DX Manager`와 버전이 포함된 x64 ZIP으로 구분한다.
스크립트는 DX Manager 실행 여부를 확인하고 번들 Release ADB 서버를 정리한
뒤 Debug/Release의 로그와 스크린샷 테스트 파일을 비운다.

## Git과 문서

- diff를 확인하고 한 커밋에 한 목적만 담는다.
- 생성물과 사용자 데이터를 커밋하지 않는다.
- 빌드·커밋·배포 전에 `bin\Debug`와 `bin\Release` 아래 `logs`,
  `screenshot`의 테스트 파일을 비운다.
- merge, tag, push, GitHub Release는 사용자 확인 없이 하지 않는다.
- 파괴적인 reset/checkout을 사용하지 않는다.
- 설계 변경은 `DECISIONS.md`
- 새 제약은 `KNOWN_ISSUES.md`
- 우선순위는 `TODO.md`
- 세션 종료 상태는 `SESSION.md`
- 큰 이정표는 `CHANGELOG.md`

문서에 추측을 사실처럼 적지 않는다.

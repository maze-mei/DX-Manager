# Project Brief

## 개요

DX Manager는 Samsung DeX용 가상 디스플레이와 Scrcpy 창을 관리하는
Windows WinForms 프로그램이다. 연결된 여러 물리 휴대폰마다 DeX 화면과
앱별 단일창 세 개를 유선 또는 무선 ADB로 독립 실행하고, 키 입력 보정,
캡처, 양방향 파일 전송, Companion과 휴대폰 화면 상태를 함께 관리한다.

## 환경

- 저장소: `E:\vs\dex system`
- 솔루션: `E:\vs\dex system\DexManager.sln`
- Visual Studio 2019, C# WinForms
- .NET Framework 4.6.2
- 외부 NuGet 패키지 없음
- 번들 Scrcpy 4.1
- 번들 선택형 Android companion: `DXDisplayCleanup` (Java 17, min SDK 24,
  compile/target SDK 36)
- 지원 목표: 64비트 Windows 7 SP1부터 Windows 11(32비트 Windows 제외)
- 현재 휴대폰 확인 기준: Android 16 / One UI 8.x의 DeX 지원 Galaxy 기기

개발 산출물은 `DexManager\bin\Release`에 생성된다. 공개 배포는
`scripts\Package-Release.ps1`로 `dist\DX Manager` 폴더와 버전이 포함된 x64
ZIP을 만든다. 실행 파일만 복사하지 말고 `tools`, Scrcpy DLL과
`scrcpy-server`, 라이선스와 사용자 문서를 포함한 폴더 전체를 배포한다.
GitHub 메인 README와 별도로 HTML이 없는 `docs\PACKAGE_README.md`를 배포
폴더의 `README.md`로 사용하며, 모든 이중 언어 문서는 영어 다음 한국어
순서로 작성한다.

macOS 에디션은 `scripts/Package-Mac-Release.sh`로 Apple Silicon arm64와
Intel x64 self-contained ZIP을 별도로 만든다. 각 ZIP에는 해당 아키텍처로
미리 publish한 DX Manager와 ADB proxy, 공식 scrcpy 4.1 정적 빌드, ADB,
scrcpy-server, 실행기와 라이선스를 포함한다. 사용자는 Homebrew나 .NET을
설치하거나 소스를 빌드하지 않고 ZIP 전체를 풀어 실행한다.

## 완료 기능

- DeX overlay 가상 디스플레이 생성, 재사용, 초기화
- 생성 전후 비교 기반 display ID 탐색
- DeX Scrcpy 실행/중지와 설정 적용
- Scrcpy `--new-display` 기반 단일창 슬롯 3개
- 슬롯별 해상도, DPI, 비트레이트, FPS, 앱과 옵션 저장
- Android 패키지별 단일창 프로필 저장·자동 적용·삭제
- 앱 목록, 표시 이름/패키지 저장, 자동 실행
- 성공적으로 자동 실행한 앱의 공통 최근 목록 보존
- Scrcpy 4.0 이상 `--flex-display`
- Scrcpy 3.3.4/4.x 실행 옵션 자동 호환
- USB 및 TCP/IP 무선 ADB, IP 자동 감지, Android 11 페어링
- OS별 ADB 선택과 절대 경로 실행
- 물리 휴대폰 identity별 독립 런타임과 복수 휴대폰 동시 제어
- 같은 휴대폰의 USB·무선 transport 병합과 휴대폰별 연결 정책 강제
- 휴대폰별 DeX·단일창·앱 프로필·Companion·양방향 전송 상태 분리
- 기기 인식 후 실제 시작 명령 직전의 사용자 설정 대기(기본 1초)
- 한영키/Enter 보정
- Scrcpy 4.0/SDL3 오른쪽 Shift 호환 치환
- F8 전체/영역 캡처와 스마트폰 전송
- 미입력 자동 숨김과 시스템 트레이
- DeX·단일창 HWND/PID별 미니 컨트롤바와 scrcpy 주요 동작
- 다중 Scrcpy의 화면 끄기, 잠자기 방지, 종료 복구
- 실행 세션 로그와 수동 저장
- Windows 언어 자동 감지와 한국어/영어 UI 선택
- `.resx` 기반 UI 문자열 관리
- DeX overlay 해상도/DPI 일치 재사용과 불일치 자동 재생성
- 정상 종료 시 관리 기기 overlay 정리
- 제작자/GitHub 링크, MIT 라이선스와 제3자 고지
- 사용자 지정 해상도 가로·세로 4096 상한과 DPI 120 하한 입력 검증
- 첫 실행과 전체 초기화에 동일하게 적용되는 v1 기본 설정
- DeX·단일창 드롭 파일과 폴더를 설정한 휴대폰 폴더로 보내는 관리형 Unicode 전송
- 현재 항목·다음 4개·크기·경과·완료·실패·대기를 보여주는 독립 이동 상태창,
  취소와 충돌 파일·폴더 이름 자동 변경
- 설정에서 관리형 전송을 끄면 새 창부터 Scrcpy 순정 전송으로 복귀
- 공통 `1.0.41` 문구 대신 ADB의 실제 `Version ...` 빌드 값 표시
- overlay 제거 시 global setting 자체를 삭제
- 진단 페이지에서 공식 Android 정리 앱의 package/v2 서명 인증서를 검증한 뒤
  `WRITE_SECURE_SETTINGS` 권한을 한 번 부여하고 결과를 재확인
- 캡처와 드롭 파일의 휴대폰 저장 위치를 ADB 폴더 탐색기로 선택
- 휴대폰의 공유 메뉴와 Companion 폴더 선택으로 파일·폴더를 PC에 전송
- 선택 기기별 DX Companion 설치·업데이트·재설치·권한 부여·삭제

`DXDisplayCleanup` 프로젝트의 공개 이름은 **DX Companion 2.0.0**이다.
휴대폰에 남은 `overlay_display_devices`를 제거하고 개발자 옵션의 절전모드
해제를 끄는 제한된 복구 도구다. 앱 본체의 개별·동시 정리, 빠른 설정 타일과
2 × 1 홈 위젯, 휴대폰에서 PC로 파일·폴더 전송을 제공한다. 네트워크는 인증된
로컬 연결에만 사용하며 분석 수집·클라우드 전송·임의 shell 기능은 포함하지
않는다. 서명된 APK는 공개 ZIP의 `tools\companion`에
포함하지만 자동 설치하지 않는다. DX Manager는 설치 전 APK 해시·공식 서명,
설치 후 package·버전·서명과 권한을 검증한다.

## 현재 상태

v2.0.1은 첫 물리 휴대폰 identity가 확정된 뒤에도 DeX 화면에 공통 기본값이
남아 있던 UI 동기화 회귀를 수정한 GitHub 업로드 후보이다. DX Companion은
변경 없이 2.0.0(versionCode 6)을 유지한다.

v2.0.0과 DX Companion 2.0.0을 공개 배포했다. Windows 11에서
휴대폰 두 대의 USB·무선 조합, 독립 DeX·단일창, 설정, Companion과 양방향
전송을 확인했다. 64비트 Windows 7 SP1/.NET 4.6.2에서는 USB 복수 기기와
핵심 기능을 확인했다. 공개 v2.0.0은 물리 기기별 런타임·설정·연결 정책과
Companion 2.0.0 종료 보호를 포함한다. 설정의 진단 페이지에는 현재 선택된
휴대폰의 Android·SDK·One UI·보안 패치와 연결 방식을 표시하고, 민감 정보를
가린 진단 보고서를 텍스트 파일로 저장하는 기능도 포함한다.

휴대폰의 현재 정상 동작 확인 기준은 Android 16 / One UI 8.x다. One UI 7.x
이하에서는 원활한 동작을 확인하지 못했으며 검은 DeX 창이 나타날 수 있다.

앱 아이콘, 다국어 UI, 개발 문서와 제3자 라이선스 고지는 완료됐다.
한국어/영어 FAQ도 독립 사용자 문서로 완료됐으며 README, 사용 설명서와
FAQ의 스크린샷 배치도 완료됐다. 공개용 x64 ZIP을 만들고 개인 설정, PDB,
로그와 테스트 스크린샷이 포함되지 않은 것도 확인했다. GitHub 저장소와
`DX Manager v2.0.0` Release, x64 ZIP 및 최종 대표 이미지 게시도 완료했다.
Windows 7 실제 종료 복원 재확인, 일부 키보드 실기 확인, 무선 ADB endpoint
자동 발견과 upstream 입력 버그 보고가 차기 작업으로 남아 있다.

## 제품 원칙

- DeX와 단일창의 가상 디스플레이 생성 방식을 섞지 않는다.
- 모호한 display ID를 가장 큰 숫자로 추측하지 않는다.
- PATH의 `adb.exe`에 의존하지 않는다.
- Scrcpy용 파일 전송 proxy는 세션 대상 폴더의 파일·폴더 push만 가로채고
  나머지 ADB 명령과 DX Manager 내부 ADB 경로를 바꾸지 않는다.
- 64비트 Windows 7/.NET 4.6.2 호환성을 유지한다.
- 한 Scrcpy 종료가 나머지 창의 화면 상태를 깨뜨리지 않아야 한다.
- 사용자 설정과 미커밋 변경을 덮어쓰지 않는다.
- 모든 기기 명령은 작업 시작 시 캡처한 명시적 serial에만 보낸다.
- 한 기기의 종료·전송·연결 변경이 다른 기기 런타임에 영향을 주지 않는다.

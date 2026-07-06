# Project Brief

## 개요

DX Manager는 Samsung DeX용 가상 디스플레이와 Scrcpy 창을 관리하는
Windows WinForms 프로그램이다. DeX 화면 한 개와 앱별 단일창 세 개를
유선 또는 무선 ADB로 실행하고, 키 입력 보정, 캡처, 자동 숨김과 휴대폰
화면 상태를 함께 관리한다.

## 환경

- 저장소: `E:\vs\dex system`
- 솔루션: `E:\vs\dex system\DexManager.sln`
- Visual Studio 2019, C# WinForms
- .NET Framework 4.6.2
- 외부 NuGet 패키지 없음
- 번들 Scrcpy 4.0
- 지원 목표: Windows 7 SP1부터 Windows 11

산출물은 `DexManager\bin\Release`에 생성된다. 실행 파일만 복사하지 말고
`tools`, Scrcpy DLL과 `scrcpy-server`를 포함한 폴더 전체를 배포한다.

## 완료 기능

- DeX overlay 가상 디스플레이 생성, 재사용, 초기화
- 생성 전후 비교 기반 display ID 탐색
- DeX Scrcpy 실행/중지와 설정 적용
- Scrcpy `--new-display` 기반 단일창 슬롯 3개
- 슬롯별 해상도, DPI, 비트레이트, FPS, 앱과 옵션 저장
- 앱 목록, 표시 이름/패키지 저장, 자동 실행
- 성공적으로 자동 실행한 앱의 공통 최근 목록 보존
- Scrcpy 4.0 `--flex-display`
- Scrcpy 3.3.4/4.0 실행 옵션 자동 호환
- USB 및 TCP/IP 무선 ADB, IP 자동 감지, Android 11 페어링
- OS별 ADB 선택과 절대 경로 실행
- 한영키/Enter 보정
- Scrcpy 4.0/SDL3 오른쪽 Shift 호환 치환
- F8 전체/영역 캡처와 스마트폰 전송
- 미입력 자동 숨김과 시스템 트레이
- 다중 Scrcpy의 화면 끄기, 잠자기 방지, 종료 복구
- 실행 세션 로그와 수동 저장
- Windows 언어 자동 감지와 한국어/영어 UI 선택
- `.resx` 기반 UI 문자열 관리

## 현재 상태

핵심 기능은 완료된 베타다. Windows 11에서 유선/무선 DeX와 단일창 3개
동시 실행을 확인했다. Windows 7 SP1/.NET 4.6.2에서 유선 핵심 기능과
Scrcpy 4.0 실행도 확인했다.

앱 아이콘, 다국어 UI, 사용자/개발 문서와 제3자 라이선스 고지는 완료됐다.
남은 일은 최신 빌드의 Windows 7 회귀 확인, upstream 입력 버그 보고와
GitHub Release 준비다.

## 제품 원칙

- DeX와 단일창의 가상 디스플레이 생성 방식을 섞지 않는다.
- 모호한 display ID를 가장 큰 숫자로 추측하지 않는다.
- PATH의 `adb.exe`에 의존하지 않는다.
- Windows 7/.NET 4.6.2 호환성을 유지한다.
- 한 Scrcpy 종료가 나머지 창의 화면 상태를 깨뜨리지 않아야 한다.
- 사용자 설정과 미커밋 변경을 덮어쓰지 않는다.

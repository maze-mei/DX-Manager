# Decision Log

## .NET Framework 4.6.2

회사 Windows 7 SP1에 설치된 4.6.2를 기준으로 4.7.2에서 낮췄다.

## OS별 ADB

Windows 10 미만은 legacy, Windows 10 이상은 modern ADB를 쓴다.
PATH 의존은 배포 환경 차이를 만들기 때문에 금지한다.

## 키 보정은 scan code 우선

AHK의 `SC1F2::Send +{Space}`에 맞춰 한영키를 scan code로 감지하고
SendInput scan-code 조합을 우선한다. 오른쪽 Alt를 분리하고 ADB는
fallback으로 남겼다.

## 최대 display ID 선택 제거

DeX와 단일창을 함께 쓰면 display가 여러 개다. 생성 전후 차집합과
메타데이터로 선택하고, 모호하면 추측하지 않고 실패한다.

## 단일창은 Scrcpy display

단일창은 overlay DeX를 추가하지 않고 Scrcpy `--new-display`로 만든다.

## 다중 Scrcpy 화면 전원 조정

한 창 종료가 나머지 창을 깨우거나 검게 만들지 않도록 전체 세션 요청으로
판단한다. 화면 OFF 재적용은 Scrcpy 시작과 직렬화한다.

## 앱 이름과 패키지 함께 저장

재시작 뒤 창 제목이 패키지명으로 바뀌지 않도록 둘 다 저장한다.

## 세션 로그

상시 누적 대신 현재 실행 로그만 표시하고 요청할 때 파일로 저장한다.

## 무선 연결

`adb tcpip` USB bootstrap과 Android 11 페어링을 모두 지원한다. USB 준비
시 Wi-Fi IP를 자동 감지하고 직접 입력은 보조 수단으로 둔다.

## 2026-07 - 제품명은 DX Manager

배포 시 Samsung DeX 브랜드와 제품명이 혼동되지 않도록 프로그램 브랜드를
DX Manager로 바꿨다. 기능 설명의 `DeX 모드`, `DeX 시작`은 실제 지원
기능명이므로 유지한다. 내부 namespace와 저장소 폴더명은 불필요한 위험을
피하기 위해 `DexManager`를 유지한다.

## 2026-07 - 영어 기본 리소스와 한국어 위성 리소스

UI 문자열은 `.resx`로 관리한다. Windows UI 언어가 한국어면 한국어,
그 외에는 영어를 자동 선택하며 설정에서 수동 지정할 수 있다. 기술 로그는
문제 분석의 일관성을 위해 당분간 기존 언어를 유지한다.

## 2026-07 - 실행 버튼 단순화

동일한 크기의 시작/중지와 설정 적용 버튼이 나란히 있던 구조를 제거했다.
시작/중지 버튼 하나를 기본 동작으로 두고, 실행 중 설정이 바뀐 경우에만
작은 `변경사항 적용` 링크를 표시한다.

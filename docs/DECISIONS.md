# Decision Log

## .NET Framework 4.6.2

회사 Windows 7 SP1에 설치된 4.6.2를 기준으로 4.7.2에서 낮췄다.

## OS별 ADB

Windows 10 미만은 legacy ADB를 사용한다. Windows 10 이상은 선택한 Scrcpy
폴더의 ADB를 사용하고, 해당 ADB를 실행할 수 없을 때 legacy ADB로
대체한다.
PATH 의존은 배포 환경 차이를 만들기 때문에 금지한다.

## 키 보정은 scan code 우선

AHK의 `SC1F2::Send +{Space}`에 맞춰 한영키를 scan code로 감지하고
SendInput scan-code 조합을 우선한다. 오른쪽 Alt를 분리하고 ADB는
fallback으로 남겼다.

## Scrcpy 4.0 오른쪽 Shift 호환

Windows에서 Scrcpy 3.3.4/SDL2는 오른쪽 Shift가 정상 동작하지만
Scrcpy 4.0/SDL3는 같은 물리 입력을 처리하지 못하는 현상을 재현했다.
Windows 저수준 후크에는 `vk=0xA1`, `scan=0x36` 누름/뗌이 모두 들어오므로
DX Manager의 감지 실패는 아니다.

SDL3 기반 Scrcpy 창이 활성화된 경우에만 원본 오른쪽 Shift를 차단하고
SendInput scan code 왼쪽 Shift로 치환한다. 오른쪽 Shift를 다시 보내는
보정은 SDL3의 같은 처리 경로를 반복하므로 사용하지 않는다. 이 우회로 인해
Android에서는 좌우 Shift를 구분할 수 없지만 일반 타이핑 기능을 우선한다.

## 외부 Scrcpy 버전 호환

지정한 Scrcpy의 `--version` 결과에서 Scrcpy와 SDL 주 버전을 감지한다.
3.3.4는 잠자기 방지에 `-w`를 사용하고 `--flex-display`를 제외한다.
4.0 이상은 `--keep-active`와 `--flex-display`를 사용한다. 버전 감지에
실패하면 번들 기준인 Scrcpy 4.0 동작을 유지한다.

## 일반 텍스트 입력은 네이티브 편집 엔진

경로와 추가 인자처럼 자유 편집이 필요한 필드는 커스텀 둥근 프레임 안에
테두리 없는 WinForms TextBox를 배치한다. 외형은 DX Manager가 그리되
선택, 드래그, 가로 스크롤, 실행 취소와 IME는 Windows에 맡긴다. 숫자와
단축키처럼 동작이 제한된 필드는 기존 커스텀 입력기를 유지한다.

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

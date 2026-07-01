# Known Issues and Constraints

## 네트워크 격리

무선 ADB는 PC와 휴대폰의 직접 로컬 통신이 필요하다. 같은 대역이어도 게스트
Wi-Fi, AP/client isolation, VLAN 또는 인트라넷 차단이 있으면
`adb connect`가 10060 timeout으로 실패한다.

ASUS 공유기는 게스트 네트워크의 인트라넷 접근 허용과 AP 격리 해제를
확인한다. 개발 중 5GHz 초기 연결 실패가 2.4GHz 연결 및 ADB 재시작 뒤
해소됐다. 재현 시 ARP, 5555 포트와 공유기 격리를 먼저 확인한다.

## 무선 준비 지속성

휴대폰 재부팅이나 Android 정책으로 `adb tcpip`가 풀리면 USB로 무선 준비를
다시 실행한다. IP는 비워두면 자동 감지된다.

## Windows 7

자동화된 Windows 7 테스트 환경은 없다. 릴리스 전 회사 PC 실기 확인이
필요하다.

## 외부 Scrcpy

사용자 지정 Scrcpy가 현재 옵션을 지원하는지는 사용자가 확인해야 한다.
기준 번들은 Scrcpy 4.0이다.

## 배포

- 설치 프로그램 없음
- Assembly/File version은 `1.0.0.0`
- 앱 아이콘과 최종 UI 미확정
- Scrcpy 및 동봉 파일 라이선스 고지 정리 필요
- 자동화 테스트 없이 주요 흐름은 실기 테스트에 의존

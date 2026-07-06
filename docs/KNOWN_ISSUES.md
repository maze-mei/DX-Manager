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

Scrcpy 3.3.4와 4.0의 주요 옵션 차이는 자동 처리한다. 3.3.4에서는
`--keep-active` 대신 `-w`를 사용하고 `--flex-display`를 제외한다.
그 밖의 과거/향후 버전과 사용자가 직접 입력한 추가 인자의 호환성까지
보장하지는 않는다. 기준 번들은 Scrcpy 4.0이다.

## Scrcpy 4.0 오른쪽 Shift

Windows용 Scrcpy 4.0/SDL3에서 물리 오른쪽 Shift가 Android로 정상
전달되지 않는 현상을 재현했다. 3.3.4/SDL2에서는 정상이며 `-K`, `-M`
사용 여부와 관계없이 발생했다.

DX Manager는 SDL3 Scrcpy가 활성화된 동안 오른쪽 Shift를 왼쪽 Shift로
치환한다. 타이핑은 정상화되지만 Android 앱에서 좌우 Shift를 서로 다른
키로 사용하는 경우에는 두 키를 구분할 수 없다. 다른 Windows 앱과 SDL2
Scrcpy에는 영향을 주지 않는다. Upstream 보고 및 수정 여부를 추적한다.

## 배포

- 설치 프로그램 없음
- Assembly/File version은 `1.0.0.0`
- 앱 아이콘과 최종 UI 미확정
- Scrcpy 및 동봉 파일 라이선스 고지 정리 필요
- 자동화 테스트 없이 주요 흐름은 실기 테스트에 의존

# TODO

## 다음 작업

- [x] v2.0.1 DeX 기기별 설정 표시 회귀 수정
  - [x] 첫 물리 identity 결속 뒤 기기별 DeX 설정 UI 재동기화
  - [x] 최초 다중 기기 선택에서 공통 기본값의 기기별 설정 덮어쓰기 차단
  - [x] Windows 버전 2.0.1과 기존 DX Companion 2.0.0(versionCode 6) 호환 확인
  - [x] .NET Framework 4.6.2 x64 Release와 39개 다중 기기 회귀 테스트

- [x] macOS 아키텍처별 포터블 배포 기반
  - [x] arm64/x64 DX Manager와 ADB proxy self-contained single-file publish
  - [x] 공식 scrcpy 4.1 arm64/x86_64 아카이브 SHA-256 검증 및 번들
  - [x] ZIP 재압축 해제, 실행 권한, 아키텍처, 외부 경로, 제외 파일 검증
  - [x] Apple Silicon/x64 GitHub Actions matrix, PR artifact와 검증된 Release 초안
  - [ ] PR에서 첫 GitHub Actions arm64/x64 원격 실행 결과 확인
  - [x] `Q` 정상 종료의 DeX·단일창·전송·overlay cleanup 보강
  - [x] DeX 시작·정리 전 live identity 재검증과 identity별 보류 정리 분리
  - [x] CLI 시작·자연 종료 대기의 `Ctrl+C` 취소 및 종료 경합 보강
  - [ ] 실제 Intel Mac에서 Galaxy DeX 시작·중지와 overlay cleanup 실기 확인
  - [ ] Apple Developer ID 서명·notarization 자격이 준비되면 공개 ZIP 서명

- [x] v2.0.0 공개 후보 준비
  - [x] Windows 버전 2.0.0과 DX Companion 2.0.0(versionCode 6) 일치
  - [x] 다중 기기 README·사용 설명서·FAQ·개발 문서 최신화
  - [x] 진단 페이지의 선택 기기 Android·SDK·One UI·보안 패치 진단
  - [x] 기기 이름·serial·IP·토큰·로컬 경로를 가린 진단 보고서 저장
  - [x] Windows x64/.NET Framework 4.6.2 Release와 전체 회귀 테스트
  - [x] Android 단위 테스트·lint·Release APK 서명 및 해시 재검증
  - [x] 공개용 ZIP의 필수 파일과 비밀키·설정·로그 제외 최종 검사

- [x] v2 기능 구현 후 구조 건강성 감사
  - [x] `MainForm` 기기 탭·연결 수명주기·연결 상태 보조 로직 분리
  - [x] 설정 루트와 설정 DTO·열거형 분리
  - [x] 사용자 입력 컨트롤을 기본 입력·값 입력·드롭다운 팝업으로 분리
  - [x] 구독되지 않은 v1 단일 기기 연결·분리 처리기 제거
- [ ] Scrcpy 주 창과 단일창의 프로세스 종료·출력 drain 공통화 검토
  - 종료 경합과 비정상 분리 회귀 테스트를 먼저 추가한 뒤 진행한다.
- [ ] `PhoneTransferReceiver`의 소켓 프로토콜과 PC 저장 경로·충돌 이름 처리를 분리
  - 대용량 전송·중단·재연결 테스트를 먼저 보강한 뒤 진행한다.
- [ ] `WirelessAdbService`의 기기별 연결 명령과 자동 재연결 snapshot 계산을 분리
  - USB·무선 동시 연결과 여러 무선 기기의 격리 테스트를 유지한다.
- [ ] Android 11+ 무선 ADB의 현재 연결 endpoint를 mDNS로 자동 발견
  - 번들 ADB의 `adb mdns services`에서 `_adb-tls-connect._tcp` 서비스를 조회하고,
    페어링용 `_adb-tls-pairing._tcp` 포트와 실제 연결 포트를 명확히 구분한다.
  - 검색 결과의 첫 항목을 임의로 선택하지 않는다. 물리 기기 identity, 기존 USB
    serial, 모델과 이미 검증된 transport를 대조해 현재 선택한 휴대폰의 endpoint만
    갱신하며, 같은 모델 여러 대와 복수 무선 기기를 안전하게 구분한다.
  - 휴대폰 재부팅, 무선 디버깅 재활성화, Wi-Fi 변경 등으로 연결 포트가 바뀌면
    자동 재연결 전에 새 endpoint를 다시 발견한다. 검증에 성공한 경우에만 기기별
    `WirelessHost`와 `WirelessPort`를 갱신한다.
  - mDNS를 사용할 수 없거나 방화벽·공유기가 multicast 검색을 차단하면 현재의
    수동 IP·연결 포트 입력과 저장된 endpoint를 폴백으로 유지한다.
  - 사용자가 선택한 USB 전용·무선 전용 연결 정책은 그대로 지킨다. 자동 발견을
    이유로 USB와 Wi-Fi 사이를 임의로 전환하지 않는다.
  - Windows 7의 legacy ADB와 `USB로 무선 준비`의 고정 5555 방식은 지원 여부와
    동작이 다르므로 Android 11+ 보안 무선 디버깅 경로와 분리한다.
  - 로그에는 발견 서비스, 대상 기기 판정 근거, 이전/새 endpoint, 폴백 사유와
    연결 결과를 남기되 6자리 페어링 코드와 인증 정보는 기록하지 않는다.
  - 한 대, 같은 모델 두 대, USB+Wi-Fi 혼합, 무선 두 대, 포트 변경, stale 서비스,
    mDNS 차단과 수동 폴백을 독립 회귀 테스트와 실제 기기에서 확인한다.
  - 구현과 실기 검증이 끝난 뒤 README, 영어·한국어 사용 설명서와 FAQ의 무선 ADB
    안내를 갱신한다. 기본 흐름은 `최초 1회 페어링 -> 연결 endpoint 자동 발견`으로
    설명하고, 수동 IP·포트 입력은 폴백으로 안내한다. 페어링 포트와 연결 포트가
    서로 다르며 연결 포트는 바뀔 수 있다는 설명, legacy 5555 방식, 관련 스크린샷과
    캡션도 함께 수정한다.
- [ ] `AppSettings.EnsureDefaults`를 설정 범주별 정규화 helper로 축소
  - 기존 스키마 설정 파일 마이그레이션 표본을 만든 뒤 진행한다.
- [x] v2 1단계 물리 기기·ADB transport 기반
  - [x] v1.3.0 기준 `feature/v2-multi-device` 분기
  - [x] 물리 기기, transport, 방어적 복사 스냅샷 모델 추가
  - [x] 동일 identity의 USB·무선 병합과 서로 다른 휴대폰 분리
  - [x] transport별 상태, 기본 USB 우선과 명시적 transport 선택
  - [x] identity 조회 실패 시 기존 serial 매핑 또는 임시 identity 사용
  - [x] 상태 변화 세대 번호·이벤트와 방어적 복사
  - [x] .NET Framework 4.6.2 x64 Release 및 독립 회귀 테스트
- [x] v2 2단계 전역 ADB 대상 제거
  - [x] `AdbService.TargetSerial`과 프로세스 전역 `ANDROID_SERIAL` 의존 위치 목록화 및 제거
  - [x] 모든 기기별 ADB·Scrcpy 명령에 명시적 serial 전달
  - [x] 기기 선택 상태와 명령 실행 대상을 분리하고 시작·정리 시 serial을 캡처
  - [x] 한 기기의 명령·정리·취소 범위가 다른 기기와 섞이지 않는 독립 회귀 테스트
- [x] v2 3단계 기기별 런타임 세션
  - [x] DeviceMonitor, DeX, 단일창, Scrcpy, 화면 전원 상태를 물리 기기별로 분리
  - [x] Companion reverse·수신 토큰과 PC↔폰 전송 큐를 기기별로 분리
  - [x] 물리 기기와 독립 서비스 묶음을 1:1로 결속하고 USB·무선 전환 시 유지
  - [x] 연결 해제 상태를 정리 증거와 함께 보존하고 의미 없는 감시 갱신은 무시
  - [x] .NET Framework 4.6.2 x64 Release 및 기기 격리 회귀 테스트
- [x] v2 4단계 다중 기기 선택 UI와 실기 검증
  - [x] 메인 화면 기기 선택 UI와 연결 방식·상태 표시
  - [x] 탭별 독립 DeX·단일창·양방향 전송 서비스 선택과 비선택 세션 유지
  - [x] USB·무선 transport 변경, 연결 해제, 전체 종료의 기기별 정리 경로
  - [x] .NET Framework 4.6.2 x64 Release 및 26개 다중 기기 회귀 테스트
  - [x] 두 휴대폰 동시 DeX·단일창 실행과 탭별 실행 상태 복원
  - [x] 두 휴대폰 PC→폰 파일 전송과 각각의 연결 해제 격리
  - [x] 두 휴대폰 폰→PC 파일 전송과 연결 상태 전체 정상 종료 정리
- [x] v2 5단계 기기별 무선 ADB 설정
  - [x] 설정의 연결 페이지에서 대상 휴대폰을 명시적으로 선택
  - [x] 선택한 휴대폰의 정확한 USB serial로 무선 준비와 IP 감지 수행
  - [x] 물리 identity별 USB·무선 모드, IP, 포트와 자동 재연결 저장
  - [x] 혼합 USB·무선 연결과 복수 무선 자동 재연결 지원
  - [x] 기존 전역 무선 설정을 최초 기기 프로필로 안전하게 이관
  - [x] .NET Framework 4.6.2 x64 Release 및 31개 다중 기기 회귀 테스트
- [x] v2 다중 기기 식별·표시 마무리
  - [x] 저장된 USB·무선 연결 정책과 실제 감지 transport 상태를 분리해 표시
  - [x] USB·무선 정책 사이 자동 fallback 제거와 선택 transport 대기 처리
  - [x] 연결 방식 변경·분리 시 이전 세션·전송·ADB reverse 정리
  - [x] 메인 기기 탭 전환 시 설정 창을 유지하고 선택 기기 정보 새로 고침
  - [x] 휴대폰→PC 수신 파일을 휴대폰 표시 이름별 하위 폴더로 분리
  - [x] DeX·단일창 scrcpy 제목에 휴대폰 표시 이름 추가
  - [x] 연결 시 DeX 자동 시작을 연결된 각 휴대폰의 독립 런타임으로 확장
  - [x] 한 대 사용 시 기기 탭 숨김과 두 번째 기기 확인 후 실행 중 탭 유지
  - [x] 기기 선택 UI를 왼쪽 사이드바로 이동하고 시작 시 최신 모델 우선·순차 연결 시 최초 연결 순서 유지
  - [x] .NET Framework 4.6.2 x64 Release 빌드 및 35개 다중 기기 회귀 테스트
- [ ] Windows 종료 중 ADB 오류 반복 방지
  - [x] 새 ADB·보조 프로세스 실행 차단과 실행 중 프로세스 종료 gate
  - [x] DX Manager가 선택한 절대 경로의 ADB 프로세스만 종료
  - [x] 정상 종료의 기기 설정 복원·overlay 정리·`adb kill-server` 경로 유지
  - [x] 프로세스 차단·취소 회귀 테스트 추가
  - [x] Windows 7 회사 PC 1차 확인: 오류창 약 10회에서 2~3회로 감소
  - [x] 제한 시간 내 overlay·절전모드 해제 복원 후 ADB 최종 차단 경로 추가
  - [x] 자식 프로세스 네이티브 오류 대화상자 억제 모드 추가
  - [x] 일반 종료 후 분리된 ADB 서버와 번들 보조 프로세스의 절대 경로 최종 정리
  - [x] 이름이 같은 다른 경로 프로세스를 보존하는 회귀 테스트 추가
  - [x] 공개 UI에 넣었던 Windows 종료 모의 테스트를 RC 검증 후 제거
  - [x] 실제 종료 시 새 ADB 실행·종료를 없애고 미리 연결된 Companion 소켓만 사용
  - [x] Companion 미설치·미검증 기기는 실제 Windows 종료 정리를 건너뛰도록 분리
  - [x] Companion 연결 손실 시 기본 5분 유예와 재연결 취소, 즉시 정리 및 자동 정리 안 함 옵션 추가
  - [x] .NET Framework 4.6.2 Debug 빌드와 37개 다중 기기 회귀 테스트 통과
  - [x] Windows 11 RC2 다중 기기 overlay·절전모드 해제 정리 확인
  - [x] Windows 11 실제 종료에서 ADB 오류창 제거와 Companion 기기 설정 복원 확인
  - [ ] Windows 7 회사 PC에서 오류창 제거와 기기 설정 복원 재확인

- [x] v1.2.0 구조 분리
  - [x] MainForm·SettingsForm feature partial 분리
  - [x] 파일 전송 IPC·처리·ADB·원격 작업·진행 상태 분리
  - [x] 폴더 전송 계획과 실행 결과 모델 분리
  - [x] .NET Framework 4.6.2 x64 Debug/Release 빌드
- [x] v1.2.0 실기 회귀 테스트
  - DeX·단일창 시작/중지와 USB·무선 재연결
  - 화면 끄기·잠자기 방지·키보드 보정과 비정상 분리 정리
  - 단일 파일·폴더·Unicode 파일 전송, 취소와 연결 해제
  - Windows 11 확인 후 Windows 7 SP1 확인

- [x] DX Companion 실제 Galaxy 기기 검증
  - [x] 공식 Release APK 설치와 package/certificate 재확인
  - [x] ADB 1회 권한 부여 전·후 상태 표시
  - [x] 앱 본체의 활성 overlay 삭제와 삭제 후 재조회
  - [x] 빠른 설정 타일·2 × 1 홈 위젯과 절전모드 해제 복구 확인
- [x] DX Manager의 정리 앱 package/v2 인증서 검증과 실제 권한 부여 연결
  - 부여 직전·직후 재검증 및 사후 검증 실패 시 권한 회수
- [x] v1.3.0 번들 DX Companion 관리 코드와 Release 빌드
  - [x] 번들 APK SHA-256/v2 인증서 사전 검증
  - [x] 현재 선택 기기 명시적 설치·업데이트·재설치·삭제
  - [x] 설치 후 package·versionCode·서명·권한 재검증
  - [x] 삭제 전 해당 기기의 파일 수신과 ADB reverse 정리
  - [ ] 실제 기기에서 진단 UI 설치·업데이트·재설치·삭제 회귀 확인
- [x] v1.3.0 포터블 ZIP에 APK 포함 및 비밀키·설정·로그 제외 검사
  - 실제 설치·삭제 회귀 뒤 공개 직전에 같은 검사를 한 번 더 수행
- [x] v1.3.0 공개 후보 x64/.NET 4.6.2 Release와 ZIP 최종 재검증
  - Windows 빌드 경고 0·오류 0, Android 단위 테스트·Release lint 통과
  - 필수 파일, APK 해시·v2 서명과 비밀키·설정·로그 제외 확인
- [x] Companion PC 수신 준비 상태 실기 회귀
  - DX Manager 실행·종료와 USB/무선 연결·분리 시 준비/대기 표시 전환
  - 비정상 종료 뒤 저장된 세션 정보가 남아도 실제 수신기 확인 실패 시 대기 표시
  - 공유 메뉴 전송 직전 수신기 재확인
- [x] 캡처·드롭 파일 휴대폰 저장 경로 ADB 폴더 찾아보기

- [x] v1 최초 휴대폰 고정 로직 실기 확인
  - 시작 시 USB 휴대폰 2대 중 한 대 선택 및 고정
  - 실행 중 다른 휴대폰 연결 무시
  - 고정 휴대폰 분리 시 다른 휴대폰으로 자동 전환하지 않음
  - 같은 휴대폰 재연결 및 USB↔무선 전환 허용
- [x] Windows 7에서 최신 입력/설정 UI 회귀 확인
- [ ] Scrcpy 4.0/SDL3 오른쪽 Shift 재현 내용을 upstream에 보고
- [ ] Scrcpy 4.1/SDL3에서도 오른쪽 Shift 호환 보정의 필요 여부 실기 확인
- [ ] Upstream 수정 시 오른쪽 Shift 치환 우회 제거 여부 검토
- [ ] 한국어 노트북의 `VK_HANGUL + extended scan 0x38` 한영키 실기 확인
  - 브라질 ABNT/ABNT2 또는 AltGr 키보드에서 `?`, `@` 등 특수문자 회귀 확인
- [x] 기기 인식 후 실제 Scrcpy 시작 전 대기 옵션(0~60초, 기본 1초)

## 배포 준비

- [x] `dist\DX Manager` 폴더와 버전별 x64 ZIP 패키징 스크립트
- [x] 생성된 공개 패키지 최종 배포 체크리스트
- [x] 패키징 전 Debug/Release의 logs/screenshot 테스트 파일 자동 제거
- [x] 준비된 한국어/영어 스크린샷을 README와 사용 설명서에 배치
- [x] 번들 구성요소 버전 확인과 제3자 라이선스 원문/고지 포함
- [x] 한국어 Q&A 초안 사용자 검토 및 수정
- [x] 한국어 Q&A 승인 후 독립 영문 FAQ로 번역
- [x] Windows 7 SP1~11 v1.1.0/1.2.0 최종 회귀 테스트
  - [x] Windows 11 실제 기기 관리형 Unicode 단일 파일 전송과 proxy 전달
  - [x] x64 Release 빌드 및 v1.1.0 ZIP 구성 검증
  - [x] Windows 7 SP1~11 Scrcpy 4.1 DeX/단일창 실행과 종료 UI 회귀
  - [x] Windows 7/11 한글·Unicode 복수 파일과 폴더 전체 전송 확인
  - [x] 사용자 대상 경로, 파일·폴더 충돌 이름, 취소와 독립 상태창 확인
  - [x] 관리형 전송 끄기 후 새 창에서 Scrcpy 순정 동작 복귀
  - [x] 설정·진단의 실제 ADB `Version ...` 값 표시 확인
- [x] GitHub 첫 공개 Release 게시 및 저장소 public 전환
- [x] v1.3.0 공개 Release 게시와 DX Companion 번들 배포
- [x] DX Manager 자체 소스 라이선스를 MIT로 확정

별도 설치 프로그램은 보류한다. v1.3.0 WinGet manifest는 등록됐다.
- [ ] 별도 `winget-pkgs` 작업 사본에서 v2.0.0 manifest의 제출·승인 상태를
  확인하고, 필요하면 공개 자산 URL과 SHA-256으로 갱신한다.

## 독립 FAQ 반영 항목

1. 비정상 종료 뒤 휴대폰에 작은 보조 화면이 남았을 때 제거 방법
2. 삼성 브라우저 자동 실행 전에 앱을 강제 종료하는 이유
3. 휴대폰을 두 대 이상 연결했을 때 v2의 독립 세션과 연결 정책
4. 1600×900보다 낮은 해상도에서 DeX UI 일부가 잘리는 이유
5. 해상도/DPI에 따라 바탕화면 배치와 배경화면이 달라지는 이유
6. 최근 앱 화면의 데스크톱이 이상할 때 새 데스크톱으로 복구하는 방법
7. DPI를 120보다 낮게 설정할 수 없는 이유
8. Scrcpy 서버 push 속도가 낮게 표시될 때 USB 케이블 확인
9. 무선 ADB 연결/재연결 실패 시 네트워크와 포트 점검
10. `unauthorized`, `offline` 장치와 RSA 승인 문제
11. Scrcpy 4.0에서 오른쪽 Shift를 왼쪽 Shift로 보정하는 이유
12. 기기 연결 후 시작 대기와 프로세스 제한시간의 차이
13. 사용 중 USB가 분리됐을 때 정리와 같은 휴대폰 재연결 동작
14. 휴대폰 화면 끄기와 stay-awake 옵션의 동작
15. 비정상 종료 뒤 충전 중 화면이 계속 켜질 때 절전모드 해제 복구
16. Windows 7 요구사항과 실행 파일만 복사하면 안 되는 이유
17. 관리형 드래그 앤 드롭 파일 전송, Unicode 이름, 충돌과 순정 전환
18. One UI 7.x 이하에서 검은 DeX 창이 나타날 수 있는 이유
19. ADB 공통 `1.0.41` 문구와 실제 `Version ...` 빌드 값의 차이
20. 금융·게임·DRM 앱의 실행 거부 또는 검은 화면
21. DeX에서 실행한 앱이 휴대폰 화면에서 열리는 경우
22. HID 마우스 사용 중 포인터가 scrcpy 창에 잡히는 동작
23. DX Companion 설치·서명 검증과 권한 버튼 비활성화
24. DX Companion 휴대폰→PC 파일·폴더 전송 준비와 사용 방법

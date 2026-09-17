# DX Manager v2.0.1

## English

DX Manager v2.0.1 is a maintenance release that fixes a DeX settings display
regression introduced with per-device settings in v2.0.0.

### Fixed

- Reload the selected phone's DeX resolution, DPI, bitrate, FPS, and launch
  options after the first physical-device identity is established.
- Prevent the legacy default values shown before device identification from
  being saved over a phone's existing DeX profile during initial selection.
- Preserve the same protection when multiple phones are already connected at
  startup. Single-Window settings remain independent and unchanged.

### DX Companion compatibility

DX Companion is unchanged in this release. The package continues to bundle the
verified DX Companion 2.0.0 APK (versionCode 6), and DX Manager still validates
its package name, version, SHA-256, and official signing certificate before
installation or privileged use.

### Compatibility

- 64-bit Windows 7 SP1, 8.1, 10, or 11
- .NET Framework 4.6.2 or later
- Bundled scrcpy 4.1
- Samsung Galaxy device with DeX support

Extract the complete portable folder and run `DXManager.exe`. Do not copy the
executable by itself.

### SHA-256

- `DX-Manager-v2.0.1-win-x64.zip`: `5F9C5A6AF6199D38458F6266869DDBACC58722A3A915696FF02935CF8965B2C1`
- `DXManager.exe`: `F94C6EDC43DEDF742E0885E63A8E7D1B0104385B36A29F20D1BC55495FF466EB`
- Bundled `DX-Companion.apk`:
  `7CD40017789E22440DCA0291AB0C45ADB564A19D8A623E669F373395536B880F`

### VirusTotal verification

The published `DXManager.exe` was rescanned after the Microsoft Defender
false-positive correction. VirusTotal currently reports **0 detections**;
all listed security vendors, including Microsoft, show **Undetected**:
https://www.virustotal.com/gui/file/f94c6edc43dedf742e0885e63a8e7d1b0104385b36a29f20d1bc55495ff466eb

---

## 한국어

DX Manager v2.0.1은 v2.0.0의 기기별 설정 도입 뒤 발생한 DeX 설정 표시 회귀를
수정한 유지보수 릴리스입니다.

### 수정 사항

- 첫 물리 휴대폰 identity가 확정된 뒤 선택 기기의 DeX 해상도·DPI·비트레이트·
  FPS와 실행 옵션을 다시 불러와 정확히 표시합니다.
- 기기 식별 전에 표시한 공통 기본값이 최초 선택 과정에서 기존 기기별 DeX
  프로필을 덮어쓰지 않도록 수정했습니다.
- 여러 휴대폰이 시작부터 연결된 경우에도 같은 보호를 적용합니다. 단일창의
  기기별 설정과 동작은 변경하지 않았습니다.

### DX Companion 호환성

이번 릴리스에서 DX Companion은 변경되지 않았습니다. 검증된 DX Companion
2.0.0 APK(versionCode 6)를 계속 포함하며, DX Manager는 설치 또는 권한 기능
사용 전에 package 이름, 버전, SHA-256과 공식 서명 인증서를 계속 검증합니다.

### 호환성

- 64비트 Windows 7 SP1, 8.1, 10 또는 11
- .NET Framework 4.6.2 이상
- 번들 scrcpy 4.1
- Samsung DeX 지원 Galaxy 기기

포터블 폴더 전체의 압축을 푼 뒤 `DXManager.exe`를 실행하십시오. 실행 파일만
따로 복사하지 마십시오.

### SHA-256

- `DX-Manager-v2.0.1-win-x64.zip`: `5F9C5A6AF6199D38458F6266869DDBACC58722A3A915696FF02935CF8965B2C1`
- `DXManager.exe`: `F94C6EDC43DEDF742E0885E63A8E7D1B0104385B36A29F20D1BC55495FF466EB`
- 번들 `DX-Companion.apk`:
  `7CD40017789E22440DCA0291AB0C45ADB564A19D8A623E669F373395536B880F`

### VirusTotal 검증

Microsoft Defender 오탐 해제 후 게시된 `DXManager.exe`를 다시 검사했습니다.
VirusTotal은 현재 **탐지 0건**이며 Microsoft를 포함한 전체 보안 엔진이
**Undetected**로 표시됩니다:
https://www.virustotal.com/gui/file/f94c6edc43dedf742e0885e63a8e7d1b0104385b36a29f20d1bc55495ff466eb

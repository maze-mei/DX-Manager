# DX Manager for macOS

DX Manager for macOS is distributed as a portable ZIP package. Extract the
complete archive and run it from the extracted folder. The package includes the
DX Manager runtime, scrcpy 4.1, ADB, and the scrcpy server. Homebrew and a
separate .NET installation are not required.

The portable packages require **macOS 14 Sonoma or later**.

## English

### 1. Choose the correct package

- **Apple Silicon Mac:** choose the package labeled **arm64**. This applies to
  Macs with an Apple M-series chip.
- **Intel Mac:** choose the package labeled **x64** or **x86_64**.

If you are not sure which Mac you have, open the Apple menu and select **About
This Mac**. The window shows either an Apple chip or an Intel processor.

The two packages provide the same DX Manager functions. Only their processor
architecture is different.

### 2. Verify the downloaded ZIP

Download the matching `.zip.sha256` file from the same GitHub Release and keep
it beside the ZIP. Before extracting, open Terminal in that folder and run:

```bash
release_version="<version>"
shasum -a 256 -c "DX-Manager-v${release_version}-macos-arm64.zip.sha256"
```

When reading this source document on GitHub, replace `<version>` with the
Release version shown in the downloaded filename, for example `2.0.1`. The
README inside a packaged ZIP already contains the exact package version.
For an Intel Mac, replace `arm64` with `x64`. Continue only when the command
prints `OK`. If it reports a mismatch, do not run the ZIP; download both files
again from the Release.

### 3. Extract and keep the complete folder

1. Extract the entire ZIP archive to a folder where your user account can write
   files, such as **Downloads** or **Documents**.
2. Keep all extracted files and subfolders together.
3. Do not move only `Start DX Manager.command` or only the DX Manager
   executable. The launcher uses the bundled runtime, scrcpy 4.1, ADB, and
   related files from this folder.

### 4. Prepare the Galaxy phone

1. On the phone, open **Settings > About phone > Software information**.
2. Tap **Build number** seven times to enable Developer options.
3. Open **Settings > Developer options** and enable **USB debugging**.
4. Unlock the phone and connect it to the Mac with a USB cable that supports
   data transfer.
5. When the phone displays **Allow USB debugging?**, verify the computer and
   approve the RSA key. You may select **Always allow from this computer** if
   this is your Mac.

DX Manager cannot control a device shown as `unauthorized`. If the RSA prompt
does not appear, reconnect the cable while the phone is unlocked and check the
USB debugging setting again.

### 5. Start DX Manager

Double-click `Start DX Manager.command` in the extracted folder. A Terminal
window opens and displays DX Manager's text menu.

Use the menu as follows:

- Press `1` to start DeX mode.
- Press `2` to stop the active DeX session and remove the virtual display.
- Press `Q` to exit DX Manager.

Keep the phone connected while starting or stopping the session. Selecting `Q`
also stops active DX Manager windows and requests removal of the DeX virtual
display. Wait for the final cleanup message before disconnecting the cable. If
the message says that a cleanup step could not be confirmed, reconnect the
phone and use `2` before exiting again.

### 6. First launch and Gatekeeper

This package is not currently signed with an Apple Developer ID or notarized by
Apple. On the first launch, macOS may block the launcher or display a warning.

If you obtained the package from a source you trust, try either of these
approval methods:

- Control-click `Start DX Manager.command`, select **Open**, and confirm
  **Open**.
- After macOS blocks the launch, open **System Settings > Privacy & Security**
  and use **Open Anyway** for the blocked item.

The wording and location of these controls can differ by macOS version. Without
Apple Developer ID signing and notarization, a completely warning-free launch
on every supported Mac and macOS version cannot be guaranteed.

### Package contents and limits

- Bundled DX Manager runtime for the selected Mac architecture
- Bundled scrcpy 4.1, ADB, and scrcpy server
- No Homebrew requirement
- No separate .NET installation requirement
- No DX Companion APK in the current macOS package

The Intel executable has been checked under Rosetta on Apple Silicon, while a
complete Galaxy DeX session on physical Intel Mac hardware still needs direct
verification.

Do not add, replace, or delete bundled ADB and scrcpy files unless the package
instructions for a later version explicitly require it.

---

## 한국어

DX Manager macOS 버전은 포터블 ZIP 패키지로 배포됩니다. ZIP 전체를 풀고
압축을 푼 폴더에서 실행하십시오. 패키지에는 DX Manager 런타임, scrcpy 4.1,
ADB와 scrcpy 서버가 포함됩니다. Homebrew와 별도의 .NET 설치는 필요하지
않습니다.

포터블 패키지의 최소 운영체제는 **macOS 14 Sonoma**입니다.

### 1. Mac에 맞는 패키지 선택

- **Apple Silicon Mac:** **arm64**로 표시된 패키지를 선택하십시오. Apple M
  시리즈 칩이 탑재된 Mac이 여기에 해당합니다.
- **Intel Mac:** **x64** 또는 **x86_64**로 표시된 패키지를 선택하십시오.

Mac의 종류를 모르면 Apple 메뉴에서 **이 Mac에 관하여**를 여십시오. 이 화면에
Apple 칩 또는 Intel 프로세서가 표시됩니다.

두 패키지의 DX Manager 기능은 같습니다. 프로세서 아키텍처만 다릅니다.

### 2. 다운로드한 ZIP 확인

같은 GitHub Release에서 ZIP과 이름이 같은 `.zip.sha256` 파일도 내려받아 ZIP
옆에 둡니다. 압축을 풀기 전에 해당 폴더에서 터미널을 열고 다음 명령을
실행하십시오.

```bash
release_version="<version>"
shasum -a 256 -c "DX-Manager-v${release_version}-macos-arm64.zip.sha256"
```

이 원본 문서를 GitHub에서 읽는 경우 `<version>`을 내려받은 파일명에 표시된
Release 버전(예: `2.0.1`)으로 바꾸십시오. 패키지 ZIP 안의 README에는 실제
패키지 버전이 이미 반영됩니다.
Intel Mac에서는 `arm64`를 `x64`로 바꿉니다. 결과에 `OK`가 표시된 경우에만
계속하십시오. 불일치가 표시되면 실행하지 말고 Release에서 두 파일을 다시
내려받으십시오.

### 3. ZIP 전체 압축 해제 및 폴더 유지

1. ZIP 전체를 **다운로드** 또는 **문서**처럼 사용자 계정이 파일을 쓸 수
   있는 폴더에 압축 해제하십시오.
2. 압축을 푼 모든 파일과 하위 폴더를 한 폴더 안에 그대로 유지하십시오.
3. `Start DX Manager.command` 또는 DX Manager 실행 파일만 따로 옮기지
   마십시오. 실행기는 같은 폴더에 포함된 런타임, scrcpy 4.1, ADB와 관련
   파일을 사용합니다.

### 4. Galaxy 휴대폰 준비

1. 휴대폰에서 **설정 > 휴대전화 정보 > 소프트웨어 정보**를 여십시오.
2. **빌드번호**를 일곱 번 눌러 개발자 옵션을 활성화하십시오.
3. **설정 > 개발자 옵션**에서 **USB 디버깅**을 켜십시오.
4. 휴대폰 잠금을 해제하고 데이터 전송을 지원하는 USB 케이블로 Mac과
   연결하십시오.
5. 휴대폰에 **USB 디버깅을 허용하시겠습니까?**가 표시되면 연결된 컴퓨터를
   확인한 뒤 RSA 키를 승인하십시오. 본인의 Mac이면 **이 컴퓨터에서 항상
   허용**을 선택할 수 있습니다.

장치 상태가 `unauthorized`이면 DX Manager에서 장치를 제어할 수 없습니다.
RSA 승인 화면이 나타나지 않으면 휴대폰 잠금이 해제된 상태에서 케이블을 다시
연결하고 USB 디버깅 설정을 확인하십시오.

### 5. DX Manager 시작

압축을 푼 폴더의 `Start DX Manager.command`를 더블클릭하십시오. 터미널 창이
열리고 DX Manager의 텍스트 메뉴가 표시됩니다.

메뉴는 다음과 같이 사용합니다.

- `1`: DeX 모드를 시작합니다.
- `2`: 실행 중인 DeX 세션을 중지하고 가상 디스플레이를 제거합니다.
- `Q`: DX Manager를 종료합니다.

세션을 시작하거나 중지하는 동안에는 휴대폰 연결을 유지하십시오. `Q`를
선택해도 DX Manager가 실행한 창을 중지하고 DeX 가상 디스플레이 제거를
요청합니다. 마지막 정리 완료 메시지를 확인한 뒤 케이블을 분리하십시오. 일부
정리 단계를 확인하지 못했다는 메시지가 나오면 휴대폰을 다시 연결하고 `2`로
중지한 뒤 다시 종료하십시오.

### 6. 최초 실행 및 Gatekeeper

현재 패키지는 Apple Developer ID 서명과 공증이 적용되지 않은 빌드입니다. 최초
실행 시 macOS가 실행을 차단하거나 경고를 표시할 수 있습니다.

신뢰하는 경로에서 받은 패키지라면 다음 승인 방법 중 하나를 시도하십시오.

- `Start DX Manager.command`를 Control-클릭하고 **열기**를 선택한 뒤 다시
  **열기**를 확인합니다.
- macOS가 실행을 차단한 뒤 **시스템 설정 > 개인정보 보호 및 보안**을 열고
  차단된 항목에 대해 **확인 없이 열기** 또는 **그래도 열기**를 선택합니다.

macOS 버전에 따라 메뉴 문구와 위치가 다를 수 있습니다. Apple Developer ID
서명과 공증이 없으면 지원 대상의 모든 Mac과 macOS 버전에서 경고가 전혀 없는
실행을 보장할 수 없습니다.

### 패키지 구성 및 범위

- 선택한 Mac 아키텍처용 DX Manager 런타임
- 번들 scrcpy 4.1, ADB와 scrcpy 서버
- Homebrew 설치 불필요
- 별도 .NET 설치 불필요
- 현재 macOS 패키지에는 DX Companion APK가 포함되지 않음

Intel 실행 파일은 Apple Silicon Mac의 Rosetta에서 기동을 확인했습니다. 실제
Intel Mac에서 Galaxy DeX 전체 흐름을 확인하는 실기 검증은 남아 있습니다.

이후 버전의 패키지 안내에서 명시적으로 요구하지 않는 한 번들 ADB와 scrcpy
파일을 추가, 교체 또는 삭제하지 마십시오.

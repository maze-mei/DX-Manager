#!/bin/bash
set -euo pipefail

APP_NAME="DX Manager"
BUNDLE_ID="com.mazemei.dxmanager"
VERSION="${DXM_VERSION:-2.0.1}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

HOST_ARCH="$(uname -m)"
case "${HOST_ARCH}" in
    arm64)
        RID="osx-arm64"
        SCRCPY_DIR_NAME="scrcpy-macos-aarch64-v3.3.4"
        PROXY_ARCH_DIR="osx-arm64"
        MACH_ARCH="arm64"
        ;;
    x86_64)
        RID="osx-x64"
        SCRCPY_DIR_NAME="scrcpy-macos-x86_64-v3.3.4"
        PROXY_ARCH_DIR="osx-x64"
        MACH_ARCH="x86_64"
        ;;
    *)
        echo "지원하지 않는 macOS CPU 아키텍처입니다: ${HOST_ARCH}" >&2
        exit 1
        ;;
esac

PUBLISH_DIR="${REPO_DIR}/publish/${RID}"
PROXY_PUBLISH_DIR="${REPO_DIR}/publish/adb-proxy-${RID}"
APP_DIR="${REPO_DIR}/dist/${APP_NAME}.app"
MACOS_DIR="${APP_DIR}/Contents/MacOS"
RESOURCES_DIR="${APP_DIR}/Contents/Resources"
TOOLS_DIR="${MACOS_DIR}/tools"

SCRCPY_SOURCE="${REPO_DIR}/tools/${SCRCPY_DIR_NAME}"
ADB_PROXY_REPO_SOURCE="${REPO_DIR}/tools/adb-proxy/${PROXY_ARCH_DIR}"
APP_ICON="${SCRIPT_DIR}/app_icon.icns"

echo "============================================================"
echo " ${APP_NAME} .app 번들 생성"
echo "============================================================"
echo "Host architecture : ${HOST_ARCH}"
echo "Runtime ID        : ${RID}"
echo "Version           : ${VERSION}"
echo "Publish directory : ${PUBLISH_DIR}"
echo "App bundle        : ${APP_DIR}"
echo

command -v dotnet >/dev/null 2>&1 || {
    echo "dotnet을 찾을 수 없습니다." >&2
    exit 1
}

command -v codesign >/dev/null 2>&1 || {
    echo "codesign을 찾을 수 없습니다." >&2
    exit 1
}

# -------------------------------------------------------------------
# 1. Clean previous outputs
# -------------------------------------------------------------------
rm -rf "${PUBLISH_DIR}"
rm -rf "${PROXY_PUBLISH_DIR}"
rm -rf "${APP_DIR}"

mkdir -p "${PUBLISH_DIR}"
mkdir -p "${PROXY_PUBLISH_DIR}"
mkdir -p "${MACOS_DIR}"
mkdir -p "${RESOURCES_DIR}"
mkdir -p "${TOOLS_DIR}"

# -------------------------------------------------------------------
# 2. Publish DX Manager for the current Mac architecture
#
# CopyBundledMacTools=false:
#   csproj의 tools/** 전체 자동 복사를 막고,
#   아래 단계에서 현재 CPU에 맞는 도구만 선택해서 넣는다.
# -------------------------------------------------------------------
echo "[1/8] DX Manager publish (${RID})..."

dotnet publish "${REPO_DIR}/DexManager.Mac/DexManager.Mac.csproj" \
    --configuration Release \
    --framework net8.0 \
    --runtime "${RID}" \
    --self-contained true \
    --output "${PUBLISH_DIR}" \
    -p:Version="${VERSION}" \
    -p:CopyBundledMacTools=false

[[ -f "${PUBLISH_DIR}/DXManager.Mac" ]] || {
    echo "DXManager.Mac publish 결과를 찾을 수 없습니다." >&2
    exit 1
}

# -------------------------------------------------------------------
# 3. Publish matching ADB proxy
# -------------------------------------------------------------------
echo "[2/8] ADB proxy publish (${RID})..."

dotnet publish "${REPO_DIR}/DexManager.AdbProxy/DexManager.AdbProxy.csproj" \
    --configuration Release \
    --framework net8.0 \
    --runtime "${RID}" \
    --self-contained true \
    --output "${PROXY_PUBLISH_DIR}" \
    -p:Version="${VERSION}"

[[ -f "${PROXY_PUBLISH_DIR}/DXMAdbProxy" ]] || {
    echo "DXMAdbProxy publish 결과를 찾을 수 없습니다." >&2
    exit 1
}

# -------------------------------------------------------------------
# 4. Copy the complete .NET publish output
#
# 기존 스크립트처럼 dll/deps/runtimeconfig/ko 를 하나씩 가정하지 않고
# dotnet publish가 만든 결과 전체를 복사한다.
# -------------------------------------------------------------------
echo "[3/8] .app Contents/MacOS 구성..."

cp -R "${PUBLISH_DIR}/." "${MACOS_DIR}/"
chmod +x "${MACOS_DIR}/DXManager.Mac"

# -------------------------------------------------------------------
# 5. Add architecture-specific tools
# -------------------------------------------------------------------
echo "[4/8] 현재 CPU용 bundled tools 구성..."

if [[ ! -d "${SCRCPY_SOURCE}" ]]; then
    echo "scrcpy 폴더를 찾을 수 없습니다: ${SCRCPY_SOURCE}" >&2
    exit 1
fi

# MacPathProvider.GetMacScrcpyDirectoryName()가 찾는 경로를 그대로 유지한다.
mkdir -p "${TOOLS_DIR}/${SCRCPY_DIR_NAME}"
cp -R "${SCRCPY_SOURCE}/." "${TOOLS_DIR}/${SCRCPY_DIR_NAME}/"
chmod +x "${TOOLS_DIR}/${SCRCPY_DIR_NAME}/scrcpy"

# MacPathProvider.GetMacArchDirectoryName()가 찾는 경로를 그대로 유지한다.
mkdir -p "${TOOLS_DIR}/adb-proxy/${PROXY_ARCH_DIR}"
cp -R "${PROXY_PUBLISH_DIR}/." \
    "${TOOLS_DIR}/adb-proxy/${PROXY_ARCH_DIR}/"
chmod +x "${TOOLS_DIR}/adb-proxy/${PROXY_ARCH_DIR}/DXMAdbProxy"

# ADB는 저장소의 Universal Binary를 공통으로 사용한다.
# 현재 저장소의 tools/adb/adb 는 x86_64 + arm64 두 아키텍처를 모두 포함한다.
ADB_SOURCE="${REPO_DIR}/tools/adb/adb"

if [[ ! -f "${ADB_SOURCE}" ]]; then
    echo "bundled ADB를 찾을 수 없습니다: ${ADB_SOURCE}" >&2
    exit 1
fi

ADB_ARCHS="$(lipo -archs "${ADB_SOURCE}" 2>/dev/null || true)"
if [[ " ${ADB_ARCHS} " != *" arm64 "* || " ${ADB_ARCHS} " != *" x86_64 "* ]]; then
    echo "tools/adb/adb가 Universal Binary가 아닙니다." >&2
    echo "  expected: arm64 x86_64" >&2
    echo "  actual  : ${ADB_ARCHS:-unknown}" >&2
    exit 1
fi

mkdir -p "${TOOLS_DIR}/adb"
cp "${ADB_SOURCE}" "${TOOLS_DIR}/adb/adb"
chmod +x "${TOOLS_DIR}/adb/adb"
echo "  Universal ADB 포함: ${ADB_ARCHS}"

# -------------------------------------------------------------------
# 6. Terminal launcher
# -------------------------------------------------------------------
echo "[5/8] Finder용 Terminal launcher 생성..."

cat > "${MACOS_DIR}/DXManager.command" <<'EOF'
#!/bin/bash
set -e
DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$DIR"
exec "$DIR/DXManager.Mac"
EOF
chmod +x "${MACOS_DIR}/DXManager.command"

cat > "${MACOS_DIR}/DXManager.Launcher" <<'EOF'
#!/bin/bash
DIR="$(cd "$(dirname "$0")" && pwd)"
exec /usr/bin/open -a Terminal "$DIR/DXManager.command"
EOF
chmod +x "${MACOS_DIR}/DXManager.Launcher"

# -------------------------------------------------------------------
# 7. Icon and Info.plist
# -------------------------------------------------------------------
echo "[6/8] Info.plist 및 아이콘 구성..."

if [[ -f "${APP_ICON}" ]]; then
    cp "${APP_ICON}" "${RESOURCES_DIR}/AppIcon.icns"
    ICON_PLIST_BLOCK='
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>'
else
    echo "  주의: ${APP_ICON} 없음 - 아이콘 없이 생성합니다."
    ICON_PLIST_BLOCK=""
fi

cat > "${APP_DIR}/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>${APP_NAME}</string>

    <key>CFBundleDisplayName</key>
    <string>${APP_NAME}</string>

    <key>CFBundleIdentifier</key>
    <string>${BUNDLE_ID}</string>

    <key>CFBundleVersion</key>
    <string>${VERSION}</string>

    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>

    <key>CFBundlePackageType</key>
    <string>APPL</string>

    <key>CFBundleExecutable</key>
    <string>DXManager.Launcher</string>
${ICON_PLIST_BLOCK}

    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>

    <key>NSHighResolutionCapable</key>
    <true/>

    <key>NSHumanReadableCopyright</key>
    <string>Copyright © 2026 mazemei. All rights reserved.</string>
</dict>
</plist>
EOF

plutil -lint "${APP_DIR}/Contents/Info.plist"

# -------------------------------------------------------------------
# 8. Architecture validation + ad-hoc signing
# -------------------------------------------------------------------
echo "[7/8] 아키텍처 검증 및 ad-hoc codesign..."

check_arch() {
    local binary="$1"
    local description="$2"

    [[ -f "${binary}" ]] || {
        echo "${description} 없음: ${binary}" >&2
        exit 1
    }

    local archs
    archs="$(lipo -archs "${binary}" 2>/dev/null || true)"

    if [[ " ${archs} " != *" ${MACH_ARCH} "* ]]; then
        echo "${description} 아키텍처 불일치" >&2
        echo "  expected: ${MACH_ARCH}" >&2
        echo "  actual  : ${archs:-unknown}" >&2
        echo "  file    : ${binary}" >&2
        exit 1
    fi

    echo "  OK ${description}: ${archs}"
}

check_arch "${MACOS_DIR}/DXManager.Mac" "DXManager.Mac"
check_arch \
    "${TOOLS_DIR}/${SCRCPY_DIR_NAME}/scrcpy" \
    "scrcpy"
check_arch \
    "${TOOLS_DIR}/adb-proxy/${PROXY_ARCH_DIR}/DXMAdbProxy" \
    "DXMAdbProxy"

check_arch "${TOOLS_DIR}/adb/adb" "adb"

# Nested Mach-O binaries first.
codesign --force --sign - --timestamp=none \
    "${MACOS_DIR}/DXManager.Mac"

codesign --force --sign - --timestamp=none \
    "${TOOLS_DIR}/${SCRCPY_DIR_NAME}/scrcpy"

codesign --force --sign - --timestamp=none \
    "${TOOLS_DIR}/adb-proxy/${PROXY_ARCH_DIR}/DXMAdbProxy"

if [[ -f "${TOOLS_DIR}/adb/adb" ]]; then
    codesign --force --sign - --timestamp=none \
        "${TOOLS_DIR}/adb/adb"
fi

# Local/ad-hoc signing for the app bundle.
# --deep is used here because this is a locally assembled bundle containing nested tools.
codesign --force --deep --sign - --timestamp=none "${APP_DIR}"

codesign --verify --deep --strict "${APP_DIR}"

# Remove quarantine attributes that may have been inherited from downloaded tools.
xattr -dr com.apple.quarantine "${APP_DIR}" 2>/dev/null || true

echo "[8/8] 완료"
echo
echo "============================================================"
echo " 생성 완료"
echo "============================================================"
echo "Architecture : ${HOST_ARCH} (${RID})"
echo "Publish      : ${PUBLISH_DIR}"
echo "App          : ${APP_DIR}"
echo
echo "실행:"
echo "  open \"${APP_DIR}\""
echo
echo "직접 실행 확인:"
echo "  \"${MACOS_DIR}/DXManager.Mac\" --version"
echo
echo "번들 구조 확인:"
echo "  find \"${APP_DIR}\" -maxdepth 5 -print"

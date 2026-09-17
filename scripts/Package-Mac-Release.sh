#!/bin/bash

set -euo pipefail

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "$script_directory/.." && pwd)"

version="2.0.1"
rid=""
output_directory="$repository_root/dist"
scrcpy_archive=""
skip_tests=0

scrcpy_version="4.1"
dotnet_runtime_version="8.0.30"
dotnet_license_url="https://raw.githubusercontent.com/dotnet/runtime/v8.0.30/LICENSE.TXT"
dotnet_license_sha256="cfc21f5e8bd655ae997eec916138b707b1d290b83272c02a95c9f821b8c87310"
dotnet_notices_url="https://raw.githubusercontent.com/dotnet/runtime/v8.0.30/THIRD-PARTY-NOTICES.TXT"
dotnet_notices_sha256="97c1a7b3da6a4c6ad516448719f45114b41a4d4c5aa300a944476e2e4f5da438"

usage() {
    cat <<'EOF'
Create one prebuilt, self-contained DX Manager portable ZIP for macOS.

Usage:
  scripts/Package-Mac-Release.sh --rid osx-arm64|osx-x64 [options]

Options:
  --version VERSION          Package version (default: 2.0.1)
  --output-dir DIRECTORY    Generated ZIP directory (default: ./dist)
  --scrcpy-archive FILE     Use an already downloaded official scrcpy archive
  --skip-tests              Skip the test suites (intended only after CI tests)
  -h, --help                Show this help

Examples:
  scripts/Package-Mac-Release.sh --rid osx-arm64
  scripts/Package-Mac-Release.sh --rid osx-x64 --version 2.0.1
EOF
}

fail() {
    echo "Package-Mac-Release: $*" >&2
    exit 1
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || fail "required command not found: $1"
}

sha256_file() {
    shasum -a 256 "$1" | awk '{print $1}'
}

verify_sha256() {
    local file_path="$1"
    local expected="$2"
    local description="$3"
    local actual
    actual="$(sha256_file "$file_path")"
    [[ "$actual" == "$expected" ]] ||
        fail "$description SHA-256 mismatch: expected $expected, got $actual"
}

download_verified() {
    local url="$1"
    local destination="$2"
    local expected="$3"
    local description="$4"
    curl --fail --location --retry 3 --silent --show-error \
        "$url" --output "$destination"
    verify_sha256 "$destination" "$expected" "$description"
}

assert_architecture() {
    local binary="$1"
    local expected="$2"
    local description="$3"
    local architectures
    architectures="$(lipo -archs "$binary" 2>/dev/null || true)"
    case " $architectures " in
        *" $expected "*) ;;
        *) fail "$description architecture is '$architectures', expected '$expected': $binary" ;;
    esac
}

assert_portable_dependencies() {
    local binary="$1"
    local dependencies
    dependencies="$(otool -L "$binary" | grep '^[[:space:]]' || true)"
    if printf '%s\n' "$dependencies" | grep -E '/opt/homebrew|/usr/local|/Users/|/private/tmp|/var/folders/' >/dev/null; then
        fail "build-machine dependency found in $binary:\n$dependencies"
    fi
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --rid)
            [[ $# -ge 2 ]] || fail "--rid requires a value"
            rid="$2"
            shift 2
            ;;
        --version)
            [[ $# -ge 2 ]] || fail "--version requires a value"
            version="$2"
            shift 2
            ;;
        --output-dir)
            [[ $# -ge 2 ]] || fail "--output-dir requires a value"
            output_directory="$2"
            shift 2
            ;;
        --scrcpy-archive)
            [[ $# -ge 2 ]] || fail "--scrcpy-archive requires a value"
            scrcpy_archive="$2"
            shift 2
            ;;
        --skip-tests)
            skip_tests=1
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            fail "unknown argument: $1"
            ;;
    esac
done

[[ "$(uname -s)" == "Darwin" ]] || fail "this packaging script must run on macOS"
[[ -n "$rid" ]] || fail "--rid is required"
[[ "$version" =~ ^[0-9A-Za-z][0-9A-Za-z._-]*$ ]] ||
    fail "version contains unsupported filename characters: $version"

case "$rid" in
    osx-arm64)
        package_arch="arm64"
        binary_arch="arm64"
        scrcpy_asset_arch="aarch64"
        scrcpy_sha256="20fd47c9014dd5e0fa77091f3cb7adbda8445a360c4584aeaa0150b5b3988ff3"
        ;;
    osx-x64)
        package_arch="x64"
        binary_arch="x86_64"
        scrcpy_asset_arch="x86_64"
        scrcpy_sha256="ee2a7223bc8dbdc4f482db1134bcf441178dafb833492b71ca4c22090c58ce72"
        ;;
    *)
        fail "unsupported RID '$rid'; use osx-arm64 or osx-x64"
        ;;
esac

require_command dotnet
require_command curl
require_command shasum
require_command tar
require_command lipo
require_command otool
require_command codesign
require_command ditto
require_command unzip
require_command zip
require_command date
require_command sed

output_directory="$(mkdir -p "$output_directory" && cd "$output_directory" && pwd)"
temporary_base="${TMPDIR:-/tmp}"
temporary_base="${temporary_base%/}"
work_root="$(mktemp -d "$temporary_base/dx-manager-macos-package.XXXXXX")"
output_staging=""

source_date_epoch="${SOURCE_DATE_EPOCH:-}"
if [[ -z "$source_date_epoch" ]] && command -v git >/dev/null 2>&1; then
    source_date_epoch="$(git -C "$repository_root" log -1 --format=%ct 2>/dev/null || true)"
fi
if [[ -z "$source_date_epoch" ]]; then
    source_date_epoch="946684800"
fi
[[ "$source_date_epoch" =~ ^[0-9]+$ ]] ||
    fail "SOURCE_DATE_EPOCH must contain Unix epoch seconds: $source_date_epoch"
normalized_timestamp="$(date -u -r "$source_date_epoch" +%Y%m%d%H%M.%S)"

cleanup() {
    case "$work_root" in
        "$temporary_base"/dx-manager-macos-package.*)
            rm -rf -- "$work_root"
            ;;
    esac
    case "${output_staging:-}" in
        "$output_directory"/.dx-manager-macos-output.*)
            rm -rf -- "$output_staging"
            ;;
    esac
}
trap cleanup EXIT
output_staging="$(mktemp -d "$output_directory/.dx-manager-macos-output.XXXXXX")"

if [[ $skip_tests -eq 0 ]]; then
    dotnet test "$repository_root/DexManager.Tests/DexManager.Tests.csproj" \
        --configuration Release --nologo
    dotnet run --project "$repository_root/DexManager.MultiDeviceTests/DexManager.MultiDeviceTests.csproj" \
        --configuration Release
fi

main_publish="$work_root/publish-main"
proxy_publish="$work_root/publish-proxy"

dotnet publish "$repository_root/DexManager.Mac/DexManager.Mac.csproj" \
    --configuration Release \
    --framework net8.0 \
    --runtime "$rid" \
    --self-contained true \
    --output "$main_publish" \
    -p:Version="$version" \
    -p:RuntimeFrameworkVersion="$dotnet_runtime_version" \
    -p:TreatWarningsAsErrors=true \
    -p:CopyBundledMacTools=false \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=false \
    -p:PublishTrimmed=false \
    -p:DebugSymbols=false \
    -p:DebugType=None

dotnet publish "$repository_root/DexManager.AdbProxy/DexManager.AdbProxy.csproj" \
    --configuration Release \
    --framework net8.0 \
    --runtime "$rid" \
    --self-contained true \
    --output "$proxy_publish" \
    -p:Version="$version" \
    -p:RuntimeFrameworkVersion="$dotnet_runtime_version" \
    -p:TreatWarningsAsErrors=true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=false \
    -p:PublishTrimmed=false \
    -p:DebugSymbols=false \
    -p:DebugType=None

[[ -f "$main_publish/DXManager.Mac" ]] || fail "DXManager.Mac publish output is missing"
[[ -f "$proxy_publish/DXMAdbProxy" ]] || fail "DXMAdbProxy publish output is missing"

scrcpy_archive_name="scrcpy-macos-$scrcpy_asset_arch-v$scrcpy_version.tar.gz"
scrcpy_download="$work_root/$scrcpy_archive_name"
if [[ -n "$scrcpy_archive" ]]; then
    [[ -f "$scrcpy_archive" ]] || fail "scrcpy archive not found: $scrcpy_archive"
    cp "$scrcpy_archive" "$scrcpy_download"
    verify_sha256 "$scrcpy_download" "$scrcpy_sha256" "scrcpy $scrcpy_version $package_arch archive"
else
    scrcpy_url="https://github.com/Genymobile/scrcpy/releases/download/v$scrcpy_version/$scrcpy_archive_name"
    download_verified "$scrcpy_url" "$scrcpy_download" "$scrcpy_sha256" \
        "scrcpy $scrcpy_version $package_arch archive"
fi

scrcpy_extract="$work_root/scrcpy"
mkdir -p "$scrcpy_extract"
tar -xzf "$scrcpy_download" -C "$scrcpy_extract"
scrcpy_root="$scrcpy_extract/scrcpy-macos-$scrcpy_asset_arch-v$scrcpy_version"
[[ -d "$scrcpy_root" ]] || fail "official scrcpy archive has an unexpected directory layout"
for required in scrcpy scrcpy-server adb LICENSE; do
    [[ -f "$scrcpy_root/$required" ]] || fail "official scrcpy archive is missing: $required"
done

package_parent="$work_root/package"
package_root="$package_parent/DX Manager"
mkdir -p "$package_root/tools/scrcpy" "$package_root/tools/adb-proxy" \
    "$package_root/config" "$package_root/licenses" "$package_root/docs"

cp "$main_publish/DXManager.Mac" "$package_root/DXManager.Mac"
cp "$proxy_publish/DXMAdbProxy" "$package_root/tools/adb-proxy/DXMAdbProxy"
cp -R "$scrcpy_root/." "$package_root/tools/scrcpy/"
cp "$repository_root/build/macos/Start DX Manager.command" \
    "$package_root/Start DX Manager.command"
cp "$repository_root/build/macos/PORTABLE_PACKAGE.txt" \
    "$package_root/PORTABLE_PACKAGE.txt"
sed "s/<version>/$version/g" \
    "$repository_root/docs/PACKAGE_README_MACOS.md" > "$package_root/README.md"
sed "s/<version>/$version/g" \
    "$repository_root/docs/MACOS_GUIDE.md" > "$package_root/docs/MACOS_GUIDE.md"
cp "$repository_root/DexManager/config/README.txt" "$package_root/config/README.txt"
cp "$repository_root/LICENSE" "$package_root/LICENSE"
cp "$repository_root/DexManager/licenses/THIRD_PARTY_NOTICES_MACOS.md" \
    "$package_root/licenses/THIRD_PARTY_NOTICES.md"
cp "$repository_root/DexManager/licenses/scrcpy-LICENSE.txt" "$package_root/licenses/"
cp "$repository_root/DexManager/licenses/LGPL-2.1-LICENSE.txt" "$package_root/licenses/"
cp "$repository_root/DexManager/licenses/SDL3-LICENSE.txt" "$package_root/licenses/"
cp "$repository_root/DexManager/licenses/zlib-LICENSE.txt" "$package_root/licenses/"
cp "$repository_root/DexManager/licenses/dav1d-LICENSE.txt" "$package_root/licenses/"

dotnet_license="$work_root/dotnet-LICENSE.txt"
dotnet_notices="$work_root/dotnet-THIRD-PARTY-NOTICES.txt"
download_verified "$dotnet_license_url" "$dotnet_license" \
    "$dotnet_license_sha256" ".NET Runtime $dotnet_runtime_version license"
download_verified "$dotnet_notices_url" "$dotnet_notices" \
    "$dotnet_notices_sha256" ".NET Runtime $dotnet_runtime_version third-party notices"
cp "$dotnet_license" "$package_root/licenses/dotnet-LICENSE.txt"
cp "$dotnet_notices" "$package_root/licenses/dotnet-THIRD-PARTY-NOTICES.txt"

find "$package_root" -type d -exec chmod 755 {} +
find "$package_root" -type f -exec chmod 644 {} +
chmod 755 "$package_root/DXManager.Mac" \
    "$package_root/Start DX Manager.command" \
    "$package_root/tools/adb-proxy/DXMAdbProxy" \
    "$package_root/tools/scrcpy/scrcpy" \
    "$package_root/tools/scrcpy/adb"

for required in \
    "DXManager.Mac" \
    "Start DX Manager.command" \
    "PORTABLE_PACKAGE.txt" \
    "README.md" \
    "LICENSE" \
    "tools/adb-proxy/DXMAdbProxy" \
    "tools/scrcpy/scrcpy" \
    "tools/scrcpy/scrcpy-server" \
    "tools/scrcpy/adb" \
    "licenses/THIRD_PARTY_NOTICES.md" \
    "licenses/dotnet-LICENSE.txt" \
    "licenses/dotnet-THIRD-PARTY-NOTICES.txt"; do
    [[ -f "$package_root/$required" ]] || fail "required package file is missing: $required"
done

if grep -F '<version>' "$package_root/README.md" \
    "$package_root/docs/MACOS_GUIDE.md" >/dev/null; then
    fail "package documentation still contains an unresolved version placeholder"
fi

unexpected="$(find "$package_root" \
    \( -name 'settings.json*' -o -name '*.pdb' -o -name '*.dSYM' \
       -o -name '.DS_Store' -o -name '*.keystore' -o -name 'signing.properties' \
       -o -name '*.cs' -o -name '*.csproj' -o -name '*.sln' \) \
    -print -quit)"
[[ -z "$unexpected" ]] || fail "private, source, or debug file found in package: $unexpected"

symlink="$(find "$package_root" -type l -print -quit)"
[[ -z "$symlink" ]] || fail "symbolic links are not allowed in the portable package: $symlink"

assert_architecture "$package_root/DXManager.Mac" "$binary_arch" "DX Manager"
assert_architecture "$package_root/tools/adb-proxy/DXMAdbProxy" "$binary_arch" "ADB proxy"
assert_architecture "$package_root/tools/scrcpy/scrcpy" "$binary_arch" "scrcpy"
assert_architecture "$package_root/tools/scrcpy/adb" "$binary_arch" "ADB"

assert_portable_dependencies "$package_root/DXManager.Mac"
assert_portable_dependencies "$package_root/tools/adb-proxy/DXMAdbProxy"
assert_portable_dependencies "$package_root/tools/scrcpy/scrcpy"
assert_portable_dependencies "$package_root/tools/scrcpy/adb"

for signed_binary in \
    "$package_root/DXManager.Mac" \
    "$package_root/tools/adb-proxy/DXMAdbProxy"; do
    codesign --force --sign - --timestamp=none "$signed_binary"
    codesign --verify --strict "$signed_binary"
done

for upstream_binary in \
    "$package_root/tools/scrcpy/scrcpy" \
    "$package_root/tools/scrcpy/adb"; do
    if ! codesign --verify --strict "$upstream_binary" 2>/dev/null; then
        codesign --force --sign - --timestamp=none "$upstream_binary"
    fi
    codesign --verify --strict "$upstream_binary"
done

host_arch="$(uname -m)"
if [[ "${CI:-}" == "true" && "$host_arch" != "$binary_arch" ]]; then
    fail "CI runner architecture is $host_arch, expected $binary_arch for $rid"
fi
if [[ "$host_arch" == "$binary_arch" ]]; then
    smoke_home="$work_root/smoke-home"
    mkdir -p "$smoke_home"
    clean_path="/usr/bin:/bin:/usr/sbin:/sbin"

    version_output="$(HOME="$smoke_home" PATH="$clean_path" \
        "$package_root/DXManager.Mac" --version)"
    [[ "$version_output" == *"$version"* ]] ||
        fail "DX Manager version output does not contain $version: $version_output"
    HOME="$smoke_home" PATH="$clean_path" "$package_root/DXManager.Mac" --help >/dev/null
    proxy_output="$(HOME="$smoke_home" PATH="$clean_path" \
        "$package_root/tools/adb-proxy/DXMAdbProxy" --self-test)"
    [[ "$proxy_output" == *"$version"* ]] ||
        fail "ADB proxy self-test output does not contain $version: $proxy_output"
    scrcpy_output="$(HOME="$smoke_home" PATH="$clean_path" \
        "$package_root/tools/scrcpy/scrcpy" --version 2>&1)"
    [[ "$scrcpy_output" == *"scrcpy $scrcpy_version"* ]] ||
        fail "scrcpy version verification failed: $scrcpy_output"
    HOME="$smoke_home" PATH="$clean_path" \
        "$package_root/tools/scrcpy/adb" version >/dev/null
else
    echo "Skipping executable smoke tests: host is $host_arch, package is $binary_arch."
    echo "The matching GitHub Actions runner performs these tests before artifact upload."
fi

zip_path="$output_directory/DX-Manager-v$version-macos-$package_arch.zip"
checksum_path="$zip_path.sha256"
staged_zip="$output_staging/$(basename "$zip_path")"
staged_checksum="$output_staging/$(basename "$checksum_path")"
find "$package_root" -exec touch -h -t "$normalized_timestamp" {} +
(
    cd "$package_parent"
    find "DX Manager" -print | LC_ALL=C sort | \
        zip -X -q "$staged_zip" -@
)

archive_metadata="$(unzip -Z1 "$staged_zip" | \
    grep -E '(^|/)\._|^__MACOSX/' | head -n 1 || true)"
[[ -z "$archive_metadata" ]] ||
    fail "macOS metadata file found in portable ZIP: $archive_metadata"

extract_root="$work_root/reextract"
mkdir -p "$extract_root"
ditto -x -k --norsrc --noextattr --noqtn --noacl \
    "$staged_zip" "$extract_root"
reextracted="$extract_root/DX Manager"
[[ -x "$reextracted/DXManager.Mac" ]] || fail "ZIP did not preserve the DX Manager executable bit"
[[ -x "$reextracted/Start DX Manager.command" ]] || fail "ZIP did not preserve the launcher executable bit"
[[ -x "$reextracted/tools/adb-proxy/DXMAdbProxy" ]] || fail "ZIP did not preserve the proxy executable bit"
[[ -x "$reextracted/tools/scrcpy/scrcpy" ]] || fail "ZIP did not preserve the scrcpy executable bit"
[[ -x "$reextracted/tools/scrcpy/adb" ]] || fail "ZIP did not preserve the ADB executable bit"

if [[ "$host_arch" == "$binary_arch" ]]; then
    extracted_smoke_home="$work_root/extracted-smoke-home"
    extracted_smoke_tmp="$work_root/extracted-smoke-tmp"
    mkdir -p "$extracted_smoke_home" "$extracted_smoke_tmp"
    launcher_version="$(HOME="$extracted_smoke_home" \
        TMPDIR="$extracted_smoke_tmp" \
        PATH="/usr/bin:/bin:/usr/sbin:/sbin" \
        "$reextracted/Start DX Manager.command" --version 2>&1)"
    [[ "$launcher_version" == *"$version"* ]] ||
        fail "re-extracted launcher version check failed: $launcher_version"
    tui_output="$({ sleep 2; printf 'Q\n'; } | \
        HOME="$extracted_smoke_home" \
        TMPDIR="$extracted_smoke_tmp" \
        PATH="/usr/bin:/bin:/usr/sbin:/sbin" \
        "$reextracted/Start DX Manager.command" 2>&1)"
    [[ "$tui_output" == *"DX Manager stopped cleanly"* ]] ||
        fail "re-extracted TUI did not complete Q cleanup: $tui_output"
fi

zip_hash="$(sha256_file "$staged_zip")"
printf '%s  %s\n' "$zip_hash" "$(basename "$zip_path")" > "$staged_checksum"

previous_zip="$output_staging/previous.zip"
previous_checksum="$output_staging/previous.zip.sha256"
if [[ -e "$zip_path" || -e "$checksum_path" ]]; then
    [[ -f "$zip_path" && -f "$checksum_path" ]] ||
        fail "existing package output is incomplete; keep or remove the ZIP and checksum together"
    mv -- "$zip_path" "$previous_zip" ||
        fail "could not stage the previous portable ZIP for replacement"
    if ! mv -- "$checksum_path" "$previous_checksum"; then
        mv -- "$previous_zip" "$zip_path"
        fail "could not stage the previous checksum; previous outputs were restored"
    fi
fi

if ! mv -- "$staged_zip" "$zip_path"; then
    [[ ! -f "$previous_zip" ]] || mv -- "$previous_zip" "$zip_path"
    [[ ! -f "$previous_checksum" ]] || mv -- "$previous_checksum" "$checksum_path"
    fail "could not publish the verified portable ZIP"
fi
if ! mv -- "$staged_checksum" "$checksum_path"; then
    mv -- "$zip_path" "$staged_zip"
    [[ ! -f "$previous_zip" ]] || mv -- "$previous_zip" "$zip_path"
    [[ ! -f "$previous_checksum" ]] || mv -- "$previous_checksum" "$checksum_path"
    fail "could not publish the portable ZIP checksum; previous outputs were restored"
fi

echo "Portable macOS package: $zip_path"
echo "SHA-256: $zip_hash"
echo "Checksum file: $checksum_path"

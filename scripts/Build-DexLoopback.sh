#!/usr/bin/env bash
set -euo pipefail

# Build-only JDK 17 and pinned Google D8; neither is needed by a portable user.
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
java_root="${DXM_JAVA_HOME:-${JAVA_HOME:-}}"
if [[ -z "$java_root" || ! -x "$java_root/bin/javac" ]]; then
  echo "Set DXM_JAVA_HOME to a JDK 17 directory." >&2
  exit 1
fi
"$java_root/bin/javac" -version 2>&1 | grep -q '^javac 17\.' || { echo "JDK 17 required" >&2; exit 1; }
build_dir="$(mktemp -d "${TMPDIR:-/tmp}/dxm-loopback.XXXXXX")"
output_dir="${DXM_LOOPBACK_OUTPUT:-$repo_root/tools/loopback}"
r8_jar="${DXM_R8_JAR:-$build_dir/r8-8.7.18.jar}"
r8_sha="58366f77067207c39a17d469de7b05701d2877212a9c55201bcb0af43e59e903"
if [[ ! -f "$r8_jar" ]]; then
  curl --fail --location --retry 2 --output "$r8_jar" \
    'https://dl.google.com/dl/android/maven2/com/android/tools/r8/8.7.18/r8-8.7.18.jar'
fi
if command -v sha256sum >/dev/null; then hash_command=(sha256sum); else hash_command=(shasum -a 256); fi
actual_sha="$("${hash_command[@]}" "$r8_jar" | cut -d ' ' -f 1)"
[[ "$actual_sha" == "$r8_sha" ]] || { echo "D8 checksum mismatch" >&2; exit 1; }
mkdir -p "$build_dir/classes" "$build_dir/tests" "$build_dir/dex" "$output_dir"
sources=(); tests=()
while IFS= read -r file; do sources+=("$file"); done < <(find "$repo_root/DXLoopback/src" -name '*.java' | LC_ALL=C sort)
while IFS= read -r file; do tests+=("$file"); done < <(find "$repo_root/DXLoopback/test" -name '*.java' | LC_ALL=C sort)
"$java_root/bin/javac" --release 8 -encoding UTF-8 -g:none -Werror -Xlint:all -d "$build_dir/classes" "${sources[@]}"
"$java_root/bin/javac" --release 8 -encoding UTF-8 -g:none -Werror -Xlint:all -cp "$build_dir/classes" -d "$build_dir/tests" "${tests[@]}"
"$java_root/bin/java" -cp "$build_dir/classes:$build_dir/tests" com.dxmanager.loopback.ProtocolTests
"$java_root/bin/jar" --create --file "$build_dir/classes.jar" --no-manifest --date=2026-01-01T00:00:00Z -C "$build_dir/classes" .
"$java_root/bin/java" -cp "$r8_jar" com.android.tools.r8.D8 --release --no-desugaring --min-api 31 \
  --lib "$java_root" --output "$build_dir/dex" "$build_dir/classes.jar"
"$java_root/bin/jar" --create --file "$output_dir/dxm-loopback.jar" --no-manifest --date=2026-01-01T00:00:00Z \
  -C "$build_dir/dex" classes.dex -C "$repo_root/DXLoopback" LICENSE -C "$repo_root/DXLoopback" NOTICE
(cd "$output_dir" && "${hash_command[@]}" dxm-loopback.jar > dxm-loopback.jar.sha256)
cp "$repo_root/DXLoopback/LICENSE" "$output_dir/LICENSE"
cp "$repo_root/DXLoopback/NOTICE" "$output_dir/NOTICE"
echo "Built $output_dir/dxm-loopback.jar (host tests only; Samsung device validation is separate)."
echo "Build intermediates: $build_dir"

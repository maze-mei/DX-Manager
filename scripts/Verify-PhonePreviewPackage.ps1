param([string]$Archive = '')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$productRoot = Join-Path $repo 'DexManager\bin\Release'
$versionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $productRoot 'DXManager.exe'))
$version = '{0}.{1}.{2}' -f $versionInfo.FileMajorPart, $versionInfo.FileMinorPart, $versionInfo.FileBuildPart
if (!$Archive) { $Archive = Join-Path $repo 'dist\phone-preview-ci\DX-Manager-win-x64.zip' }
$Archive = [IO.Path]::GetFullPath($Archive)
$hash = (Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash.ToLowerInvariant()
if ((Get-Content -LiteralPath ($Archive + '.sha256') -Raw).Trim() -cne "$hash  $([IO.Path]::GetFileName($Archive))") { throw 'Archive checksum mismatch.' }
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('dxm-package-' + [Guid]::NewGuid().ToString('N'))
$prefix = 'DX Manager v' + $version + '/'
$zip = [IO.Compression.ZipFile]::OpenRead($Archive)
try {
    $names = @{}
    foreach ($entry in $zip.Entries) {
        $name = $entry.FullName.Replace('\', '/')
        if (!$name.StartsWith($prefix, [StringComparison]::Ordinal) -or $name.Contains('../') -or $name.Contains(':') -or $names.ContainsKey($name)) {
            throw "Unexpected or duplicate archive path: $name"
        }
        $names[$name] = $true
    }
} finally { $zip.Dispose() }
[IO.Compression.ZipFile]::ExtractToDirectory($Archive, $scratch)
$root = Join-Path $scratch $prefix.TrimEnd('/')
$runtime = Get-Content (Join-Path $PSScriptRoot 'scrcpy-runtime.json') -Raw | ConvertFrom-Json
$companion = Get-Content (Join-Path $repo 'companion\release.json') -Raw | ConvertFrom-Json
# This independent allowlist intentionally does not read the packaging script.
$allowed = @('DXManager.exe', 'DXManager.exe.config', 'ko/DXManager.resources.dll',
    'config/README.txt', 'README.md', 'LICENSE', 'PHONE_PREVIEW_MODE.md',
    'tools/adb-proxy/DXMAdbProxy.exe', 'tools/companion/DX-Companion.apk',
    'tools/companion/Pretendard-LICENSE.txt', 'tools/loopback/dxm-loopback.jar',
    'tools/loopback/dxm-loopback.jar.sha256', 'tools/loopback/LICENSE', 'tools/loopback/NOTICE')
$allowed += @('adb.exe', 'AdbWinApi.dll', 'AdbWinUsbApi.dll', 'libwinpthread-1.dll') | ForEach-Object { 'tools/adb/legacy/' + $_ }
$allowed += @('DX-Manager-MIT-LICENSE.txt', 'THIRD_PARTY_NOTICES.md', 'scrcpy-LICENSE.txt',
    'LGPL-2.1-LICENSE.txt', 'MinGW-w64-winpthreads-LICENSE.txt', 'SDL3-LICENSE.txt',
    'zlib-LICENSE.txt', 'dav1d-LICENSE.txt', 'Fluent-System-Icons-LICENSE.txt', 'Pretendard-LICENSE.txt') | ForEach-Object { 'licenses/' + $_ }
$allowed += $runtime.Files | ForEach-Object { 'tools/scrcpy/' + $_.Name }
$actual = @(Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object { [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\','/') })
if (Compare-Object ($allowed | Sort-Object -Unique) ($actual | Sort-Object)) { throw 'Portable ZIP file set does not match the independent allowlist.' }
foreach ($binary in @('DXManager.exe','tools/adb-proxy/DXMAdbProxy.exe')) {
    $identity = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $root $binary))
    $source = Join-Path $productRoot $binary
    $sourceIdentity = [Diagnostics.FileVersionInfo]::GetVersionInfo($source)
    if ($identity.ProductVersion -cne $sourceIdentity.ProductVersion -or $identity.FileVersion -cne $sourceIdentity.FileVersion -or
        (Get-FileHash (Join-Path $root $binary)).Hash -ne (Get-FileHash $source).Hash) { throw "Packaged binary differs from the build: $binary" }
}
foreach ($file in $runtime.Files) {
    if ((Get-FileHash (Join-Path $root ('tools/scrcpy/' + $file.Name))).Hash -ine $file.Sha256) { throw "Runtime pin mismatch: $($file.Name)" }
}
if ((Get-FileHash (Join-Path $root 'tools/companion/DX-Companion.apk')).Hash -ine $companion.ApkSha256) { throw 'Companion APK mismatch.' }
$pin = [regex]::Match((Get-Content (Join-Path $repo 'DexManager/Services/LoopbackDexService.cs') -Raw), 'HelperSha256 = "([a-f0-9]{64})"').Groups[1].Value
if ($pin.Length -ne 64 -or (Get-FileHash (Join-Path $root 'tools/loopback/dxm-loopback.jar')).Hash -ine $pin) { throw 'Helper pin mismatch.' }
foreach ($file in $actual) {
    if ($file -match '(?i)(settings\.json|\.pdb$|\.p12$|\.keystore$|signing\.properties|(^|/)(logs|screenshot)/)') { throw "Private or development file in archive: $file" }
    if ($file -match '\.(md|txt|config)$') {
        $content = Get-Content (Join-Path $root $file) -Raw
        if ($content -match 'github_pat_[A-Za-z0-9_]+|gh[pousr]_[A-Za-z0-9]+|-----BEGIN .*PRIVATE KEY-----|\{\{[A-Z_]+\}\}') { throw "Secret or unresolved template in $file" }
    }
}
$report = [pscustomobject]@{
    Version = $version; FeatureContributor = 'asyouplz'
    Archive = [IO.Path]::GetFileName($Archive); Sha256 = $hash
    Files = ($actual | Sort-Object); Scrcpy = $runtime.Version; Companion = $companion.VersionName
    PhysicalGalaxyValidation = 'Not performed by package verification'
}
[IO.File]::WriteAllText((Join-Path ([IO.Path]::GetDirectoryName($Archive)) 'package-verification.json'), ($report | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
Write-Host "Verified phone-preview package $($version): $($actual.Count) allowlisted files; SHA256 $hash"
# Remove only this unique scratch directory created for archive inspection.
Remove-Item -LiteralPath $scratch -Recurse -Force

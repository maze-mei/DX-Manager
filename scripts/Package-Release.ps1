param(
    [string]$Version = "",
    [string]$CompanionSharedRoot = "",
    [switch]$SkipBuild,
    [switch]$NoArchive,
    [string]$TargetFrameworkRootPath = "",
    [string]$OutputRoot = "",
    [ValidateNotNullOrEmpty()][string]$PackageDirectoryName = "DX Manager"
)

$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$solutionPath = Join-Path $repoRoot "DexManager.sln"
$releaseRoot = Join-Path $repoRoot "DexManager\bin\Release"
$distRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $repoRoot "dist"
} else { [IO.Path]::GetFullPath($OutputRoot) }
if ($PackageDirectoryName.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) {
    throw "The package directory must be a single valid folder name."
}
$packageRoot = Join-Path $distRoot $PackageDirectoryName
. (Join-Path $PSScriptRoot 'Get-CompanionRoot.ps1')
$companionSharedRoot = Get-DxmCompanionRoot -RepositoryRoot $repoRoot -OverrideRoot $CompanionSharedRoot
$companionRelease = Get-Content -LiteralPath (Join-Path $companionSharedRoot 'release.json') -Raw | ConvertFrom-Json
$companionApkSource = Join-Path $companionSharedRoot $companionRelease.ApkRelativePath
$companionApkSha256 = $companionRelease.ApkSha256
$companionFontLicense = Join-Path $companionSharedRoot 'licenses\Pretendard-LICENSE.txt'

function Assert-ChildPath([string]$Parent, [string]$Child) {
    $parentPath = [IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    $childPath = [IO.Path]::GetFullPath($Child)
    if (!$childPath.StartsWith(
        $parentPath,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the distribution folder: $childPath"
    }
}

function Find-MSBuild {
    $fromPath = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($fromPath) { return $fromPath.Source }

    $candidates = @(
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }

    throw "MSBuild was not found. Build Release in Visual Studio, then run this script with -SkipBuild."
}

if (Get-Process DXManager -ErrorAction SilentlyContinue | Where-Object {
    try {
        !$_.HasExited -and $_.Path -and (
            [IO.Path]::GetFullPath($_.Path).Equals((Join-Path $releaseRoot 'DXManager.exe'),[StringComparison]::OrdinalIgnoreCase) -or
            [IO.Path]::GetFullPath($_.Path).Equals((Join-Path $packageRoot 'DXManager.exe'),[StringComparison]::OrdinalIgnoreCase))
    } catch { $false }
}) {
    throw "DX Manager is running. Exit it before creating a release package."
}

$releaseAdbPath = Join-Path $releaseRoot "tools\scrcpy\adb.exe"
$releaseAdbProcesses = Get-Process adb -ErrorAction SilentlyContinue |
    Where-Object {
        try {
            [string]::Equals(
                [IO.Path]::GetFullPath($_.Path),
                [IO.Path]::GetFullPath($releaseAdbPath),
                [StringComparison]::OrdinalIgnoreCase)
        }
        catch {
            $false
        }
    }
if ($releaseAdbProcesses -and (Test-Path -LiteralPath $releaseAdbPath)) {
    & $releaseAdbPath kill-server
    Start-Sleep -Milliseconds 300
    $remainingReleaseAdb = Get-Process adb -ErrorAction SilentlyContinue |
        Where-Object {
            try {
                [string]::Equals(
                    [IO.Path]::GetFullPath($_.Path),
                    [IO.Path]::GetFullPath($releaseAdbPath),
                    [StringComparison]::OrdinalIgnoreCase)
            }
            catch {
                $false
            }
        }
    if ($remainingReleaseAdb) {
        throw "The bundled ADB server is still running. Stop it before packaging."
    }
}

# Preserve user logs/captures in development outputs. Only the explicit release
# payload below is copied; private configuration and runtime data are excluded.

if (!$SkipBuild) {
    $msbuild = Find-MSBuild
    $arguments = @(
        $solutionPath,
        "/t:Rebuild",
        "/p:Configuration=Release",
        "/m"
    )
    $frameworkRoot = $TargetFrameworkRootPath
    if ([string]::IsNullOrWhiteSpace($frameworkRoot)) {
        $frameworkRoot = $env:DXM_TARGET_FRAMEWORK_ROOT
    }
    if ([string]::IsNullOrWhiteSpace($frameworkRoot)) {
        $frameworkRoot = Join-Path $repoRoot ".build-tools\net462\build"
    }
    if (Test-Path -LiteralPath $frameworkRoot) {
        $frameworkRoot = [IO.Path]::GetFullPath($frameworkRoot)
        $referenceAssembly = Join-Path $frameworkRoot `
            ".NETFramework\v4.6.2\mscorlib.dll"
        if (!(Test-Path -LiteralPath $referenceAssembly -PathType Leaf)) {
            throw "The supplied .NET Framework root does not contain the v4.6.2 reference assemblies: $frameworkRoot"
        }
        $arguments += "/p:TargetFrameworkRootPath=$frameworkRoot"
    }

    $arguments += "/p:CompanionSharedRoot=$companionSharedRoot"
    & $msbuild @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed with exit code $LASTEXITCODE."
    }
}

& (Join-Path $PSScriptRoot 'Sync-ScrcpyRuntime.ps1') -DestinationRoot (Join-Path $releaseRoot 'tools\scrcpy')

$requiredOutput = @(
    "DXManager.exe",
    "DXManager.exe.config",
    "ko",
    "licenses",
    "tools"
)
foreach ($item in $requiredOutput) {
    $path = Join-Path $releaseRoot $item
    if (!(Test-Path -LiteralPath $path)) {
        throw "Required Release output is missing: $path"
    }
}
$requiredFiles = @(
    "ko\DXManager.resources.dll",
    "tools\adb-proxy\DXMAdbProxy.exe",
    "tools\loopback\dxm-loopback.jar",
    "tools\loopback\dxm-loopback.jar.sha256",
    "tools\loopback\LICENSE",
    "tools\loopback\NOTICE",
    "tools\adb\legacy\adb.exe",
    "tools\adb\legacy\AdbWinApi.dll",
    "tools\adb\legacy\AdbWinUsbApi.dll",
    "tools\adb\legacy\libwinpthread-1.dll",
    "tools\scrcpy\scrcpy.exe",
    "tools\scrcpy\scrcpy-server",
    "tools\scrcpy\adb.exe",
    "tools\scrcpy\AdbWinApi.dll",
    "tools\scrcpy\AdbWinUsbApi.dll",
    "tools\scrcpy\SDL3.dll",
    "tools\scrcpy\avcodec-63.dll",
    "tools\scrcpy\avformat-63.dll",
    "tools\scrcpy\avutil-61.dll",
    "tools\scrcpy\swresample-7.dll",
    "tools\scrcpy\libusb-1.0.dll",
    "licenses\DX-Manager-MIT-LICENSE.txt",
    "licenses\THIRD_PARTY_NOTICES.md",
    "licenses\scrcpy-LICENSE.txt",
    "licenses\LGPL-2.1-LICENSE.txt",
    "licenses\MinGW-w64-winpthreads-LICENSE.txt",
    "licenses\SDL3-LICENSE.txt",
    "licenses\zlib-LICENSE.txt",
    "licenses\dav1d-LICENSE.txt",
    "licenses\Fluent-System-Icons-LICENSE.txt",
    "licenses\Pretendard-LICENSE.txt"
)
foreach ($item in $requiredFiles) {
    $path = Join-Path $releaseRoot $item
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required Release file is missing: $path"
    }
}

if (!(Test-Path -LiteralPath $companionApkSource -PathType Leaf)) {
    throw "The signed DX Companion Release APK is missing: $companionApkSource"
}
$actualCompanionHash = (Get-FileHash `
    -LiteralPath $companionApkSource `
    -Algorithm SHA256).Hash
if (![string]::Equals(
    $actualCompanionHash,
    $companionApkSha256,
    [StringComparison]::OrdinalIgnoreCase)) {
    throw "The signed DX Companion APK hash does not match the verified shared official APK."
}

if (!(Test-Path -LiteralPath $companionFontLicense -PathType Leaf)) {
    throw "The Companion font license is missing."
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    $assemblyInfo = Get-Content -LiteralPath (Join-Path $repoRoot "DexManager\Properties\AssemblyInfo.cs")
    $versionLine = $assemblyInfo | Where-Object {
        $_ -match 'AssemblyInformationalVersion\("([^"]+)"\)'
    } | Select-Object -First 1
    if (!$versionLine -or $versionLine -notmatch 'AssemblyInformationalVersion\("([^"]+)"\)') {
        throw "AssemblyInformationalVersion was not found."
    }
    $Version = $Matches[1]
}

if ($Version -notmatch '^[0-9A-Za-z][0-9A-Za-z._-]*$') {
    throw "The version contains unsupported filename characters: $Version"
}

# Keep the asset name stable for GitHub /releases/latest/download links.
$zipPath = Join-Path $distRoot "DX-Manager-win-x64.zip"
Assert-ChildPath $distRoot $packageRoot
Assert-ChildPath $distRoot $zipPath

New-Item -ItemType Directory -Path $distRoot -Force | Out-Null
if (Test-Path -LiteralPath $packageRoot) {
    if ((Get-Item -LiteralPath $packageRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'The package directory must not be a reparse point.'
    }
    if (@(Get-ChildItem -LiteralPath $packageRoot -Recurse -Attributes ReparsePoint).Count) {
        throw 'The package directory must not contain reparse points.'
    }
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}
if (!$NoArchive -and (Test-Path -LiteralPath $zipPath)) {
    Remove-Item -LiteralPath $zipPath -Force
}
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
$packageConfig = Join-Path $packageRoot "config"
New-Item -ItemType Directory -Path $packageConfig -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot "DexManager\config\README.txt") `
    -Destination $packageConfig

# Copy only runtime files and required notices; never recurse through build output.
$runtimePin = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'scrcpy-runtime.json') -Raw | ConvertFrom-Json
$payloadFiles = @('DXManager.exe', 'DXManager.exe.config') + $requiredFiles + @(
    $runtimePin.Files | ForEach-Object { 'tools\scrcpy\' + $_.Name }
)
foreach ($relative in ($payloadFiles | Sort-Object -Unique)) {
    $target = Join-Path $packageRoot $relative
    Assert-ChildPath $packageRoot $target
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $releaseRoot $relative) -Destination $target -Force
}

$packageCompanionDirectory = Join-Path $packageRoot "tools\companion"
New-Item -ItemType Directory -Path $packageCompanionDirectory -Force |
    Out-Null
Copy-Item -LiteralPath $companionApkSource `
    -Destination (Join-Path $packageCompanionDirectory "DX-Companion.apk")

# Include the license for the font embedded in the unchanged Companion APK.
Copy-Item -LiteralPath $companionFontLicense -Destination $packageCompanionDirectory

# Release builds create a PDB for the managed ADB helper under tools. Keep
# debugging symbols out of the public portable package.
Get-ChildItem -LiteralPath $packageRoot -Filter "*.pdb" -File -Recurse |
    Remove-Item -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\PHONE_PREVIEW_MODE.md') -Destination $packageRoot
$packageReadme = Join-Path $packageRoot "README.md"
Copy-Item -LiteralPath (Join-Path $repoRoot "docs\PACKAGE_README.md") `
    -Destination $packageReadme
Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination $packageRoot

$packageMarkdownFiles = @($packageReadme)
# A BOM keeps the Korean sections readable in the Windows 7 version of
# Notepad while remaining valid UTF-8 for Markdown viewers.
$packageUtf8 = New-Object Text.UTF8Encoding($true)
foreach ($markdownPath in $packageMarkdownFiles) {
    $markdown = [IO.File]::ReadAllText($markdownPath)
    $markdown = $markdown.Replace('{{VERSION}}', $Version).
        Replace('{{SCRCPY_VERSION}}', $runtimePin.Version).
        Replace('{{COMPANION_VERSION}}', $companionRelease.VersionName)
    $markdown = $markdown.Replace(
        "DexManager/licenses/THIRD_PARTY_NOTICES.md",
        "licenses/THIRD_PARTY_NOTICES.md")
    [IO.File]::WriteAllText($markdownPath, $markdown, $packageUtf8)
}

Get-ChildItem -LiteralPath $packageRoot -Filter ".gitkeep" -File -Recurse |
    Remove-Item -Force

$unexpectedPdb = Get-ChildItem -LiteralPath $packageRoot -Filter "*.pdb" `
    -File -Recurse | Select-Object -First 1
if ($unexpectedPdb) {
    throw "Debug symbols must not be included in the package: $($unexpectedPdb.FullName)"
}

if (!$NoArchive) {
    Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal
    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText($zipPath + '.sha256', "$zipHash  $([IO.Path]::GetFileName($zipPath))`n", [Text.UTF8Encoding]::new($false))
}

Write-Host "Release folder: $packageRoot"
if (!$NoArchive) { Write-Host "Release archive: $zipPath" }

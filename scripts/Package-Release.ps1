param(
    [string]$Version = "",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$solutionPath = Join-Path $repoRoot "DexManager.sln"
$releaseRoot = Join-Path $repoRoot "DexManager\bin\Release"
$distRoot = Join-Path $repoRoot "dist"
$packageRoot = Join-Path $distRoot "DX Manager"

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

if (Get-Process DXManager -ErrorAction SilentlyContinue) {
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

foreach ($runtimeDirectory in @(
    (Join-Path $repoRoot "DexManager\bin\Debug\logs"),
    (Join-Path $repoRoot "DexManager\bin\Debug\screenshot"),
    (Join-Path $repoRoot "DexManager\bin\Debug\screenshots"),
    (Join-Path $repoRoot "DexManager\bin\Release\logs"),
    (Join-Path $repoRoot "DexManager\bin\Release\screenshot"),
    (Join-Path $repoRoot "DexManager\bin\Release\screenshots")
)) {
    Assert-ChildPath $repoRoot $runtimeDirectory
    if (Test-Path -LiteralPath $runtimeDirectory) {
        Get-ChildItem -LiteralPath $runtimeDirectory -File -Recurse |
            Remove-Item -Force
    }
}

if (!$SkipBuild) {
    $msbuild = Find-MSBuild
    $arguments = @(
        $solutionPath,
        "/t:Rebuild",
        "/p:Configuration=Release",
        "/m"
    )
    $localFrameworkRoot = Join-Path $repoRoot ".build-tools\net462\build"
    if (Test-Path -LiteralPath $localFrameworkRoot) {
        $arguments += "/p:TargetFrameworkRootPath=$localFrameworkRoot"
    }

    & $msbuild @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed with exit code $LASTEXITCODE."
    }
}

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

$zipPath = Join-Path $distRoot "DX-Manager-v$Version-win-x64.zip"
Assert-ChildPath $distRoot $packageRoot
Assert-ChildPath $distRoot $zipPath

New-Item -ItemType Directory -Path $distRoot -Force | Out-Null
if (Test-Path -LiteralPath $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null

foreach ($item in $requiredOutput) {
    Copy-Item -LiteralPath (Join-Path $releaseRoot $item) -Destination $packageRoot -Recurse -Force
}
$packageReadme = Join-Path $packageRoot "README.md"
Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination $packageReadme
Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination $packageRoot

$packageDocs = Join-Path $packageRoot "docs"
New-Item -ItemType Directory -Path $packageDocs -Force | Out-Null
foreach ($document in @(
    "USER_GUIDE_KO.md",
    "USER_GUIDE_EN.md",
    "FAQ_KO.md",
    "FAQ_EN.md"
)) {
    Copy-Item -LiteralPath (Join-Path $repoRoot "docs\$document") -Destination $packageDocs
}
$imageRoot = Join-Path $repoRoot "docs\images"
if (Test-Path -LiteralPath $imageRoot) {
    Copy-Item -LiteralPath $imageRoot -Destination $packageDocs -Recurse -Force
}

$packageImageRoot = Join-Path $packageDocs "images"
New-Item -ItemType Directory -Path $packageImageRoot -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot "DexManager\Resources\DXManager_256.png") `
    -Destination (Join-Path $packageImageRoot "DXManager_256.png") -Force

# The repository README and user guides link to source-tree paths. Rewrite only
# the generated package copies so their local links also work after extraction.
$packageMarkdownFiles = @($packageReadme) + @(
    Get-ChildItem -LiteralPath $packageDocs -Filter "*.md" -File |
        ForEach-Object { $_.FullName }
)
$utf8WithoutBom = New-Object Text.UTF8Encoding($false)
foreach ($markdownPath in $packageMarkdownFiles) {
    $markdown = [IO.File]::ReadAllText($markdownPath)
    $markdown = $markdown.Replace(
        "DexManager/Resources/DXManager_256.png",
        "docs/images/DXManager_256.png")
    $markdown = $markdown.Replace(
        "DexManager/licenses/THIRD_PARTY_NOTICES.md",
        "licenses/THIRD_PARTY_NOTICES.md")
    $markdown = $markdown.Replace(
        "[DexManager/README.md](DexManager/README.md)",
        "[DexManager/README.md](https://github.com/maze-mei/DX-Manager/blob/main/DexManager/README.md)")
    [IO.File]::WriteAllText($markdownPath, $markdown, $utf8WithoutBom)
}

Get-ChildItem -LiteralPath $packageRoot -Filter ".gitkeep" -File -Recurse |
    Remove-Item -Force

Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Release folder: $packageRoot"
Write-Host "Release archive: $zipPath"

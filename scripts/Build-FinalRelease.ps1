param(
    [switch]$SkipBuild,
    [string]$OutputRoot = '',
    [string]$TargetFrameworkRootPath = ''
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$assemblyInfo = Get-Content -LiteralPath (Join-Path $repo 'DexManager\Properties\AssemblyInfo.cs') -Raw
$versionMatch = [regex]::Match($assemblyInfo, 'AssemblyInformationalVersion\("([0-9A-Za-z][0-9A-Za-z._-]*)"\)')
if (!$versionMatch.Success) { throw 'A valid AssemblyInformationalVersion was not found.' }
$version = $versionMatch.Groups[1].Value
$folder = "DX Manager v$version"
if (!$OutputRoot) { $OutputRoot = Join-Path $repo 'dist' }
$dist = [IO.Path]::GetFullPath($OutputRoot)
$target = Join-Path $dist $folder
$archive = Join-Path $dist 'DX-Manager-win-x64.zip'

foreach ($path in @($target, $archive)) {
    if (Test-Path -LiteralPath $path) {
        throw "Use a new output folder so the existing distribution is preserved: $path"
    }
}
& (Join-Path $PSScriptRoot 'Verify-CompanionSnapshot.ps1')
$packageParameters = @{
    SkipBuild = $SkipBuild
    Version = $version
    OutputRoot = $dist
    PackageDirectoryName = $folder
    TargetFrameworkRootPath = $TargetFrameworkRootPath
    CompanionSharedRoot = Join-Path $repo 'companion'
}
& (Join-Path $PSScriptRoot 'Package-Release.ps1') @packageParameters

foreach ($path in @('docs','logs','screenshot','receive','config\settings.json',
    'config\onboarding.json','config\update-check.json','config\update-notice.json')) {
    if (Test-Path -LiteralPath (Join-Path $target $path)) {
        throw "Unexpected private or development path in distribution: $path"
    }
}
Write-Host "Windows executable: $(Join-Path $target 'DXManager.exe')"
Write-Host "Windows archive: $archive"

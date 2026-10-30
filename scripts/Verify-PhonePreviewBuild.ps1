param(
    [switch]$SkipBuild,
    [string]$TargetFrameworkRootPath = $env:DXM_TARGET_FRAMEWORK_ROOT,
    [string]$JavaHome = $env:JAVA_HOME_17_X64,
    [string]$R8Jar = $env:DXM_R8_JAR
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$validation = Join-Path $repo '.build-tools\phone-preview-validation'
New-Item -ItemType Directory -Force -Path $validation | Out-Null
$runRoot = Join-Path $validation ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runRoot | Out-Null
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (!$msbuild) { throw 'Visual Studio MSBuild was not found.' }
$compiler = Join-Path ([IO.Path]::GetDirectoryName($msbuild)) 'Roslyn\csc.exe'
if (!$TargetFrameworkRootPath) { $TargetFrameworkRootPath = Join-Path $repo '.build-tools\net462\build' }
if (!$JavaHome) { $JavaHome = $env:JAVA_HOME }
& (Join-Path $PSScriptRoot 'Verify-CompanionSnapshot.ps1')
$productRoot = Join-Path $repo 'DexManager\bin\Release'
& (Join-Path $PSScriptRoot 'Sync-ScrcpyRuntime.ps1') -DestinationRoot (Join-Path $productRoot 'tools\scrcpy')
if (!$SkipBuild) {
    & $msbuild (Join-Path $repo 'DexManager.sln') /t:Build /p:Configuration=Release `
        "/p:TargetFrameworkRootPath=$TargetFrameworkRootPath" "/p:CompanionSharedRoot=$repo\companion" `
        /p:UseSharedCompilation=false /m:1 /nodeReuse:false /warnaserror *> (Join-Path $runRoot 'build.log')
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath (Join-Path $runRoot 'build.log') -Tail 30
        throw 'Windows phone-preview build failed.'
    }
}
$product = Join-Path $productRoot 'DXManager.exe'

$results = @()
function Invoke-PhonePreviewCheck([string]$Name, [string]$Executable, [string[]]$Arguments = @()) {
    $log = Join-Path $runRoot ($Name + '.log')
    & $Executable @Arguments *> $log
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath $log | Where-Object { $_ -notlike 'IMAGE:*' } | Select-Object -Last 20
        throw "Windows phone-preview check failed: $Name"
    }
    $last = Get-Content -LiteralPath $log | Where-Object { $_ -notlike 'IMAGE:*' } | Select-Object -Last 1
    Write-Host $last
    $script:results += [pscustomobject]@{ Test = $Name; Result = $last }
}
Invoke-PhonePreviewCheck 'foundation' (Join-Path $repo 'DexManager.MultiDeviceTests\bin\Release\DexManager.MultiDeviceTests.exe')
Invoke-PhonePreviewCheck 'loopback-source' 'dotnet' @('run', '--project', (Join-Path $repo 'DexManager.WindowsTests'), '-c', 'Release', '-p:UseSharedCompilation=false')
$rebuilt = Join-Path $runRoot 'rebuilt-helper'
& (Join-Path $PSScriptRoot 'Build-DexLoopback.ps1') -JavaHome $JavaHome -R8Jar $R8Jar -OutputDirectory $rebuilt
if ((Get-FileHash -LiteralPath (Join-Path $rebuilt 'dxm-loopback.jar')).Hash -ne
    (Get-FileHash -LiteralPath (Join-Path $repo 'tools\loopback\dxm-loopback.jar')).Hash) {
    throw 'The rebuilt helper is not byte-identical to the pinned bundle.'
}
foreach ($name in @('ExperimentalFrameRateIntegrationTests', 'PhonePreviewIntegrationTests')) {
    # Every fixture receives isolated settings, captures and loopback-only test data.
    $root = Join-Path $runRoot $name
    foreach ($folder in @('', 'ko', 'tools\companion', 'tools\loopback', 'update-state-fixture', 'consent-Light', 'consent-Dark')) {
        New-Item -ItemType Directory -Force -Path (Join-Path $root $folder) | Out-Null
    }
    Copy-Item -LiteralPath $product -Destination (Join-Path $root 'DXManager.exe')
    Copy-Item -LiteralPath (Join-Path $productRoot 'tools\adb-proxy\DXMAdbProxy.exe') -Destination (Join-Path $root 'DXMAdbProxy.exe')
    Copy-Item -LiteralPath (Join-Path $productRoot 'ko\DXManager.resources.dll') -Destination (Join-Path $root 'ko\DXManager.resources.dll')
    Copy-Item -LiteralPath (Join-Path $repo 'companion\release\DX-Companion.apk') -Destination (Join-Path $root 'tools\companion\DX-Companion.apk')
    Copy-Item -LiteralPath (Join-Path $repo 'tools\loopback\dxm-loopback.jar') -Destination (Join-Path $root 'tools\loopback\dxm-loopback.jar')
    $target = Join-Path $root ($name + '.exe')
    Copy-Item -LiteralPath (Join-Path $productRoot 'DXManager.exe.config') -Destination ($target + '.config')
    & $compiler /nologo /target:exe /platform:x64 "/out:$target" "/reference:$product" `
        (Join-Path $repo ('DexManager.MultiDeviceTests\' + $name + '.cs')) *> (Join-Path $root 'compile.log')
    if ($LASTEXITCODE -ne 0) { Get-Content (Join-Path $root 'compile.log'); throw "Could not compile $name" }
    $arguments = @()
    Invoke-PhonePreviewCheck $name $target $arguments
    foreach ($line in Get-Content -LiteralPath (Join-Path $runRoot ($name + '.log'))) {
        if ($line -match '^IMAGE:([A-Za-z0-9-]+):([A-Za-z0-9+/=]+)$') {
            [IO.File]::WriteAllBytes((Join-Path $root ($Matches[1] + '.png')), [Convert]::FromBase64String($Matches[2]))
        }
    }
}
$record = [pscustomobject]@{
    Version = [Diagnostics.FileVersionInfo]::GetVersionInfo($product).ProductVersion
    FeatureContributor = 'asyouplz'
    SourceCommit = (& git -C $repo rev-parse HEAD)
    ProductSha256 = (Get-FileHash -LiteralPath $product).Hash.ToLowerInvariant()
    Checks = $results
    PhysicalGalaxyValidation = 'Not performed by this script'
}
[IO.File]::WriteAllText((Join-Path $validation 'verification-summary.json'), ($record | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
Write-Host "Verified Windows phone-preview build; evidence: $runRoot"

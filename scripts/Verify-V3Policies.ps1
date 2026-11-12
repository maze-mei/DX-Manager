$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fixture=Join-Path $repo '.build-tools\v3-test-validation'
foreach($part in @('ko','saved-settings\config','v3-log-preference\logs','config','DX Manager v3.0.0\config')){[IO.Directory]::CreateDirectory((Join-Path $fixture $part))|Out-Null}
$product=Join-Path $repo 'DexManager\bin\Release\DXManager.exe'
Copy-Item -LiteralPath $product -Destination (Join-Path $fixture 'DXManager.exe') -Force
Copy-Item -LiteralPath (Join-Path $repo 'DexManager\bin\Release\ko\DXManager.resources.dll') -Destination (Join-Path $fixture 'ko\DXManager.resources.dll') -Force
$test=Join-Path $fixture 'ExperimentalFrameRateIntegrationTests.exe'
Copy-Item -LiteralPath ($product+'.config') -Destination ($test+'.config') -Force
$compiler=Join-Path ([IO.Path]::GetDirectoryName($env:DXM_MSBUILD)) 'Roslyn\csc.exe'
& $compiler /nologo /target:exe /platform:x64 ("/out:"+$test) ("/reference:"+$product) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll (Join-Path $repo 'DexManager.MultiDeviceTests\ExperimentalFrameRateIntegrationTests.cs') (Join-Path $repo 'DexManager.MultiDeviceTests\V3SettingsAndTransitionTests.cs') *> (Join-Path $fixture 'build.log')
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $fixture 'build.log') -Tail 18;throw 'v3/FPS validation compilation failed.'}
& $test *> (Join-Path $fixture 'tests.log')
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $fixture 'tests.log') -Tail 22;throw 'v3/FPS behavior checks failed.'}
Get-Content -LiteralPath (Join-Path $fixture 'tests.log') -Tail 1

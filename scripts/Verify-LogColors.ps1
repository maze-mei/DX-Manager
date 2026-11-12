$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fixture=Join-Path $repo '.build-tools\ui-metrics-polish\log-ui'
[IO.Directory]::CreateDirectory($fixture)|Out-Null
[IO.Directory]::CreateDirectory((Join-Path $fixture 'ko'))|Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'DexManager\bin\Release\DXManager.exe') -Destination (Join-Path $fixture 'DXManager.exe') -Force
Copy-Item -LiteralPath (Join-Path $repo 'DexManager\bin\Release\ko\DXManager.resources.dll') -Destination (Join-Path $fixture 'ko\DXManager.resources.dll') -Force
foreach($name in @('plain-Light.log','plain-Dark.log')){[IO.File]::WriteAllText((Join-Path $fixture $name),'',[Text.UTF8Encoding]::new($false))}
$test=Join-Path $fixture 'LogColorIntegrationTests.exe'
Copy-Item -LiteralPath (Join-Path $repo 'DexManager\bin\Release\DXManager.exe.config') -Destination ($test+'.config') -Force
$compiler=Join-Path ([IO.Path]::GetDirectoryName($env:DXM_MSBUILD)) 'Roslyn\csc.exe'
& $compiler /nologo /target:exe /platform:x64 ("/out:"+$test) ("/reference:"+(Join-Path $fixture 'DXManager.exe')) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll (Join-Path $repo 'DexManager.MultiDeviceTests\LogColorIntegrationTests.cs') *> (Join-Path $fixture 'build.log')
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $fixture 'build.log');throw 'Log UI tests compile failed.'}
& $test *> (Join-Path $fixture 'tests.log')
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $fixture 'tests.log') -Tail 20;throw 'Log UI/path tests failed.'}
Get-Content -LiteralPath (Join-Path $fixture 'tests.log') -Tail 1

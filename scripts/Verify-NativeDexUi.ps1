$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fixture=Join-Path $repo '.build-tools\native-dex\ui-verify'
[IO.Directory]::CreateDirectory($fixture)|Out-Null
foreach($language in @('Korean','English')){foreach($theme in @('Light','Dark')){foreach($suffix in @('','config','logs')){[IO.Directory]::CreateDirectory((Join-Path $fixture (('state-'+$language+'-'+$theme)+'\'+$suffix)))|Out-Null}}}
Copy-Item -LiteralPath (Join-Path $repo 'DexManager\bin\Release\DXManager.exe') -Destination (Join-Path $fixture 'DXManager.exe') -Force
[IO.Directory]::CreateDirectory((Join-Path $fixture 'ko'))|Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'DexManager\bin\Release\ko\DXManager.resources.dll') -Destination (Join-Path $fixture 'ko\DXManager.resources.dll') -Force
$compiler=Join-Path ([IO.Path]::GetDirectoryName($env:DXM_MSBUILD)) 'Roslyn\csc.exe'
$test=Join-Path $fixture 'NativeSettingsUiTests.exe'
Copy-Item -LiteralPath (Join-Path $repo 'DexManager\bin\Release\DXManager.exe.config') -Destination ($test+'.config') -Force
& $compiler /nologo /target:exe /platform:x64 ("/out:"+$test) ("/reference:"+(Join-Path $fixture 'DXManager.exe')) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll (Join-Path $repo 'DexManager.MultiDeviceTests\NativeSettingsUiTests.cs') *> (Join-Path $fixture 'build.log')
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $fixture 'build.log');throw 'Native settings UI fixture build failed.'}
& $test *> (Join-Path $fixture 'tests.log')
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $fixture 'tests.log') -Tail 15;throw 'Native settings UI checks failed.'}
Get-Content -LiteralPath (Join-Path $fixture 'tests.log') -Tail 1

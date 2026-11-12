param()
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$env:TEMP=Join-Path $repo '.build-tools\tmp'
$env:TMP=$env:TEMP
[IO.Directory]::CreateDirectory($env:TEMP)|Out-Null
$fixture=Join-Path $repo '.build-tools\autolog\verify'
[IO.Directory]::CreateDirectory($fixture)|Out-Null
$state=[IO.Path]::GetFullPath((Join-Path $fixture 'state'))
if(!$state.StartsWith($repo+'\.build-tools\autolog\verify\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected log fixture state path.'}
if(Test-Path -LiteralPath $state){
 if(((Get-Item -LiteralPath $state).Attributes -band [IO.FileAttributes]::ReparsePoint) -or @(Get-ChildItem -LiteralPath $state -Recurse -Attributes ReparsePoint).Count){throw 'Log fixtures must not contain reparse points.'}
 Remove-Item -LiteralPath $state -Recurse -Force
}
foreach($name in @('','live','manual-directory','concurrent','limits','unavailable','blocked-writer','child-crash','child-graceful')){[IO.Directory]::CreateDirectory((Join-Path $state $name))|Out-Null}
$product=Join-Path $repo 'DexManager\bin\Release\DXManager.exe'
Copy-Item -LiteralPath $product -Destination (Join-Path $fixture 'DXManager.exe') -Force
$test=Join-Path $fixture 'AutomaticLogIntegrationTests.exe'
Copy-Item -LiteralPath ($product+'.config') -Destination ($test+'.config') -Force
$compiler=Join-Path ([IO.Path]::GetDirectoryName($env:DXM_MSBUILD)) 'Roslyn\csc.exe'
& $compiler /nologo /target:exe /platform:x64 ("/out:"+$test) ("/reference:"+(Join-Path $fixture 'DXManager.exe')) (Join-Path $repo 'DexManager.MultiDeviceTests\AutomaticLogIntegrationTests.cs') *> (Join-Path $fixture 'build.log')
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $fixture 'build.log');throw 'Automatic log fixture compilation failed.'}
& $test *> (Join-Path $fixture 'checks.log')
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $fixture 'checks.log') -Tail 22;throw 'Automatic log checks failed.'}
Get-Content -LiteralPath (Join-Path $fixture 'checks.log') -Tail 1

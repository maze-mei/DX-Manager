param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$env:TEMP=Join-Path $repo '.build-tools\tmp'
$env:TMP=$env:TEMP
[IO.Directory]::CreateDirectory($env:TEMP)|Out-Null
$work=Join-Path $repo '.build-tools\native-dex'
$fixture=Join-Path $work 'validation'
[IO.Directory]::CreateDirectory($fixture)|Out-Null
$state=[IO.Path]::GetFullPath((Join-Path $fixture 'native-test-state'))
if(!$state.StartsWith($work+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected native fixture path.'}
if(Test-Path -LiteralPath $state){
 if(((Get-Item -LiteralPath $state).Attributes -band [IO.FileAttributes]::ReparsePoint) -or @(Get-ChildItem -LiteralPath $state -Recurse -Attributes ReparsePoint).Count){throw 'Native fixtures must not contain reparse points.'}
 Remove-Item -LiteralPath $state -Recurse -Force
}
[IO.Directory]::CreateDirectory($state)|Out-Null
$msbuild=$env:DXM_MSBUILD
$jdk=$env:JAVA_HOME
$android=Join-Path $env:ANDROID_SDK_ROOT 'platforms\android-36\android.jar'
$classes=Join-Path $work 'build\classes'
if(!$SkipBuild){
 & (Join-Path $PSScriptRoot 'Build-NativeDexHelper.ps1') *> (Join-Path $work 'java-build.log')
 if(!$?){Get-Content -LiteralPath (Join-Path $work 'java-build.log') -Tail 15;throw 'Native Java build failed.'}
 & $msbuild (Join-Path $repo 'DexManager.sln') /t:Build /p:Configuration=Release ("/p:TargetFrameworkRootPath="+$env:DXM_TARGET_FRAMEWORK_ROOT) /m:1 /nodeReuse:false *> (Join-Path $work 'windows-build.log')
 if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $work 'windows-build.log') -Tail 18;throw 'Native Windows build failed.'}
}
& (Join-Path $jdk 'bin\javac.exe') --release 8 -encoding UTF-8 -classpath ($classes+';'+$android) -d $classes (Join-Path $repo 'NativeDex\tests\NativeDexTests.java') (Join-Path $repo 'NativeDex\tests\NativeRetentionTests.java') (Join-Path $repo 'NativeDex\tests\NativeAudioPolicyTests.java') (Join-Path $repo 'NativeDex\tests\NativeSessionFallbackTests.java') (Join-Path $repo 'NativeDex\tests\NativeBluetoothPreserverTests.java') (Join-Path $repo 'NativeDex\tests\NativeBrowserCleanupTests.java') (Join-Path $repo 'NativeDex\tests\NativeMetricsVerifierTests.java')
if($LASTEXITCODE -ne 0){throw 'Native Java test compilation failed.'}
& (Join-Path $jdk 'bin\java.exe') -cp ($classes+';'+$android) NativeDexTests *> (Join-Path $work 'java-tests.log')
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $work 'java-tests.log') -Tail 18;throw 'Native Java tests failed.'}
Get-Content -LiteralPath (Join-Path $work 'java-tests.log') -Tail 1
$product=Join-Path $repo 'DexManager\bin\Release\DXManager.exe'
Copy-Item -LiteralPath $product -Destination (Join-Path $fixture 'DXManager.exe') -Force
Copy-Item -LiteralPath ($product+'.config') -Destination (Join-Path $fixture 'NativeDexIntegrationTests.exe.config') -Force
[IO.Directory]::CreateDirectory((Join-Path $fixture 'tools\native-dex'))|Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'DexManager\bin\Release\tools\native-dex\dxm-native-dex.jar') -Destination (Join-Path $fixture 'tools\native-dex\dxm-native-dex.jar') -Force
[IO.Directory]::CreateDirectory((Join-Path $fixture 'tools\companion'))|Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'companion\release\DX-Companion.apk') -Destination (Join-Path $fixture 'tools\companion\DX-Companion.apk') -Force
$compiler=Join-Path ([IO.Path]::GetDirectoryName($msbuild)) 'Roslyn\csc.exe'
$test=Join-Path $fixture 'NativeDexIntegrationTests.exe'
& $compiler /nologo /target:exe /platform:x64 ("/out:"+$test) ("/reference:"+(Join-Path $fixture 'DXManager.exe')) /reference:System.Runtime.Serialization.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $repo 'DexManager.MultiDeviceTests\NativeDexIntegrationTests.cs') (Join-Path $repo 'DexManager.MultiDeviceTests\NativeStartReadinessTests.cs') (Join-Path $repo 'DexManager\Services\NativeStartReadiness.cs') *> (Join-Path $work 'native-windows-test-build.log')
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $work 'native-windows-test-build.log');throw 'Native Windows test compilation failed.'}
# Normal-exit tests sweep the selected ADB executable path. A separate synthetic binary
# keeps that real cleanup path from mistaking the fixture controller for an ADB server.
Copy-Item -LiteralPath $test -Destination (Join-Path $fixture 'NativeFakeAdb.exe') -Force
Copy-Item -LiteralPath ($test+'.config') -Destination (Join-Path $fixture 'NativeFakeAdb.exe.config') -Force
& $test *> (Join-Path $work 'native-windows-tests.log')
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $work 'native-windows-tests.log') -Tail 30;throw 'Native Windows tests failed.'}
Get-Content -LiteralPath (Join-Path $work 'native-windows-tests.log') -Tail 1

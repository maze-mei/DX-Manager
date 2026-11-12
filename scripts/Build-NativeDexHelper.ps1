param(
 [string]$JavaHome=$env:JAVA_HOME,
 [string]$AndroidSdkRoot=$env:ANDROID_SDK_ROOT
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(!$JavaHome -or !$AndroidSdkRoot){throw 'Set JAVA_HOME to JDK 17 and ANDROID_SDK_ROOT to an SDK with platform 36 and build-tools 36.0.0.'}
$jdk=[IO.Path]::GetFullPath($JavaHome)
$sdk=[IO.Path]::GetFullPath($AndroidSdkRoot)
$androidJar=Join-Path $sdk 'platforms\android-36\android.jar'
$tools=Join-Path $sdk 'build-tools\36.0.0'
$build=Join-Path $repo '.build-tools\native-dex\build'
$classes=Join-Path $build 'classes'
$dex=Join-Path $build 'dex'
foreach($path in @($classes,$dex)){
 $full=[IO.Path]::GetFullPath($path)
 if(!$full.StartsWith($repo+'\.build-tools\native-dex\build\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected generated helper path.'}
 $ancestor=$full
 while($ancestor.Length -ge $repo.Length){
  if((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Reparse points are not allowed in helper outputs.'}
  $ancestor=[IO.Path]::GetDirectoryName($ancestor)
 }
 if(Test-Path -LiteralPath $full){Remove-Item -LiteralPath $full -Recurse -Force}
 [IO.Directory]::CreateDirectory($full)|Out-Null
}
if(!(Test-Path -LiteralPath $androidJar)){throw 'Android SDK platform 36 is missing.'}
$sourceFiles=@(Get-ChildItem -LiteralPath (Join-Path $repo 'NativeDex\vendor\server\src'),(Join-Path $repo 'NativeDex\src') -Filter '*.java' -Recurse -File | Sort-Object FullName | ForEach-Object {$_.FullName})
$argsFile=Join-Path $build 'javac.args'
$javacArgs=@('--release','8','-encoding','UTF-8','-classpath',('"'+$androidJar.Replace('\','/')+'"'),'-d',('"'+$classes.Replace('\','/')+'"'))
$javacArgs+=@($sourceFiles|ForEach-Object{'"'+$_.Replace('\','/')+'"'})
[IO.File]::WriteAllLines($argsFile,$javacArgs,[Text.UTF8Encoding]::new($false))
& (Join-Path $jdk 'bin\javac.exe') ('@'+$argsFile)
if($LASTEXITCODE -ne 0){throw 'Native helper Java compilation failed.'}
$inputJar=Join-Path $build 'classes.jar'
& (Join-Path $jdk 'bin\jar.exe') --create --date=2026-10-26T00:00:00Z --file $inputJar -C $classes .
if($LASTEXITCODE -ne 0){throw 'Native helper class archive failed.'}
$env:JAVA_HOME=$jdk
& (Join-Path $tools 'd8.bat') --min-api 33 --lib $androidJar --output $dex $inputJar
if($LASTEXITCODE -ne 0){throw 'Native helper DEX compilation failed.'}
$resources=Join-Path $build 'resources\META-INF'
[IO.Directory]::CreateDirectory($resources)|Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'NativeDex\vendor\LICENSE') -Destination (Join-Path $resources 'LICENSE-ScrcpyDeX.txt') -Force
Copy-Item -LiteralPath (Join-Path $repo 'DexManager\licenses\ScrcpyDex-NOTICE.txt') -Destination (Join-Path $resources 'NOTICE') -Force
$runtime=Join-Path $repo 'DexManager\tools\native-dex'
[IO.Directory]::CreateDirectory($runtime)|Out-Null
$jar=Join-Path $runtime 'dxm-native-dex.jar'
& (Join-Path $jdk 'bin\jar.exe') --create --date=2026-10-26T00:00:00Z --file $jar -C $dex . -C (Join-Path $build 'resources') .
if($LASTEXITCODE -ne 0){throw 'Native helper package failed.'}
$hash=(Get-FileHash -LiteralPath $jar -Algorithm SHA256).Hash
$generated=@"
namespace DexManager.Services {
    internal static class NativeDexHelperRelease {
        internal const string Version = "13";
        internal const string Sha256 = "$hash";
        internal const string UpstreamCommit = "eebbba903b73d54bbd13210096ea28fe31e51200";
    }
}
"@
[IO.File]::WriteAllText((Join-Path $repo 'DexManager\Services\NativeDexHelperRelease.cs'),$generated,[Text.UTF8Encoding]::new($false))
@{Version=13;Sha256=$hash;Upstream='ScrcpyDeX v1.1';UpstreamCommit='eebbba903b73d54bbd13210096ea28fe31e51200';MinAndroidApi=33}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $repo 'NativeDex\build-info.json') -Encoding utf8NoBOM
Write-Host ('Built Native DeX helper: '+$hash)

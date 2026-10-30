param(
    [string]$JavaHome = $env:DXM_JAVA_HOME,
    [string]$R8Jar = $env:DXM_R8_JAR,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
if (-not $JavaHome) { $JavaHome = $env:JAVA_HOME }
if (-not $JavaHome -or -not (Test-Path (Join-Path $JavaHome 'bin/javac.exe'))) {
    throw 'Specify -JavaHome with a JDK 17 directory (build dependency only).'
}
$Java = Join-Path $JavaHome 'bin/java.exe'
$Javac = Join-Path $JavaHome 'bin/javac.exe'
$Jar = Join-Path $JavaHome 'bin/jar.exe'
$JavaVersion = & $Javac -version 2>&1
if ($LASTEXITCODE -ne 0 -or "$JavaVersion" -notmatch '^javac 17\.') { throw 'JDK 17 required.' }
$BuildDirectory = Join-Path ([IO.Path]::GetTempPath()) ('dxm-loopback-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $BuildDirectory | Out-Null
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $RepoRoot 'tools/loopback' }
if (-not $R8Jar) { $R8Jar = Join-Path $BuildDirectory 'r8-8.7.18.jar' }
if (-not (Test-Path $R8Jar)) {
    Invoke-WebRequest -UseBasicParsing -Uri 'https://dl.google.com/dl/android/maven2/com/android/tools/r8/8.7.18/r8-8.7.18.jar' -OutFile $R8Jar
}
$ExpectedR8 = '58366f77067207c39a17d469de7b05701d2877212a9c55201bcb0af43e59e903'
if ((Get-FileHash -Algorithm SHA256 $R8Jar).Hash.ToLowerInvariant() -ne $ExpectedR8) { throw 'D8 checksum mismatch.' }
$Classes = Join-Path $BuildDirectory 'classes'
$Tests = Join-Path $BuildDirectory 'tests'
$Dex = Join-Path $BuildDirectory 'dex'
New-Item -ItemType Directory -Force -Path $Classes, $Tests, $Dex, $OutputDirectory | Out-Null
$Sources = @(Get-ChildItem (Join-Path $RepoRoot 'DXLoopback/src') -Recurse -Filter '*.java' | Sort-Object FullName | ForEach-Object FullName)
$TestSources = @(Get-ChildItem (Join-Path $RepoRoot 'DXLoopback/test') -Recurse -Filter '*.java' | Sort-Object FullName | ForEach-Object FullName)
& $Javac --release 8 -encoding UTF-8 -g:none -Werror -Xlint:all -d $Classes @Sources
if ($LASTEXITCODE -ne 0) { throw 'Helper compilation failed.' }
& $Javac --release 8 -encoding UTF-8 -g:none -Werror -Xlint:all -cp $Classes -d $Tests @TestSources
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& $Java -cp "$Classes;$Tests" com.dxmanager.loopback.ProtocolTests
if ($LASTEXITCODE -ne 0) { throw 'Helper tests failed.' }
$ClassJar = Join-Path $BuildDirectory 'classes.jar'
& $Jar --create --file $ClassJar --no-manifest --date=2026-01-01T00:00:00Z -C $Classes .
if ($LASTEXITCODE -ne 0) { throw 'Class packaging failed.' }
& $Java -cp $R8Jar com.android.tools.r8.D8 --release --no-desugaring --min-api 31 --lib $JavaHome --output $Dex $ClassJar
if ($LASTEXITCODE -ne 0) { throw 'DEX compilation failed.' }
$OutputJar = Join-Path $OutputDirectory 'dxm-loopback.jar'
$SourceRoot = Join-Path $RepoRoot 'DXLoopback'
& $Jar --create --file $OutputJar --no-manifest --date=2026-01-01T00:00:00Z -C $Dex classes.dex -C $SourceRoot LICENSE -C $SourceRoot NOTICE
if ($LASTEXITCODE -ne 0) { throw 'DEX packaging failed.' }
$Hash = (Get-FileHash -Algorithm SHA256 $OutputJar).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'dxm-loopback.jar.sha256'), "$Hash  dxm-loopback.jar`n", [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $SourceRoot 'LICENSE') -Destination (Join-Path $OutputDirectory 'LICENSE') -Force
Copy-Item -LiteralPath (Join-Path $SourceRoot 'NOTICE') -Destination (Join-Path $OutputDirectory 'NOTICE') -Force
Write-Host "Built $OutputJar (host tests only; Samsung device validation is separate)."
Write-Host "Build intermediates: $BuildDirectory"

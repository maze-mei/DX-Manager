function Get-DxmCompanionRoot {
    param([Parameter(Mandatory=$true)][string]$RepositoryRoot, [string]$OverrideRoot = '')
    if (!$OverrideRoot) { $OverrideRoot = $env:CompanionSharedRoot }
    $candidates = if ($OverrideRoot) { @($OverrideRoot) } else { @(
        (Join-Path $RepositoryRoot 'companion'),
        (Join-Path $RepositoryRoot '..\DX Companion Shared')
    ) }
    foreach ($candidate in $candidates) {
        $root = [IO.Path]::GetFullPath($candidate)
        if ((Test-Path -LiteralPath (Join-Path $root 'release.json') -PathType Leaf) -and
            (Test-Path -LiteralPath (Join-Path $root 'integration\CompanionRelease.cs') -PathType Leaf) -and
            (Test-Path -LiteralPath (Join-Path $root 'release\DX-Companion.apk') -PathType Leaf)) {
            return $root
        }
    }
    throw 'DX Companion is missing. Restore companion/ from the source checkout or set CompanionSharedRoot to the verified shared component.'
}

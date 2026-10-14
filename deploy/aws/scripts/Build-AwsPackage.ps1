#requires -Version 7.0
param(
    [Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}$')][string]$Version,
    [Parameter(Mandatory)][ValidateSet('linux/amd64', 'linux/arm64')][string]$Platform
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../../..").Path
function Invoke-Checked([string]$Program, [string[]]$Arguments) {
    & $Program @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Program failed (exit $LASTEXITCODE)" }
}
Push-Location $root
try {
    $destination = Join-Path $root "artifacts/aws/$Version"
    if (Test-Path -LiteralPath $destination) { throw 'Version already packaged; choose a new version.' }
    Invoke-Checked docker @('version')
    Invoke-Checked docker @('compose', 'version')
    Invoke-Checked docker @('buildx', 'version')
    Invoke-Checked dotnet @('build', 'Buscarro.sln', '-c', 'Release')
    Invoke-Checked dotnet @('test', 'tests/Buscarro.Tests/Buscarro.Tests.csproj', '-c', 'Release', '--no-build')
    # Pull pinned dependencies explicitly: fails early if architecture/tag is unavailable.
    Invoke-Checked docker @('pull', '--platform', $Platform, 'postgres:17.9-bookworm')
    Invoke-Checked docker @('pull', '--platform', $Platform, 'caddy:2.10.2')
    $images = @()
    foreach ($entry in @(@('api','Dockerfile'), @('worker','Dockerfile.catalog-sync'), @('seed','Dockerfile.seed'), @('migrations','Dockerfile.migrations'))) {
        $image = "buscarro-$($entry[0]):$Version"
        Invoke-Checked docker @('buildx', 'build', '--platform', $Platform, '--load', '-f', $entry[1], '-t', $image, '.')
        $images += $image
    }
    & "$PSScriptRoot/New-DeploymentPackage.ps1" -SourceRoot $root -Destination $destination
    Invoke-Checked docker (@('image','save','-o', (Join-Path $destination 'images.tar')) + $images)
    $head = & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source revision' }
    $status = & git status --porcelain
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify local changes' }
    $sourceNames = & git ls-files --cached --others --exclude-standard
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate source files' }
    # Hash source and Docker inputs only; no diff contents or local secrets.
    $sourceHashes = @($sourceNames | Sort-Object -Unique | Where-Object {
        ($_ -match '^src/.*\.(cs|csproj)$') -or ($_ -match '^src/.*/appsettings\.json$') -or ($_ -match '^Dockerfile(\..+)?$') -or ($_ -in @('Buscarro.sln','.dockerignore'))
    } | ForEach-Object {
        if (Test-Path -LiteralPath $_ -PathType Leaf) { [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } }
    })
    $packageHashes = @(Get-ChildItem -LiteralPath $destination -Recurse -File -Force | ForEach-Object {
        [ordered]@{ path = [System.IO.Path]::GetRelativePath($destination, $_.FullName); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    [ordered]@{ version=$Version; platform=$Platform; gitHead=$head; hasLocalChanges=($null -ne $status); images=$images; sourceHashes=$sourceHashes; packageHashes=$packageHashes } |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $destination 'manifest.json') -Encoding utf8NoBOM
    Write-Host "Package: $destination. No runtime secrets included. PostgreSQL tests skipped without a test database are not verified."
} finally { Pop-Location }

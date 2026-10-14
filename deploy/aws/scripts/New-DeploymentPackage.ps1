#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$SourceRoot,
    [Parameter(Mandatory)][string]$Destination
)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $Destination) { throw 'Destination already exists; use a new version directory.' }
New-Item -ItemType Directory -Path $Destination | Out-Null
$source = Join-Path $SourceRoot 'deploy/aws'
# Allowlist: never copy runtime .env, certificates, backups or arbitrary files.
foreach ($name in @('compose.yaml', 'Caddyfile')) {
    Copy-Item -LiteralPath (Join-Path $source $name) -Destination $Destination
}
Get-ChildItem -LiteralPath $source -File -Force | Where-Object { $_.Name -eq '.env.example' -or $_.Name -match '^[a-z]+\.env\.example$' } | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $Destination
}
New-Item -ItemType Directory -Path (Join-Path $Destination 'scripts') | Out-Null
Get-ChildItem -LiteralPath (Join-Path $source 'scripts') -File | Where-Object { $_.Extension -in @('.sh', '.service', '.timer') } | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $Destination 'scripts')
}

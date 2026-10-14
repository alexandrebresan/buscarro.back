#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$fixture = Join-Path ([System.IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path "$fixture/deploy/aws/scripts" -Force | Out-Null
Set-Content "$fixture/deploy/aws/api.env" 'TOP_SECRET'
Set-Content "$fixture/deploy/aws/.env" 'TOP_SECRET'
Set-Content "$fixture/deploy/aws/api.env.example" 'EXAMPLE'
Set-Content "$fixture/deploy/aws/compose.yaml" 'CONFIG'
Set-Content "$fixture/deploy/aws/Caddyfile" 'CONFIG'
Set-Content "$fixture/deploy/aws/scripts/backup.sh" 'SCRIPT'
& "$root/deploy/aws/scripts/New-DeploymentPackage.ps1" -SourceRoot $fixture -Destination "$fixture/package"
if (Test-Path "$fixture/package/api.env") { throw 'Secret copied' }
if (Test-Path "$fixture/package/.env") { throw 'Secret copied' }
if (-not (Test-Path "$fixture/package/api.env.example")) { throw 'Template missing' }
if (-not (Test-Path "$fixture/package/scripts/backup.sh")) { throw 'Operation script missing' }
Write-Host 'PASS: package copies templates and scripts, excludes runtime secrets'

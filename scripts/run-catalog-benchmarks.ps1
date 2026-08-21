param([ValidateSet(10000,100000,1000000)][int]$Items=100000)
$ErrorActionPreference='Stop'

function Invoke-DotNet {
    param([string[]]$DotnetArguments)

    & dotnet @DotnetArguments
    if($LASTEXITCODE -ne 0){throw "dotnet exited with code ${LASTEXITCODE}: $($DotnetArguments -join ' ')"}
}

if($env:RUN_CATALOG_BENCHMARKS -ne 'true'){throw 'RUN_CATALOG_BENCHMARKS=true is required.'}
if([string]::IsNullOrWhiteSpace($env:BENCHMARK_ADMIN_DATABASE_URL)){throw 'BENCHMARK_ADMIN_DATABASE_URL is required to prepare an isolated schema.'}
if($env:BENCHMARK_ADMIN_DATABASE_URL -match '(?i)(Search\s*Path|SearchPath)\s*='){throw 'BENCHMARK_ADMIN_DATABASE_URL must not define Search Path.'}

$repoRoot=Split-Path -Parent $PSScriptRoot
$solutionPath=Join-Path $repoRoot 'Buscarro.Benchmarks.sln'
$projectPath=Join-Path $repoRoot 'benchmarks\Buscarro.CatalogBenchmarks\Buscarro.CatalogBenchmarks.csproj'
$previousBenchmarkDatabaseUrl=$env:BENCHMARK_DATABASE_URL

try {
    $schema='buscarro_benchmark_'+[guid]::NewGuid().ToString('N')
    $env:BENCHMARK_DATABASE_URL=$env:BENCHMARK_ADMIN_DATABASE_URL.TrimEnd(';')+';Search Path='+$schema
    Invoke-DotNet -DotnetArguments @('build',$solutionPath,'--configuration','Release')
    Invoke-DotNet -DotnetArguments @('run','--project',$projectPath,'--configuration','Release','--no-build','--','prepare-schema')
    Invoke-DotNet -DotnetArguments @('run','--project',$projectPath,'--configuration','Release','--no-build','--','seed','--items',$Items)
    foreach($concurrency in 1,16,64){Invoke-DotNet -DotnetArguments @('run','--project',$projectPath,'--configuration','Release','--no-build','--','query','--items',$Items,'--iterations',200,'--warmup',20,'--concurrency',$concurrency)}
    Invoke-DotNet -DotnetArguments @('run','--project',$projectPath,'--configuration','Release','--no-build','--','explain','--items',$Items)
    Write-Host "Benchmark schema retained for inspection: $schema"
}
finally {
    if($null -eq $previousBenchmarkDatabaseUrl){Remove-Item Env:BENCHMARK_DATABASE_URL -ErrorAction SilentlyContinue}
    else{$env:BENCHMARK_DATABASE_URL=$previousBenchmarkDatabaseUrl}
}

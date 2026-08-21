# Desempenho do catalogo

Nenhum numero de desempenho foi medido nesta fase. Nao ha SLO, capacidade ou
ganho de indice inferido a partir de testes unitarios. Novos indices somente devem
ser considerados depois de `EXPLAIN (ANALYZE, BUFFERS)` com dados representativos.

Os comandos `seed`, `query` e `explain` exigem `BENCHMARK_DATABASE_URL`,
`RUN_CATALOG_BENCHMARKS=true`, nome do banco igual a `benchmark`, `test`,
`buscarro_benchmark` ou `buscarro_test`, um
`Search Path` explicito no formato `buscarro_benchmark_<32 hex minusculos>` e,
para host remoto, `ALLOW_REMOTE_BENCHMARK_DATABASE=true`. O harness nunca consulta
`DATABASE_URL`. `memory` e independente de banco, seed e variaveis de conexao.

O script `scripts/run-catalog-benchmarks.ps1` exige uma conexao administrativa
separada em `BENCHMARK_ADMIN_DATABASE_URL`, gera o nome seguro, cria o schema por
um comando `prepare-schema` explicito e reutiliza exatamente o mesmo Search Path
em seed/query/explain. A conexao administrativa nao pode trazer `Search Path`.
O schema e retido para inspecao: nao ha create/drop silencioso.

O harness e seus testes ficam isolados em `Buscarro.Benchmarks.sln`; eles nao
fazem parte de `Buscarro.sln`. Compile e teste essa solution explicitamente:

```powershell
dotnet build .\Buscarro.Benchmarks.sln
dotnet test .\Buscarro.Benchmarks.sln --no-build
```

```powershell
dotnet run --project benchmarks/Buscarro.CatalogBenchmarks --configuration Release -- seed --items 10000
dotnet run --project benchmarks/Buscarro.CatalogBenchmarks --configuration Release -- seed --items 100000
dotnet run --project benchmarks/Buscarro.CatalogBenchmarks --configuration Release -- seed --items 1000000
dotnet run --project benchmarks/Buscarro.CatalogBenchmarks --configuration Release -- query --items 1000000 --iterations 200 --warmup 20 --concurrency 1
dotnet run --project benchmarks/Buscarro.CatalogBenchmarks --configuration Release -- query --items 1000000 --iterations 200 --warmup 20 --concurrency 16
dotnet run --project benchmarks/Buscarro.CatalogBenchmarks --configuration Release -- query --items 1000000 --iterations 200 --warmup 20 --concurrency 64
dotnet run --project benchmarks/Buscarro.CatalogBenchmarks --configuration Release -- explain --items 1000000
dotnet run --project benchmarks/Buscarro.CatalogBenchmarks --configuration Release -- memory --items 2000000
```

Para `query`, a saida JSON inclui p50/p95/p99 e bytes alocados. `memory` usa
`HashSet<string>` com IDs deterministas, forca GC antes/depois e informa
`totalMemoryBefore/After`, `workingSetBefore/After`, `bytesPerItem`, tempo e
cardinalidade, sem abrir conexao com banco.

Nos comandos de banco, “Cold” significa somente um
novo processo/cache L1; o harness nao limpa shared buffers do PostgreSQL nem o
page cache do sistema operacional. O comando `memory` aceita exatamente 2.000.000
itens, e sempre explicito e nao faz parte dos testes normais.

As integracoes usam exclusivamente `TEST_DATABASE_URL` quando
`RUN_POSTGRES_INTEGRATION_TESTS=true`. O nome do banco deve conter `test`; host
remoto exige `ALLOW_REMOTE_TEST_DATABASE=true`. Cada execucao cria um schema
`buscarro_test_<guid>` e somente esse prefixo validado pode ser removido com
`DROP SCHEMA ... CASCADE`.

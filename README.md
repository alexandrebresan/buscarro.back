# buscarro.back

Back-end em .NET para uma Web API de consulta e normalizacao de veiculos vindos de provedores externos.

O projeto segue Clean Architecture, CQRS e MediatR:

- `src/Buscarro.Api`: controllers, configuracoes e entrada HTTP.
- `src/Buscarro.Application`: commands, queries, handlers, DTOs e interfaces de aplicacao.
- `src/Buscarro.Domain`: entidades e interfaces do dominio.
- `src/Buscarro.Infrastructure`: integracoes externas, persistencia, repositories, contracts e Unit of Work.
- `src/Buscarro.CarzinSeedSync`: carga manual do catalogo Carzin no banco.
- `tests/Buscarro.Tests`: testes unitarios, de integracao e de seguranca de configuracao.

## Requisitos

- [Git](https://git-scm.com/downloads).
- [.NET SDK 9.0](https://dotnet.microsoft.com/download).
- PostgreSQL local ou banco PostgreSQL no Render.
- Ferramenta do Entity Framework Core:

```powershell
dotnet tool install --global dotnet-ef
```

## Configuracao do banco

O projeto usa PostgreSQL via Npgsql.

Para desenvolvimento local, configure `src/Buscarro.Api/appsettings.Development.json` ou variaveis de ambiente:

```json
"ConnectionStrings": {
  "BuscarroDb": "Host=localhost;Port=5432;Database=BuscarroDb;Username=postgres;Password=postgres"
}
```

No Render, configure a API com `DATABASE_URL` usando a Internal Database URL do PostgreSQL.

## Restaurar, compilar e testar

```powershell
dotnet restore
dotnet build .\Buscarro.sln --no-restore
dotnet test .\tests\Buscarro.Tests\Buscarro.Tests.csproj --no-restore
```

### Gate opcional de integracao PostgreSQL

Os testes padrao usam EF Core InMemory para comportamento e inspecionam o modelo/DDL gerado. Eles nao provam locks `FOR UPDATE`, advisory locks, concorrencia, `jsonb`, `tsvector` nem constraints executadas pelo PostgreSQL.

O teste com trait `PostgreSqlIntegration` aplica as migrations e inspeciona constraints e o indice GIN em um banco real. Use exclusivamente um banco de teste descartavel:

```powershell
$env:TEST_DATABASE_URL="postgresql://USER:PASSWORD@HOST/DATABASE_TESTE_DESCARTAVEL"
dotnet test .\tests\Buscarro.Tests\Buscarro.Tests.csproj --filter "Category=PostgreSqlIntegration"
```

Se `TEST_DATABASE_URL` estiver ausente, o teste aparece explicitamente como ignorado com a mensagem `integracao PostgreSQL real NAO EXECUTADA`; essa ausencia nao deve ser reportada como cobertura de integracao aprovada.

### Contrato transacional e retries

`IUnitOfWork.ExecuteInTransactionAsync` pode repetir uma tentativa que falhe antes do commit. Por isso, o delegate deve reler entidades e agregados do `DbContext` em cada invocacao, sem reutilizar referencias capturadas de uma tentativa anterior.

Uma excecao de `CommitAsync` tem resultado desconhecido: a infraestrutura limpa o tracker e lanca `TransactionCommitOutcomeUnknownException`, sem retry automatico. O chamador deve reconciliar o estado persistido por uma chave de idempotencia (por exemplo, a geracao/publicacao alvo) antes de decidir qualquer nova tentativa. Essa politica nao afirma que o commit falhou nem que foi concluido.

## Criar ou atualizar o banco

Local:

```powershell
dotnet ef database update --project .\src\Buscarro.Infrastructure\Buscarro.Infrastructure.csproj --startup-project .\src\Buscarro.Api\Buscarro.Api.csproj
```

Render, usando a External Database URL do PostgreSQL:

```powershell
$env:DATABASE_URL="postgresql://USER:PASSWORD@HOST/DATABASE"
dotnet ef database update --project .\src\Buscarro.Infrastructure\Buscarro.Infrastructure.csproj --startup-project .\src\Buscarro.Api\Buscarro.Api.csproj
```

## Rodar SeedSync Carzin

Configure as variaveis e execute o projeto de seed:

```powershell
$env:DATABASE_URL="postgresql://USER:PASSWORD@HOST/DATABASE"
$env:Carzin__BaseUrl="https://carzin.com.br"
$env:Carzin__Token="TOKEN_DA_CARZIN"
$env:CarzinCatalog__ManufacturersPath="PATH_DE_MARCAS"
$env:CarzinCatalog__ModelsPath="PATH_DE_MODELOS"
$env:CarzinCatalog__VersionsPath="PATH_DE_VERSOES"

dotnet run --project .\src\Buscarro.CarzinSeedSync\Buscarro.CarzinSeedSync.csproj -c Release
```

## Rodar a API localmente

```powershell
dotnet run --project .\src\Buscarro.Api\Buscarro.Api.csproj
```

## Rodar o sincronizador versionado de catalogo

O catalogo online e alimentado por um Worker separado. A API nao agenda nem hospeda sincronizacoes. Configure os segredos somente no ambiente e execute:

```powershell
$env:DATABASE_URL="postgresql://USER:PASSWORD@HOST/DATABASE"
$env:Carzin__BaseUrl="https://carzin.com.br"
$env:Carzin__Token="TOKEN_DA_CARZIN"
dotnet run --project .\src\Buscarro.CatalogSync.Worker\Buscarro.CatalogSync.Worker.csproj -c Release
```

Por padrao, a primeira sincronizacao comeca apos 15 segundos, repete a cada 6 horas e a limpeza roda a cada 24 horas. Cada provider e processado sequencialmente por comandos MediatR. Um advisory lock de sessao impede duas sincronizacoes simultaneas do mesmo provider.

O Worker le apenas o endpoint comprovado `POST /api/classified-search` da Carzin, com `token`, `type=1`, `page` e `results`. Ele grava lotes via PostgreSQL binary COPY, valida a geracao completa e so entao publica a nova versao. Falhas anteriores ao estado `Ready` ficam registradas como `Failed`; falhas de publicacao preservam a geracao `Ready` para reconciliacao.

Antes de sincronizar anuncios, execute o `Buscarro.CarzinSeedSync` para preencher a correlacao codigo/nome de fabricantes, modelos e versoes. O Worker carrega essas tabelas e o catalogo persistido de cor, cambio e combustivel uma unica vez por sincronizacao, converte os codigos em memoria e grava em `vehicle_catalog` somente os nomes. A API nunca chama a Carzin: buscas e favoritos usam exclusivamente a publicacao ativa do PostgreSQL.

Na resposta de busca, `publicationId` identifica a versao global e coerente do catalogo. `GenerationId` permanece um conceito interno do Worker para identificar a carga de um provider.

Para gerar a imagem dedicada:

```powershell
docker build -f Dockerfile.catalog-sync -t buscarro-catalog-sync .
```

URLs padrao:

- HTTPS: `https://localhost:7175`
- HTTP: `http://localhost:5294`
- Swagger: `https://localhost:7175/swagger` ou `http://localhost:5294/swagger`
- Health check: `/health`

## Deploy no Render

1. Crie um PostgreSQL no Render na mesma regiao da API.
2. Guarde a Internal Database URL para a API.
3. Guarde a External Database URL para migrations e SeedSync manuais.
4. Crie um Web Service conectado ao repositorio.
5. Selecione deploy via Docker.
6. Configure as variaveis:

```text
DATABASE_URL=<Internal Database URL>
ASPNETCORE_ENVIRONMENT=Production
Jwt__Issuer=Buscarro
Jwt__Audience=Buscarro.Api
Jwt__Secret=<segredo com pelo menos 32 bytes>
```

7. Aplique migrations manualmente com a External Database URL.
8. Rode o SeedSync manualmente com a External Database URL.
9. Faca deploy da API no Render e valide `/health`.

No plano free/manual, migrations e SeedSync ficam fora do pre-deploy do Render.

## Observacoes

- Controllers devem permanecer finos e chamar os fluxos via MediatR.
- DTOs internos ficam em `src/Buscarro.Application/DTOs`.
- Contracts de provedores externos ficam em `src/Buscarro.Infrastructure/Contracts`.
- Integracoes externas devem permanecer isoladas em `src/Buscarro.Infrastructure/Providers`.
- Nao exponha DTOs externos diretamente pela API.

## Busca por IA e segredos

A rota `POST /api/AiSearch` usa Structured Outputs e converte o prompt em filtros canonicos antes de gerar a URL de busca. Configure a integracao somente por variaveis de ambiente ou um secret store:

```powershell
$env:OpenAI__ApiKey="<segredo>"
$env:OpenAI__Model="gpt-4o-mini"
$env:OpenAI__BaseUrl="https://api.openai.com"
```

O `ApiKey` dos arquivos `appsettings*.json` versionados deve permanecer vazio. Se uma chave real tiver sido versionada anteriormente, ela deve ser rotacionada imediatamente no provedor.

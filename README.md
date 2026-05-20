# buscarro.back

Back-end em .NET para uma Web API de consulta e normalizacao de veiculos vindos de provedores externos.

O projeto segue Clean Architecture, CQRS e MediatR:

- `Buscarro.Api`: controllers, configuracoes e entrada HTTP.
- `Buscarro.Application`: commands, queries, handlers, DTOs e interfaces de aplicacao.
- `Buscarro.Domain`: entidades e interfaces do dominio.
- `Buscarro.Infrastructure`: integracoes externas, persistencia, repositories, contracts e Unit of Work.
- `Buscarro.CarzinSeedSync`: carga manual do catalogo Carzin no banco.

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

Para desenvolvimento local, configure `Buscarro.Api/appsettings.Development.json` ou variaveis de ambiente:

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
dotnet test .\Buscarro.Tests\Buscarro.Tests.csproj --no-restore
```

## Criar ou atualizar o banco

Local:

```powershell
dotnet ef database update --project .\Buscarro.Infrastructure\Buscarro.Infrastructure.csproj --startup-project .\Buscarro.Api\Buscarro.Api.csproj
```

Render, usando a External Database URL do PostgreSQL:

```powershell
$env:DATABASE_URL="postgresql://USER:PASSWORD@HOST/DATABASE"
dotnet ef database update --project .\Buscarro.Infrastructure\Buscarro.Infrastructure.csproj --startup-project .\Buscarro.Api\Buscarro.Api.csproj
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

dotnet run --project .\Buscarro.CarzinSeedSync\Buscarro.CarzinSeedSync.csproj -c Release
```

## Rodar a API localmente

```powershell
dotnet run --project .\Buscarro.Api\Buscarro.Api.csproj
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
Carzin__BaseUrl=https://carzin.com.br
Carzin__Token=<token real>
VehicleCache__DurationMinutes=5
```

7. Aplique migrations manualmente com a External Database URL.
8. Rode o SeedSync manualmente com a External Database URL.
9. Faca deploy da API no Render e valide `/health`.

No plano free/manual, migrations e SeedSync ficam fora do pre-deploy do Render.

## Observacoes

- Controllers devem permanecer finos e chamar os fluxos via MediatR.
- DTOs internos ficam em `Buscarro.Application/DTOs`.
- Contracts de provedores externos ficam em `Buscarro.Infrastructure/Contracts`.
- Integracoes externas devem permanecer isoladas em `Buscarro.Infrastructure/Providers`.
- Nao exponha DTOs externos diretamente pela API.

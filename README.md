# buscarro.back

Back-end em .NET para uma Web API de consulta e normalizacao de veiculos vindos de provedores externos.

O projeto segue Clean Architecture, CQRS e MediatR:

Autenticacao com JWT, refresh token e Google. Configure `Jwt__Issuer`, `Jwt__Audience`, `Jwt__Secret` (minimo 32 bytes), `Google__ClientId` e as origens exatas em `Auth__AllowedOrigins__0` e `Cors__AllowedOrigins__0`. Fora de Development, a API exige certificado PFX com chave privada para Data Protection e a senha correspondente. Use ambiente ou secret store; veja os nomes de configuracao no [exemplo da API](deploy/aws/api.env.example), sem publicar credenciais.

- `src/Buscarro.Api`: controllers, configuracoes e entrada HTTP.
- `src/Buscarro.Application`: commands, queries, handlers, DTOs e interfaces de aplicacao.
- `src/Buscarro.Domain`: entidades e interfaces do dominio.
- `src/Buscarro.Infrastructure`: integracoes externas, persistencia, repositories, contracts e Unit of Work.
- `src/Buscarro.CarzinSeedSync`: carga manual do catalogo Carzin no banco.
- `tests/Buscarro.Tests`: testes unitarios, de integracao e de seguranca de configuracao.

## Requisitos

- [Git](https://git-scm.com/downloads).
- [.NET SDK 9.0](https://dotnet.microsoft.com/download).
- PostgreSQL local ou container privado na EC2 AWS da faculdade.
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

Na AWS, use `ConnectionStrings__BuscarroDb` Npgsql com `Host=postgres`; não configure `DATABASE_URL`. Use os arquivos de ambiente de exemplo em [deploy/aws](deploy/aws).

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

Na EC2, execute `docker compose --profile maintenance run --rm migrate` no diretorio de implantacao, com PostgreSQL healthy e `migrate.env` configurado. Faca backup antes de atualizar um banco existente.

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
$env:AutoDev__BaseUrl="https://api.auto.dev/"
$env:AutoDev__ApiKey="CHAVE_DA_AUTODEV"
dotnet run --project .\src\Buscarro.CatalogSync.Worker\Buscarro.CatalogSync.Worker.csproj -c Release
```

Por padrao, a primeira sincronizacao comeca apos 15 segundos, repete Carzin a cada 24 horas e AutoDev a cada 48 horas e a limpeza roda a cada 24 horas. Cada provider e processado sequencialmente por comandos MediatR. Um advisory lock de sessao impede duas sincronizacoes simultaneas do mesmo provider.

O Worker consulta `POST /api/classified-search` da Carzin, com `token`, `type=1`, `page` e `results`, e `GET /listings` da AutoDev, com paginacao e autenticacao por API key. Para AutoDev, converte precos de USD para BRL usando a cotacao de venda PTAX mais recente valida do Banco Central e quilometragem de milhas para quilometros. Grava lotes via PostgreSQL binary COPY, valida a geracao completa e so entao publica a nova versao. Falhas anteriores ao estado `Ready` ficam registradas como `Failed`; falhas de publicacao preservam a geracao `Ready` para reconciliacao.

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
- Health checks: `/health/live` e `/health/ready`

## Deploy manual na AWS da faculdade

O [Compose de producao](deploy/aws/compose.yaml) executa API, Worker, PostgreSQL e proxy HTTPS Caddy em uma EC2 Ubuntu; migrations e SeedSync ficam no perfil `maintenance`. O frontend continua na Vercel. Nao ha provisionamento automatico nem deploy acionado por push no GitHub.

Gere as imagens no Windows com PowerShell 7 e Docker Desktop, usando [Build-AwsPackage.ps1](deploy/aws/scripts/Build-AwsPackage.ps1) com `-Version <versao>` e `-Platform linux/arm64` ou `linux/amd64`, conforme a EC2. Transfira o pacote `artifacts/aws/<versao>` por SCP, importe `images.tar` com `docker load` e configure os arquivos `.env` e `<servico>.env` a partir dos exemplos. Mantenha credenciais e certificado fora do Git.

Para banco novo, inicie `postgres`, execute `migrate`, execute `seed` com os caminhos reais da Carzin e inicie `worker`. Confirme a publicacao de ambos os providers antes de iniciar `api proxy`. Valide HTTPS, `/health/live` e `/health/ready`; so depois altere o destino `/api` na Vercel, preservando caminhos, cookies e headers de autenticacao.

Antes de atualizar, faca e teste um backup usando [backup.sh](deploy/aws/scripts/backup.sh); guarde uma copia fora da EC2, junto do PFX e dos segredos de recuperacao. Pare API e Worker durante migrations e preserve volumes e a versao anterior das imagens. Rollback da aplicacao nao desfaz migrations. Nunca execute `docker compose down -v` em producao. Monitore memoria e disco durante as cargas e alinhe qualquer aumento de capacidade com o professor.

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

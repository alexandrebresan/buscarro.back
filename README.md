# buscarro.back

Back-end em .NET para uma Web API de consulta e normalização de veiculos vindos de provedores externos.

O projeto segue uma organização baseada em Clean Architecture, CQRS e MediatR:

- `Buscarro.Api`: controllers, configurações e entrada HTTP.
- `Buscarro.Application`: commands, queries, handlers, DTOs e interfaces de aplicação.
- `Buscarro.Domain`: entidades, enums e interfaces do domínio.
- `Buscarro.Infrastructure`: integrações externas, persistência, repositories, contracts e Unit of Work.

## Requisitos

Instale os itens abaixo antes de rodar o projeto:

- [Git](https://git-scm.com/downloads).
- [.NET SDK 9.0](https://dotnet.microsoft.com/download).
- [SQL Server Management Studio (SSMS)](https://learn.microsoft.com/sql/ssms/download-sql-server-management-studio-ssms) para acessar e gerenciar o banco.
- Ferramenta do Entity Framework Core, caso ainda não esteja instalada:

```powershell
dotnet tool install --global dotnet-ef
```

Opcionalmente, use Visual Studio 2022, Rider ou VS Code com a extensão C# Dev Kit.

## Como clonar

```powershell
git clone https://github.com/alexandrebresan/buscarro.back.git
cd buscarro.back
```

## Configuracao do banco

O projeto usa SQL Server como banco de dados. O SSMS é a ferramenta para acessar esse banco, mas ele não é o banco em si.

Configure a connection string em `Buscarro.Api/appsettings.Development.json`:

```json
"ConnectionStrings": {
  "BuscarroDb": "Server=localhost;Database=BuscarroDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

Se estiver usando SQL Server Express, normalmente o servidor fica assim:

```json
"ConnectionStrings": {
  "BuscarroDb": "Server=localhost\\SQLEXPRESS;Database=BuscarroDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

Abra o SSMS, conecte na sua instância do SQL Server e confirme qual nome de servidor deve ser usado na connection string.

## Restaurar dependências

```powershell
dotnet restore
```

## Criar ou atualizar o banco

Depois de configurar a connection string, aplique as migrations:

```powershell
dotnet ef database update --project .\Buscarro.Infrastructure\Buscarro.Infrastructure.csproj --startup-project .\Buscarro.Api\Buscarro.Api.csproj
```

## Compilar

```powershell
dotnet build
```

## Rodar a API

Para iniciar a API em ambiente de desenvolvimento:

```powershell
dotnet run --project .\Buscarro.Api\Buscarro.Api.csproj
```

URLs padrão:

- HTTPS: `https://localhost:7175`
- HTTP: `http://localhost:5294`
- Swagger: `https://localhost:7175/swagger` ou `http://localhost:5294/swagger`

Se o HTTPS local ainda não estiver confiável na sua máquina, rode:

```powershell
dotnet dev-certs https --trust
```

## Fluxo recomendado para desenvolvimento

```powershell
dotnet restore
dotnet build
dotnet ef database update --project .\Buscarro.Infrastructure\Buscarro.Infrastructure.csproj --startup-project .\Buscarro.Api\Buscarro.Api.csproj
dotnet run --project .\Buscarro.Api\Buscarro.Api.csproj
```

## Observações

- Controllers devem permanecer finos e chamar os fluxos via MediatR.
- DTOs internos ficam em `Buscarro.Application/DTOs`.
- Contracts de provedores externos ficam em `Buscarro.Infrastructure/Contracts`.
- Integrações externas devem permanecer isoladas em `Buscarro.Infrastructure/Providers`.
- Não exponha DTOs externos diretamente pela API.

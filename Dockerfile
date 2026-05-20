FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Buscarro.sln ./
COPY Buscarro.Api/Buscarro.Api.csproj Buscarro.Api/
COPY Buscarro.Application/Buscarro.Application.csproj Buscarro.Application/
COPY Buscarro.Domain/Buscarro.Domain.csproj Buscarro.Domain/
COPY Buscarro.Infrastructure/Buscarro.Infrastructure.csproj Buscarro.Infrastructure/
COPY Buscarro.CarzinSeedSync/Buscarro.CarzinSeedSync.csproj Buscarro.CarzinSeedSync/
COPY Buscarro.Tests/Buscarro.Tests.csproj Buscarro.Tests/
RUN dotnet restore Buscarro.Api/Buscarro.Api.csproj
COPY . .
RUN dotnet publish Buscarro.Api/Buscarro.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Buscarro.Api.dll"]

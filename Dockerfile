FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Buscarro.sln ./
COPY src/Buscarro.Api/Buscarro.Api.csproj src/Buscarro.Api/
COPY src/Buscarro.Application/Buscarro.Application.csproj src/Buscarro.Application/
COPY src/Buscarro.Domain/Buscarro.Domain.csproj src/Buscarro.Domain/
COPY src/Buscarro.Infrastructure/Buscarro.Infrastructure.csproj src/Buscarro.Infrastructure/
COPY src/Buscarro.CarzinSeedSync/Buscarro.CarzinSeedSync.csproj src/Buscarro.CarzinSeedSync/
COPY tests/Buscarro.Tests/Buscarro.Tests.csproj tests/Buscarro.Tests/
RUN dotnet restore src/Buscarro.Api/Buscarro.Api.csproj
COPY . .
RUN dotnet publish src/Buscarro.Api/Buscarro.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Buscarro.Api.dll"]

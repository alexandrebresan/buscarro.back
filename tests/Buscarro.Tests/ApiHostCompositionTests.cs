using Buscarro.Api.Configurations;
using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Handlers;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using Buscarro.Infrastructure.Providers;
using Buscarro.Infrastructure.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Buscarro.Tests;

public sealed class ApiHostCompositionTests
{
    [Fact]
    public void Program_composition_uses_only_postgres_vehicle_reads_and_resolves_handlers()
    {
        var builder =
            WebApplication.CreateBuilder(
                new WebApplicationOptions
                {
                    EnvironmentName = "Development",
                    ApplicationName =
                        typeof(ApiHostComposition)
                            .Assembly
                            .GetName()
                            .Name
                });

        builder.Configuration
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:BuscarroDb"] =
                        "Host=localhost;Database=BuscarroTestDb;Username=postgres;Password=postgres",

                    ["Jwt:Issuer"] = "test",
                    ["Jwt:Audience"] = "test",

                    ["Jwt:Secret"] =
                        "a-test-secret-with-at-least-32-bytes",

                    ["Auth:AllowedOrigins:0"] =
                        "https://buscarro.test",

                    ["OpenAI:BaseUrl"] =
                        "https://api.openai.test/",

                    ["OpenAI:Model"] =
                        "test-model"
                });

        ApiHostComposition.ConfigureServices(
            builder);

        using var app = builder.Build();
        using var scope =
            app.Services.CreateScope();

        Assert.IsType<
            PostgresVehicleCatalogReadService>(
            scope.ServiceProvider
                .GetRequiredService<
                    IVehicleSearchReadService>());

        Assert.IsType<
            PostgresGeneralVehicleCatalogReadService>(
            scope.ServiceProvider
                .GetRequiredService<
                    IGeneralVehicleCatalogReadService>());

        Assert.NotNull(
            scope.ServiceProvider
                .GetRequiredService<
                    IRequestHandler<
                        ListManufacturersQuery,
                        IReadOnlyCollection<string>>>());

        Assert.NotNull(
            scope.ServiceProvider
                .GetRequiredService<
                    IRequestHandler<
                        ListModelsQuery,
                        IReadOnlyCollection<string>>>());

        Assert.NotNull(
            scope.ServiceProvider
                .GetRequiredService<
                    IRequestHandler<
                        ListVersionsQuery,
                        IReadOnlyCollection<string>>>());

        Assert.NotNull(
            scope.ServiceProvider
                .GetRequiredService<
                    IRequestHandler<
                        ListFuelsQuery,
                        IReadOnlyCollection<string>>>());

        Assert.NotNull(
            scope.ServiceProvider
                .GetRequiredService<
                    IRequestHandler<
                        ListTransmissionsQuery,
                        IReadOnlyCollection<string>>>());

        Assert.NotNull(
            scope.ServiceProvider
                .GetRequiredService<
                    IRequestHandler<
                        ListColorsQuery,
                        IReadOnlyCollection<string>>>());

        Assert.NotNull(
            scope.ServiceProvider
                .GetRequiredService<
                    IRequestHandler<
                        AddFavoriteVehicleCommand,
                        FavoriteVehicleResponse>>());

        var forbiddenNames =
            new[]
            {
                "Carzin",
                "VehicleSearchReadRouter",
                "LegacyVehicleSearchReadAdapter",
                "CatalogShadowReadWorker",
                "Fallback"
            };

        Assert.DoesNotContain(
            builder.Services,
            descriptor =>
                forbiddenNames.Any(
                    name =>
                        (
                            descriptor.ServiceType
                                .FullName?
                                .Contains(
                                    name,
                                    StringComparison.Ordinal)
                            ?? false
                        )
                        ||
                        (
                            descriptor.ImplementationType
                                ?.FullName?
                                .Contains(
                                    name,
                                    StringComparison.Ordinal)
                            ?? false
                        )));

        Assert.DoesNotContain(
            builder.Services,
            descriptor =>
                descriptor.ServiceType
                ==
                typeof(
                    IRequestHandler<
                        GetProviderSynchronizationDueQuery,
                        bool>));

        Assert.DoesNotContain(
            builder.Services,
            descriptor =>
                descriptor.ServiceType
                    == typeof(
                        IVehicleCatalogSource)
                ||
                descriptor.ServiceType
                    == typeof(
                        IVehicleCatalogItemNormalizer)
                ||
                descriptor.ImplementationType
                    == typeof(
                        AutoDevCatalogSource));
    }
}
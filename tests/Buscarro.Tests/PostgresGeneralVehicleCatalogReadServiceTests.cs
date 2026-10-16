using Buscarro.Application.Exceptions;
using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.Providers;
using Buscarro.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Buscarro.Tests;

public sealed class PostgresGeneralVehicleCatalogReadServiceTests
{
    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task General_names_use_all_active_provider_generations_and_normalized_optional_filters()
    {
        await using var fixture = new PostgresTestDatabaseFixture();
        await fixture.InitializeAsync();
        var contextOptions = new DbContextOptionsBuilder<BuscarroDbContext>().UseNpgsql(fixture.ConnectionString).Options;
        await using var context = new BuscarroDbContext(contextOptions);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var carzin = await AddGenerationAsync(context, "Carzin", current: true, pointed: true);
        var autoDev = await AddGenerationAsync(context, "AutoDev", current: true, pointed: true);
        var previous = await AddGenerationAsync(context, "Carzin", current: false, pointed: false);
        var unpublished = await AddGenerationAsync(context, "Unpublished", current: true, pointed: false);
        var ready = await AddGenerationAsync(context, "Ready", current: false, pointed: true);
        await using var dataSource = NpgsqlDataSource.Create(fixture.ConnectionString);
        await new Buscarro.Infrastructure.Services.PostgresVehicleCatalogBatchWriter(dataSource).WriteBatchAsync([
            Item(carzin, "Carzin", "c1", " Jeep ", " Wrangler ", "Rubicon"),
            Item(carzin, "Carzin", "c2", "jeep", "wrangler", "RUBICON"),
            Item(carzin, "Carzin", "c3", "Fiat", "Uno", null),
            Item(carzin, "Carzin", "c4", null, null, null),
            Item(carzin, "Carzin", "c5", "  ", "  ", "  "),
            Item(autoDev, "AutoDev", "a1", "Jeep", "Wrangler", "Sahara"),
            Item(autoDev, "AutoDev", "a2", "Toyota", "Corolla", "Hybrid"),
            Item(autoDev, "AutoDev", "a3", "Other", "Wrangler", "Collision"),
            Item(previous, "Carzin", "p1", "Inactive", "Inactive", "Inactive"),
            Item(unpublished, "Unpublished", "u1", "Unpublished", "Unpublished", "Unpublished"),
            Item(ready, "Ready", "r1", "Ready", "Ready", "Ready")
        ], TestContext.Current.CancellationToken);
        var service = new PostgresGeneralVehicleCatalogReadService(dataSource, Options.Create(new CatalogQueryOptions()));
        var token = TestContext.Current.CancellationToken;

        Assert.Equal(["Fiat", "Jeep", "Other", "Toyota"], await service.ListManufacturersAsync(token));
        Assert.Equal(["Corolla", "Uno", "Wrangler"], await service.ListModelsAsync(null, token));
        Assert.Equal(["Corolla", "Uno", "Wrangler"], await service.ListModelsAsync("  ", token));
        Assert.Equal(["Wrangler"], await service.ListModelsAsync(" JÉÉP ", token));
        Assert.Equal(["Collision", "RUBICON", "Sahara"], await service.ListVersionsAsync(" WRÁNGLER ", null, token));
        Assert.Equal(["RUBICON", "Sahara"], await service.ListVersionsAsync("Wrangler", "Jeep", token));
        Assert.Equal(["Hybrid"], await service.ListVersionsAsync("Corolla", "Toyota", token));
        Assert.Empty(await service.ListVersionsAsync("Uno", "Fiat", token));
        Assert.Empty(await service.ListModelsAsync("Jeep' OR TRUE --", token));
        Assert.Empty(await service.ListVersionsAsync("Wrangler' OR TRUE --", null, token));
    }

    [Fact]
    public async Task Connection_failure_is_wrapped_without_exposing_connection_details()
    {
        await using var source = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=buscarro_test;Username=test;Password=private;Timeout=1;Pooling=false");
        var service = new PostgresGeneralVehicleCatalogReadService(source, Options.Create(new CatalogQueryOptions()));
        var error = await Assert.ThrowsAsync<CatalogUnavailableException>(() => service.ListManufacturersAsync(CancellationToken.None));
        Assert.Equal(CatalogUnavailableException.PublicMessage, error.Message);
        Assert.NotNull(error.InnerException);
    }

    [Fact]
    public async Task Cancellation_is_propagated()
    {
        await using var source = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=buscarro_test;Username=test;Password=private");
        var service = new PostgresGeneralVehicleCatalogReadService(source, Options.Create(new CatalogQueryOptions()));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ListModelsAsync(null, cancellation.Token));
    }

    private static async Task<int> AddGenerationAsync(BuscarroDbContext context, string provider, bool current, bool pointed)
    {
        var now = DateTimeOffset.UtcNow;
        var generation = new CatalogGeneration(provider, now);
        generation.MarkReady(1, 10, 10, 0, 0, new string('a', 64), null, now);
        if (current) generation.MarkCurrent(now);
        context.Add(generation);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        if (pointed)
        {
            context.Add(new ProviderCatalogCurrent(provider, generation.Id));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        return generation.Id;
    }

    private static VehicleCatalogItem Item(int generation, string provider, string id, string? brand, string? model, string? version)
    {
        static string? Normalize(string? value) => value is null ? null : Buscarro.Application.Services.VehicleCatalogSearchCriteriaNormalizer.NormalizeText(value);
        return new VehicleCatalogItem(generation, provider, id, id, id)
        {
            Brand = brand, BrandNormalized = Normalize(brand),
            Model = model, ModelNormalized = Normalize(model),
            Version = version, VersionNormalized = Normalize(version)
        };
    }
}

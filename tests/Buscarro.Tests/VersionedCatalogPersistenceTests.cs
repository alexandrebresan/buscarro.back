using Buscarro.Domain.Entities;
using Buscarro.Domain.Enums;
using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Buscarro.Tests;

public sealed class VersionedCatalogPersistenceTests
{
    [Fact]
    public async Task Repositories_persist_generation_items_and_read_latest_publication()
    {
        await using var context = CreateContext();
        var generationRepository = new CatalogGenerationRepository(context);
        var itemRepository = new VehicleCatalogWriteRepository(context);
        var publicationRepository = new CatalogPublicationRepository(context);
        var generation = ReadyGeneration("carzin");

        await generationRepository.AddAsync(generation, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await itemRepository.AddRangeAsync(
            [new VehicleCatalogItem(generation.Id, "carzin", "vehicle-1", "Honda Civic", "honda civic")],
            TestContext.Current.CancellationToken);
        context.CatalogPublications.Add(new CatalogPublication(DateTimeOffset.UtcNow, 1, 1));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var loaded = await generationRepository.GetByIdAsync(generation.Id, TestContext.Current.CancellationToken);
        var latest = await publicationRepository.GetLatestAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CatalogGenerationStatus.Ready, loaded!.Status);
        Assert.Equal(1, latest!.TotalCatalogCount);
        Assert.Single(context.VehicleCatalog);
    }

    [Fact]
    public async Task Retention_deletes_only_unreferenced_ready_or_expired_failed_generations()
    {
        await using var context = CreateContext();
        var current = ReadyGeneration("carzin");
        var loading = new CatalogGeneration("olx", DateTimeOffset.UtcNow.AddDays(-10));
        var oldOrphanReady = ReadyGeneration("webmotors", DateTimeOffset.UtcNow.AddDays(-10));
        var recentOrphanReady = ReadyGeneration("seminovos", DateTimeOffset.UtcNow.AddHours(-1));
        var oldFailed = new CatalogGeneration("icarros", DateTimeOffset.UtcNow.AddDays(-10));
        oldFailed.MarkFailed("provider unavailable", DateTimeOffset.UtcNow.AddDays(-9));
        var recentFailed = new CatalogGeneration("mobiauto", DateTimeOffset.UtcNow);
        recentFailed.MarkFailed("provider unavailable", DateTimeOffset.UtcNow);
        context.CatalogGenerations.AddRange(current, loading, oldOrphanReady, recentOrphanReady, oldFailed, recentFailed);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        current.MarkCurrent(DateTimeOffset.UtcNow);
        context.ProviderCatalogCurrents.Add(new ProviderCatalogCurrent("carzin", current.Id));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var deleted = await new CatalogRetentionRepository(context)
            .DeleteUnreferencedAsync(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(-1),
                TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, deleted);
        Assert.Equal(4, await context.CatalogGenerations.CountAsync(TestContext.Current.CancellationToken));
        Assert.Contains(context.CatalogGenerations, generation => generation.Id == current.Id);
        Assert.Contains(context.CatalogGenerations, generation => generation.Id == loading.Id);
        Assert.Contains(context.CatalogGenerations, generation => generation.Id == recentFailed.Id);
        Assert.Contains(context.CatalogGenerations, generation => generation.Id == recentOrphanReady.Id);
        Assert.DoesNotContain(context.CatalogGenerations, generation => generation.Id == oldOrphanReady.Id);
    }

    [Fact]
    public async Task Repository_propagates_pre_cancelled_token()
    {
        await using var context = CreateContext();
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CatalogGenerationRepository(context).GetByIdAsync(1, source.Token));
    }

    private static CatalogGeneration ReadyGeneration(string provider)
        => ReadyGeneration(provider, DateTimeOffset.UtcNow.AddMinutes(-2));

    private static CatalogGeneration ReadyGeneration(string provider, DateTimeOffset createdAtUtc)
    {
        var generation = new CatalogGeneration(provider, createdAtUtc);
        generation.MarkReady(1, 1, 1, 0, 0, new string('b', 64), null, createdAtUtc.AddMinutes(1));
        return generation;
    }

    private static BuscarroDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BuscarroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BuscarroDbContext(options);
    }
}

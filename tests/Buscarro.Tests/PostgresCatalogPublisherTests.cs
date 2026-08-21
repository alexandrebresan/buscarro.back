using Buscarro.Domain.Entities;
using Buscarro.Domain.Enums;
using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Buscarro.Tests;

public sealed class PostgresCatalogPublisherTests
{
    [Fact]
    public async Task First_publication_activates_generation_and_creates_global_version()
    {
        await using var context = CreateContext();
        var generation = await AddReadyAsync(context, "carzin", 12);
        var publisher = new PostgresCatalogPublisher(context);

        var publication = await publisher.PublishAsync(
            generation.Id,
            DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.Equal(CatalogGenerationStatus.Current, generation.Status);
        Assert.Equal(generation.Id, context.ProviderCatalogCurrents.Single().CurrentGenerationId);
        Assert.Equal(publication.Id, context.ProviderCatalogCurrents.Single().LastPublicationId);
        Assert.Equal(12, publication.TotalCatalogCount);
        Assert.Equal(1, publication.ProviderCount);
    }

    [Fact]
    public async Task Third_publication_keeps_only_current_and_previous_and_counts_all_providers()
    {
        await using var context = CreateContext();
        var publisher = new PostgresCatalogPublisher(context);
        var first = await AddReadyAsync(context, "carzin", 10);
        await publisher.PublishAsync(first.Id, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        var second = await AddReadyAsync(context, "carzin", 20);
        await publisher.PublishAsync(second.Id, DateTimeOffset.UtcNow.AddMinutes(1), TestContext.Current.CancellationToken);
        var olx = await AddReadyAsync(context, "olx", 7);
        await publisher.PublishAsync(olx.Id, DateTimeOffset.UtcNow.AddMinutes(2), TestContext.Current.CancellationToken);
        var third = await AddReadyAsync(context, "carzin", 30);

        var publication = await publisher.PublishAsync(
            third.Id,
            DateTimeOffset.UtcNow.AddMinutes(3),
            TestContext.Current.CancellationToken);

        Assert.Equal(CatalogGenerationStatus.Ready, first.Status);
        Assert.Equal(CatalogGenerationStatus.Previous, second.Status);
        Assert.Equal(CatalogGenerationStatus.Current, third.Status);
        Assert.Equal(CatalogGenerationStatus.Current, olx.Status);
        Assert.Equal(37, publication.TotalCatalogCount);
        Assert.Equal(2, publication.ProviderCount);
        var pointer = context.ProviderCatalogCurrents.Single(value => value.Provider == "carzin");
        Assert.Equal(third.Id, pointer.CurrentGenerationId);
        Assert.Equal(second.Id, pointer.PreviousGenerationId);
    }

    [Fact]
    public async Task Repeating_current_generation_is_idempotent()
    {
        await using var context = CreateContext();
        var publisher = new PostgresCatalogPublisher(context);
        var generation = await AddReadyAsync(context, "carzin", 1);
        var first = await publisher.PublishAsync(generation.Id, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        var second = await publisher.PublishAsync(generation.Id, DateTimeOffset.UtcNow.AddMinutes(1), TestContext.Current.CancellationToken);

        Assert.Same(first, second);
        Assert.Single(context.CatalogPublications);
    }

    [Fact]
    public async Task Invalid_state_fails_without_publication_or_pointer()
    {
        await using var context = CreateContext();
        var loading = new CatalogGeneration("carzin", DateTimeOffset.UtcNow);
        context.CatalogGenerations.Add(loading);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PostgresCatalogPublisher(context).PublishAsync(
                loading.Id,
                DateTimeOffset.UtcNow,
                TestContext.Current.CancellationToken));

        Assert.Empty(context.CatalogPublications);
        Assert.Empty(context.ProviderCatalogCurrents);
        Assert.Equal(CatalogGenerationStatus.Loading, loading.Status);
    }

    [Fact]
    public async Task Inconsistent_pointer_is_validated_before_any_generation_is_changed()
    {
        await using var context = CreateContext();
        var target = await AddReadyAsync(context, "carzin", 30);
        var invalidCurrent = await AddReadyAsync(context, "carzin", 20);
        var previous = await AddReadyAsync(context, "carzin", 10);
        previous.MarkCurrent(DateTimeOffset.UtcNow);
        previous.MarkPrevious(DateTimeOffset.UtcNow.AddMinutes(1));
        var pointer = new ProviderCatalogCurrent("carzin", invalidCurrent.Id);
        pointer.Publish(invalidCurrent.Id, previous.Id, DateTimeOffset.UtcNow.AddMinutes(2));
        context.ProviderCatalogCurrents.Add(pointer);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PostgresCatalogPublisher(context).PublishAsync(
                target.Id,
                DateTimeOffset.UtcNow.AddMinutes(3),
                TestContext.Current.CancellationToken));

        Assert.Equal(CatalogGenerationStatus.Previous, previous.Status);
        Assert.Equal(CatalogGenerationStatus.Ready, invalidCurrent.Status);
        Assert.Equal(CatalogGenerationStatus.Ready, target.Status);
        Assert.Empty(context.CatalogPublications);
    }

    [Fact]
    public async Task Non_utc_third_publication_fails_before_any_state_or_pointer_mutation()
    {
        await using var context = CreateContext();
        var publisher = new PostgresCatalogPublisher(context);
        var first = await AddReadyAsync(context, "carzin", 10);
        await publisher.PublishAsync(first.Id, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        var second = await AddReadyAsync(context, "carzin", 20);
        await publisher.PublishAsync(second.Id, DateTimeOffset.UtcNow.AddMinutes(1), TestContext.Current.CancellationToken);
        var third = await AddReadyAsync(context, "carzin", 30);
        var pointer = context.ProviderCatalogCurrents.Single();
        var publicationCount = context.CatalogPublications.Count();
        var nonUtc = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.FromHours(-3));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            publisher.PublishAsync(third.Id, nonUtc, TestContext.Current.CancellationToken));

        Assert.Equal(CatalogGenerationStatus.Previous, first.Status);
        Assert.Equal(CatalogGenerationStatus.Current, second.Status);
        Assert.Equal(CatalogGenerationStatus.Ready, third.Status);
        Assert.Equal(second.Id, pointer.CurrentGenerationId);
        Assert.Equal(first.Id, pointer.PreviousGenerationId);
        Assert.Equal(publicationCount, context.CatalogPublications.Count());
        Assert.All(context.ChangeTracker.Entries(), entry => Assert.Equal(EntityState.Unchanged, entry.State));
    }

    [Fact]
    public void Catalog_publication_and_provider_pointer_reject_non_utc_before_mutation()
    {
        var nonUtc = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.FromHours(2));
        Assert.Throws<ArgumentException>(() => new CatalogPublication(nonUtc, 1, 1));

        var pointer = new ProviderCatalogCurrent("carzin", 10);
        var originalUpdatedAt = pointer.UpdatedAtUtc;
        Assert.Throws<ArgumentException>(() => pointer.Publish(20, 10, nonUtc));

        Assert.Equal(10, pointer.CurrentGenerationId);
        Assert.Null(pointer.PreviousGenerationId);
        Assert.Equal(originalUpdatedAt, pointer.UpdatedAtUtc);
    }

    private static async Task<CatalogGeneration> AddReadyAsync(BuscarroDbContext context, string provider, int validItems)
    {
        var generation = new CatalogGeneration(provider, DateTimeOffset.UtcNow.AddMinutes(-1));
        generation.MarkReady(1, validItems, validItems, 0, 0, new string('c', 64), null, DateTimeOffset.UtcNow);
        context.CatalogGenerations.Add(generation);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
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

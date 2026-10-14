using Buscarro.Application.Queries;
using Buscarro.Application.Handlers;
using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;

namespace Buscarro.Tests;

public sealed class GetProviderSynchronizationDueHandlerTests
{
    [Fact]
    public void Query_preserves_provider_and_interval()
    {
        var query = new GetProviderSynchronizationDueQuery("AutoDev", TimeSpan.FromHours(48));
        Assert.Equal("AutoDev", query.Provider);
        Assert.Equal(TimeSpan.FromHours(48), query.Interval);
    }

    [Fact]
    public async Task First_sync_is_due()
    {
        var handler = new GetProviderSynchronizationDueHandler(new Repository(), TimeProvider.System);
        Assert.True(await handler.Handle(new GetProviderSynchronizationDueQuery("AutoDev", TimeSpan.FromDays(2)), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task Activated_generation_observes_due_boundary_without_clock_skew(int elapsedHours, bool due)
    {
        var now = new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        var generation = new CatalogGeneration("AutoDev", now);
        typeof(CatalogGeneration).GetProperty(nameof(CatalogGeneration.ActivatedAtUtc))!.SetValue(generation, now.AddHours(-elapsedHours));
        var handler = new GetProviderSynchronizationDueHandler(new Repository(generation), new FixedTimeProvider(now));
        Assert.Equal(due, await handler.Handle(new GetProviderSynchronizationDueQuery("AutoDev", TimeSpan.FromHours(1)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Maximum_interval_does_not_overflow_and_is_not_due()
    {
        var now = new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        var generation = new CatalogGeneration("AutoDev", now);
        typeof(CatalogGeneration).GetProperty(nameof(CatalogGeneration.ActivatedAtUtc))!.SetValue(generation, now);
        var handler = new GetProviderSynchronizationDueHandler(new Repository(generation), new FixedTimeProvider(now));
        Assert.False(await handler.Handle(new GetProviderSynchronizationDueQuery("AutoDev", TimeSpan.MaxValue), TestContext.Current.CancellationToken));
    }

    private sealed class Repository(CatalogGeneration? current = null) : ICatalogGenerationRepository
    {
        public Task AddAsync(CatalogGeneration generation, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<CatalogGeneration?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult<CatalogGeneration?>(null);
        public Task<CatalogGeneration?> GetCurrentByProviderAsync(string provider, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(current); }
        public Task<IReadOnlyCollection<CatalogGeneration>> ListLoadingBeforeAsync(DateTimeOffset createdBeforeUtc, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<CatalogGeneration>>([]);
    }
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}

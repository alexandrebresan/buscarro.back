using System.Reflection;
using Buscarro.Application.Commands;
using Buscarro.Application.Handlers;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Domain.Enums;
using Buscarro.Domain.Interfaces;

namespace Buscarro.Tests;

public sealed class CleanupCatalogHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Active_provider_lock_prevents_cleanup_of_loading_generation()
    {
        var generation = Generation(Now.AddHours(-3));
        var fixture = new Fixture([generation], generation, acquire: false);

        var result = await fixture.HandleAsync();

        Assert.Equal(0, result.AbandonedMarkedFailed);
        Assert.Equal(CatalogGenerationStatus.Loading, generation.Status);
    }

    [Fact]
    public async Task Generation_inside_timeout_plus_grace_window_is_preserved()
    {
        var generation = Generation(Now.AddHours(-2));
        var fixture = new Fixture([generation], generation);

        var result = await fixture.HandleAsync();

        Assert.Equal(0, result.AbandonedMarkedFailed);
        Assert.Equal(0, fixture.SyncLock.Attempts);
        Assert.Equal(CatalogGenerationStatus.Loading, generation.Status);
    }

    [Fact]
    public async Task Expired_unlocked_generation_is_revalidated_marked_failed_and_saved_under_lock()
    {
        var generation = Generation(Now.AddHours(-3));
        var fixture = new Fixture([generation], generation);

        var result = await fixture.HandleAsync();

        Assert.Equal(CatalogGenerationStatus.Failed, generation.Status);
        Assert.Equal(1, result.AbandonedMarkedFailed);
        Assert.True(fixture.SyncLock.WasHeldWhenSaved);
        Assert.Equal(Now.AddDays(-1), fixture.Retention.ReadyBefore);
        Assert.Equal(Now.AddDays(-7), fixture.Retention.FailedBefore);
    }

    [Fact]
    public async Task State_changed_after_candidate_listing_is_preserved_after_lock_revalidation()
    {
        var listed = Generation(Now.AddHours(-3));
        var current = Generation(Now.AddHours(-3));
        current.MarkReady(1, 1, 1, 0, 0, new string('a', 64), null, Now.AddHours(-2));
        var fixture = new Fixture([listed], current);

        var result = await fixture.HandleAsync();

        Assert.Equal(0, result.AbandonedMarkedFailed);
        Assert.Equal(CatalogGenerationStatus.Ready, current.Status);
    }

    private static CatalogGeneration Generation(DateTimeOffset created)
    {
        var generation = new CatalogGeneration("Carzin", created);
        typeof(CatalogGeneration).GetProperty(nameof(CatalogGeneration.Id))!.SetValue(generation, 1);
        return generation;
    }

    private sealed class Fixture
    {
        public SyncLock SyncLock { get; }
        public Retention Retention { get; } = new();
        private readonly CleanupCatalogHandler _handler;

        public Fixture(IReadOnlyCollection<CatalogGeneration> listed, CatalogGeneration current, bool acquire = true)
        {
            SyncLock = new(acquire);
            _handler = new(new Generations(listed, current), Retention, new UnitOfWork(SyncLock), SyncLock, new FixedTimeProvider(Now));
        }

        public Task<Buscarro.Application.DTOs.CatalogCleanupResult> HandleAsync() => _handler.Handle(
            new CleanupCatalogCommand(Now - TimeSpan.FromHours(2) - TimeSpan.FromMinutes(15), Now.AddDays(-1), Now.AddDays(-7)),
            TestContext.Current.CancellationToken);
    }

    private sealed class Generations(IReadOnlyCollection<CatalogGeneration> listed, CatalogGeneration current) : ICatalogGenerationRepository
    {
        public Task AddAsync(CatalogGeneration generation, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<CatalogGeneration?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult<CatalogGeneration?>(current);
        public Task<CatalogGeneration?> GetCurrentByProviderAsync(string provider, CancellationToken cancellationToken) => Task.FromResult<CatalogGeneration?>(null);
        public Task<IReadOnlyCollection<CatalogGeneration>> ListLoadingBeforeAsync(DateTimeOffset createdBeforeUtc, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<CatalogGeneration>>(listed.Where(value => value.CreatedAtUtc < createdBeforeUtc).ToArray());
    }

    private sealed class SyncLock(bool acquire) : IProviderSynchronizationLock
    {
        public int Attempts { get; private set; }
        public bool Held { get; private set; }
        public bool WasHeldWhenSaved { get; set; }
        public Task<IAsyncDisposable?> TryAcquireAsync(string provider, CancellationToken cancellationToken)
        {
            Attempts++;
            if (!acquire) return Task.FromResult<IAsyncDisposable?>(null);
            Held = true;
            return Task.FromResult<IAsyncDisposable?>(new Lease(this));
        }
        private sealed class Lease(SyncLock owner) : IAsyncDisposable
        {
            public ValueTask DisposeAsync() { owner.Held = false; return ValueTask.CompletedTask; }
        }
    }

    private sealed class Retention : ICatalogRetentionRepository
    {
        public DateTimeOffset ReadyBefore { get; private set; }
        public DateTimeOffset FailedBefore { get; private set; }
        public Task<int> DeleteVehicleRowsForFailedGenerationsAsync(CancellationToken cancellationToken) => Task.FromResult(4);
        public Task<int> DeleteUnreferencedAsync(DateTimeOffset readyBeforeUtc, DateTimeOffset failedBeforeUtc, CancellationToken cancellationToken)
        { ReadyBefore = readyBeforeUtc; FailedBefore = failedBeforeUtc; return Task.FromResult(2); }
    }

    private sealed class UnitOfWork(SyncLock syncLock) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        { if (syncLock.Held) syncLock.WasHeldWhenSaved = true; return Task.FromResult(1); }
    }
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}

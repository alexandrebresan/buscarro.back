using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Services;
using Buscarro.Infrastructure.Providers;
using Buscarro.Infrastructure.Repositories;
using Buscarro.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Buscarro.Tests;

public sealed class VehicleSearchResultCacheTests
{
    private static readonly VehicleCatalogSearchCriteriaNormalizer Normalizer = new();

    [Fact]
    public void Result_key_uses_normalized_filters_order_and_paging_without_publication()
    {
        var criteria = Normalizer.Normalize(new VehicleCatalogSearchRequest { Manufacturer = ["Fiat", "FORD"] });
        var equivalent = Normalizer.Normalize(new VehicleCatalogSearchRequest { Manufacturer = ["ford", " fiat ", "Fiat"] });
        var key = CatalogQueryCache.BuildResultKey(criteria);
        Assert.Equal(key, CatalogQueryCache.BuildResultKey(equivalent));
        Assert.NotEqual(key, CatalogQueryCache.BuildResultKey(criteria with { Page = 2 }));
        Assert.NotEqual(key, CatalogQueryCache.BuildResultKey(criteria with { PageSize = 50 }));
        Assert.NotEqual(key, CatalogQueryCache.BuildResultKey(criteria with { Order = "price" }));
        Assert.NotEqual(key, CatalogQueryCache.BuildResultKey(criteria with { YearMin = 2020 }));
        Assert.NotEqual(key, CatalogQueryCache.BuildResultKey(criteria with { Providers = ["carzin"] }));
    }

    [Fact]
    public void Result_expires_at_five_minutes_without_renewing_on_access()
    {
        var clock = new ManualClock();
        using var cache = new CatalogQueryCache(Options.Create(new CatalogQueryOptions()), clock);
        Assert.Equal(300, new CatalogQueryOptions().ResultCacheTtlSeconds);
        cache.SetResult("key", Result());
        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.True(cache.TryGetResult("key", out var cached));
        Assert.Equal(7, cached.PublicationId);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.False(cache.TryGetResult("key", out _));
    }

    [Fact]
    public void Cached_result_does_not_share_mutable_objects_with_source_or_other_responses()
    {
        using var cache = new CatalogQueryCache(Options.Create(new CatalogQueryOptions()));
        var source = Result();
        cache.SetResult("key", source);
        source.Vehicles.First().Title = "Changed source";
        Assert.True(cache.TryGetResult("key", out var first));
        Assert.Equal("Original", first.Vehicles.First().Title);
        first.Vehicles.First().Title = "Changed response";
        Assert.True(cache.TryGetResult("key", out var second));
        Assert.Equal("Original", second.Vehicles.First().Title);
        Assert.NotSame(first.Vehicles, second.Vehicles);
        Assert.NotSame(first.Vehicles.First(), second.Vehicles.First());
        Assert.NotSame(first.Providers.First(), second.Providers.First());
        Assert.True(second.Providers.First().FromCache);
        Assert.False(second.Providers.First().UsedStaleCache);
    }

    [Fact]
    public async Task Cache_hit_returns_complete_response_without_opening_database()
    {
        var options = Options.Create(new CatalogQueryOptions());
        using var cache = new CatalogQueryCache(options);
        var criteria = Normalizer.Normalize(new VehicleCatalogSearchRequest());
        cache.SetResult(CatalogQueryCache.BuildResultKey(criteria), Result());
        // No server: this test must succeed without even attempting a connection.
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1;Pooling=false");
        var service = new PostgresVehicleCatalogReadService(dataSource, cache, options);
        var result = await service.SearchAsync(criteria, TestContext.Current.CancellationToken);
        Assert.Equal(7, result.PublicationId);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(Result().CatalogUpdatedAtUtc, result.CatalogUpdatedAtUtc);
        Assert.Equal("Original", Assert.Single(result.Vehicles).Title);
        Assert.True(Assert.Single(result.Providers).FromCache);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SearchAsync(criteria, cancelled.Token));
    }

    [Fact]
    public async Task Expired_result_is_not_served_and_database_failure_is_not_cached()
    {
        var clock = new ManualClock();
        var options = Options.Create(new CatalogQueryOptions());
        using var cache = new CatalogQueryCache(options, clock);
        var criteria = Normalizer.Normalize(new VehicleCatalogSearchRequest());
        var key = CatalogQueryCache.BuildResultKey(criteria);
        cache.SetResult(key, Result());
        clock.Advance(TimeSpan.FromMinutes(5));
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1;Pooling=false");
        var service = new PostgresVehicleCatalogReadService(dataSource, cache, options);
        await Assert.ThrowsAsync<CatalogUnavailableException>(() => service.SearchAsync(criteria, TestContext.Current.CancellationToken));
        Assert.False(cache.TryGetResult(key, out _));
    }

    [Fact]
    public void Empty_successful_result_is_cached()
    {
        using var cache = new CatalogQueryCache(Options.Create(new CatalogQueryOptions()));
        cache.SetResult("empty", new VehicleCatalogSearchResult { PublicationId = 7, TotalCount = 0, TotalPages = 0, Page = 1, PageSize = 20 });
        Assert.True(cache.TryGetResult("empty", out var result));
        Assert.Empty(result.Vehicles);
        Assert.Equal(0, result.TotalCount);
    }

    private static VehicleCatalogSearchResult Result() => new()
    {
        PublicationId = 7, CatalogUpdatedAtUtc = DateTimeOffset.Parse("2026-10-15T12:00:00Z"),
        TotalCount = 1, TotalPages = 1, Page = 1, PageSize = 20,
        Vehicles = [new VehicleResponse { ExternalId = "a1", Provider = "AutoDev", Title = "Original", Price = 100m }],
        Providers = [new ProviderStatusResponse { Provider = "AutoDev", Success = true, CurrentResults = 1, CurrentPage = 1 }]
    };

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.Parse("2026-10-15T12:00:00Z");
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan interval) => _now += interval;
    }
}

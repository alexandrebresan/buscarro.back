using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Buscarro.Tests;

public sealed class VehicleProviderCatalogReadServiceTests
{
    [Fact]
    public async Task ResolveNamesAsync_returns_manufacturer_model_and_version_from_memory_cache_before_database()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("vehicle-provider-catalog|manufacturer|CARZIN|1|10", "Honda");
        cache.Set("vehicle-provider-catalog|model|CARZIN|1|10|20", "Civic");
        cache.Set("vehicle-provider-catalog|version|CARZIN|1|10|20|30", "Touring");
        var constructor = typeof(VehicleProviderCatalogReadService).GetConstructor(
            [typeof(IDbContextFactory<BuscarroDbContext>), typeof(IMemoryCache)]);

        Assert.NotNull(constructor);

        var service = Assert.IsType<VehicleProviderCatalogReadService>(
            constructor.Invoke([new ThrowingDbContextFactory(), cache]));

        var names = await service.ResolveNamesAsync(
            "Carzin",
            1,
            "10",
            "20",
            "30",
            CancellationToken.None);

        Assert.Equal("Honda", names.ManufacturerName);
        Assert.Equal("Civic", names.ModelName);
        Assert.Equal("Touring", names.VersionName);
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<BuscarroDbContext>
    {
        public BuscarroDbContext CreateDbContext()
        {
            throw new InvalidOperationException("Database should not be used on cache hit.");
        }

        public Task<BuscarroDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Database should not be used on cache hit.");
        }
    }
}

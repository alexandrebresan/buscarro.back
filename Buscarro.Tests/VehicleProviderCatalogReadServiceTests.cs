using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.Providers;
using Buscarro.Domain.Entities;
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

    [Fact]
    public async Task ListManufacturersAsync_returns_carzin_car_manufacturers_ordered_by_name()
    {
        var dbContextFactory = CreateFactory();
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.VehicleProviderManufacturers.AddRange(
                new VehicleProviderManufacturer
                {
                    Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    ProviderName = "Carzin",
                    VehicleType = 1,
                    ProviderCode = "20",
                    Name = "Toyota"
                },
                new VehicleProviderManufacturer
                {
                    Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    ProviderName = "Carzin",
                    VehicleType = 1,
                    ProviderCode = "10",
                    Name = "Honda"
                },
                new VehicleProviderManufacturer
                {
                    ProviderName = "Other",
                    VehicleType = 1,
                    ProviderCode = "99",
                    Name = "Nao deve aparecer"
                });
            await dbContext.SaveChangesAsync();
        }
        var service = new VehicleProviderCatalogReadService(
            dbContextFactory,
            new MemoryCache(new MemoryCacheOptions()));

        var result = await service.ListManufacturersAsync(CancellationToken.None);

        Assert.Collection(
            result,
            item =>
            {
                Assert.Equal("10", item.ProviderCode);
                Assert.Equal("Honda", item.Name);
                Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), item.ManufacturerId);
            },
            item =>
            {
                Assert.Equal("20", item.ProviderCode);
                Assert.Equal("Toyota", item.Name);
                Assert.Equal(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), item.ManufacturerId);
            });
    }

    [Fact]
    public async Task ListModelsAsync_returns_models_for_selected_manufacturer()
    {
        var dbContextFactory = CreateFactory();
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var honda = new VehicleProviderManufacturer
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                ProviderName = "Carzin",
                VehicleType = 1,
                ProviderCode = "10",
                Name = "Honda"
            };
            var toyota = new VehicleProviderManufacturer
            {
                ProviderName = "Carzin",
                VehicleType = 1,
                ProviderCode = "20",
                Name = "Toyota"
            };
            dbContext.VehicleProviderManufacturers.AddRange(honda, toyota);
            dbContext.VehicleProviderModels.AddRange(
                new VehicleProviderModel
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Manufacturer = honda,
                    ProviderName = "Carzin",
                    VehicleType = 1,
                    ProviderCode = "2",
                    Name = "Fit"
                },
                new VehicleProviderModel
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Manufacturer = honda,
                    ProviderName = "Carzin",
                    VehicleType = 1,
                    ProviderCode = "1",
                    Name = "Civic"
                },
                new VehicleProviderModel
                {
                    Manufacturer = toyota,
                    ProviderName = "Carzin",
                    VehicleType = 1,
                    ProviderCode = "3",
                    Name = "Corolla"
                });
            await dbContext.SaveChangesAsync();
        }
        var service = new VehicleProviderCatalogReadService(
            dbContextFactory,
            new MemoryCache(new MemoryCacheOptions()));

        var result = await service.ListModelsAsync(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            CancellationToken.None);

        Assert.Collection(
            result,
            item =>
            {
                Assert.Equal("1", item.ProviderCode);
                Assert.Equal("Civic", item.Name);
                Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), item.ModelId);
            },
            item =>
            {
                Assert.Equal("2", item.ProviderCode);
                Assert.Equal("Fit", item.Name);
                Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), item.ModelId);
            });
    }

    [Fact]
    public async Task ListVersionsAsync_returns_versions_for_selected_model()
    {
        var dbContextFactory = CreateFactory();
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var manufacturer = new VehicleProviderManufacturer
            {
                ProviderName = "Carzin",
                VehicleType = 1,
                ProviderCode = "10",
                Name = "Honda"
            };
            var civic = new VehicleProviderModel
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Manufacturer = manufacturer,
                ProviderName = "Carzin",
                VehicleType = 1,
                ProviderCode = "1",
                Name = "Civic"
            };
            var fit = new VehicleProviderModel
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Manufacturer = manufacturer,
                ProviderName = "Carzin",
                VehicleType = 1,
                ProviderCode = "2",
                Name = "Fit"
            };
            dbContext.VehicleProviderModels.AddRange(civic, fit);
            dbContext.VehicleProviderVersions.AddRange(
                new VehicleProviderVersion
                {
                    Model = civic,
                    ProviderName = "Carzin",
                    VehicleType = 1,
                    ProviderCode = "101",
                    Name = "Touring"
                },
                new VehicleProviderVersion
                {
                    Model = civic,
                    ProviderName = "Carzin",
                    VehicleType = 1,
                    ProviderCode = "100",
                    Name = "EXL"
                },
                new VehicleProviderVersion
                {
                    Model = fit,
                    ProviderName = "Carzin",
                    VehicleType = 1,
                    ProviderCode = "200",
                    Name = "LX"
                });
            await dbContext.SaveChangesAsync();
        }
        var service = new VehicleProviderCatalogReadService(
            dbContextFactory,
            new MemoryCache(new MemoryCacheOptions()));

        var result = await service.ListVersionsAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CancellationToken.None);

        Assert.Collection(
            result,
            item =>
            {
                Assert.Equal("100", item.ProviderCode);
                Assert.Equal("EXL", item.Name);
            },
            item =>
            {
                Assert.Equal("101", item.ProviderCode);
                Assert.Equal("Touring", item.Name);
            });
    }

    private static InMemoryDbContextFactory CreateFactory()
    {
        var options = new DbContextOptionsBuilder<BuscarroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new InMemoryDbContextFactory(options);
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

    private sealed class InMemoryDbContextFactory : IDbContextFactory<BuscarroDbContext>
    {
        private readonly DbContextOptions<BuscarroDbContext> _options;

        public InMemoryDbContextFactory(DbContextOptions<BuscarroDbContext> options)
        {
            _options = options;
        }

        public BuscarroDbContext CreateDbContext()
        {
            return new BuscarroDbContext(_options);
        }

        public Task<BuscarroDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}

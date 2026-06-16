using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Buscarro.Tests;

public sealed class VehicleCatalogReadServiceTests
{
    [Fact]
    public async Task GetSnapshotAsync_filters_provider_results_by_state_after_cache_or_provider_response()
    {
        var provider = new FakeVehicleProvider(
            new Vehicle { ExternalId = "1", Provider = "Carzin", Brand = "Honda", Model = "Civic", State = "SP", Doors = 4 },
            new Vehicle { ExternalId = "2", Provider = "Carzin", Brand = "Honda", Model = "Fit", State = "RJ", Doors = 2 });
        var service = CreateService(provider);

        var snapshot = await service.GetSnapshotAsync(
            new VehicleSearchCriteria { State = "SP" },
            CancellationToken.None);

        var vehicle = Assert.Single(snapshot.Vehicles);
        Assert.Equal("1", vehicle.ExternalId);
        Assert.Equal(4, vehicle.Doors);
    }

    [Theory]
    [InlineData("price", "cheap-old", "expensive-new")]
    [InlineData("price-inverse", "expensive-new", "cheap-old")]
    [InlineData("year", "cheap-old", "expensive-new")]
    [InlineData("year-inverse", "expensive-new", "cheap-old")]
    public async Task GetSnapshotAsync_applies_requested_order_to_aggregated_results(
        string order,
        string firstExpectedExternalId,
        string secondExpectedExternalId)
    {
        var expensiveNew = new Vehicle
        {
            ExternalId = "expensive-new",
            Provider = "Carzin",
            Brand = "A",
            Model = "Expensive New",
            Price = 30000,
            Year = 2024
        };
        var cheapOld = new Vehicle
        {
            ExternalId = "cheap-old",
            Provider = "Carzin",
            Brand = "Z",
            Model = "Cheap Old",
            Price = 10000,
            Year = 2018
        };
        var provider = order.EndsWith("-inverse", StringComparison.OrdinalIgnoreCase)
            ? new FakeVehicleProvider(cheapOld, expensiveNew)
            : new FakeVehicleProvider(expensiveNew, cheapOld);
        var service = CreateService(provider);

        var snapshot = await service.GetSnapshotAsync(
            new VehicleSearchCriteria { Order = order },
            CancellationToken.None);

        Assert.Equal(firstExpectedExternalId, snapshot.Vehicles.First().ExternalId);
        Assert.Equal(secondExpectedExternalId, snapshot.Vehicles.Skip(1).First().ExternalId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relevance")]
    [InlineData("rand")]
    public async Task GetSnapshotAsync_preserves_provider_order_for_provider_driven_orders(string? order)
    {
        var provider = new FakeVehicleProvider(
            new Vehicle { ExternalId = "provider-first", Provider = "Carzin", Brand = "Z", Model = "Zeta", Year = 2018 },
            new Vehicle { ExternalId = "provider-second", Provider = "Carzin", Brand = "A", Model = "Alpha", Year = 2024 });
        var service = CreateService(provider);

        var snapshot = await service.GetSnapshotAsync(
            new VehicleSearchCriteria { Order = order },
            CancellationToken.None);

        Assert.Equal(
            ["provider-first", "provider-second"],
            snapshot.Vehicles.Select(vehicle => vehicle.ExternalId).ToArray());
    }

    [Fact]
    public async Task GetSnapshotAsync_serves_repeated_equal_search_from_memory_cache()
    {
        var provider = new FakeVehicleProvider(
            new Vehicle { ExternalId = "1", Provider = "Carzin", Brand = "Honda", Model = "Civic", Doors = 4 });
        var service = CreateService(provider);
        var criteria = new VehicleSearchCriteria { State = "SP" };

        await service.GetSnapshotAsync(criteria, CancellationToken.None);
        var secondSnapshot = await service.GetSnapshotAsync(criteria, CancellationToken.None);

        Assert.Equal(1, provider.CallCount);
        Assert.True(Assert.Single(secondSnapshot.Providers).FromCache);
    }

    [Fact]
    public async Task FindVehicleByProviderAndExternalIdAsync_returns_vehicle_from_recent_snapshot()
    {
        var provider = new FakeVehicleProvider(
            new Vehicle { ExternalId = "42", Provider = "Carzin", Brand = "Honda", Model = "Civic", Color = "Branco" });
        var service = CreateService(provider);

        await service.GetSnapshotAsync(new VehicleSearchCriteria { State = string.Empty }, CancellationToken.None);

        var vehicle = await service.FindVehicleByProviderAndExternalIdAsync("Carzin", "42", CancellationToken.None);

        Assert.NotNull(vehicle);
        Assert.Equal("Civic", vehicle.Model);
    }

    [Fact]
    public async Task GetSnapshotAsync_does_not_cache_failed_provider_response()
    {
        var provider = new ThrowingVehicleProvider();
        var service = CreateService(provider);

        var firstSnapshot = await service.GetSnapshotAsync(new VehicleSearchCriteria(), CancellationToken.None);
        var secondSnapshot = await service.GetSnapshotAsync(new VehicleSearchCriteria(), CancellationToken.None);

        Assert.Equal(2, provider.CallCount);
        Assert.False(Assert.Single(firstSnapshot.Providers).FromCache);
        Assert.False(Assert.Single(secondSnapshot.Providers).FromCache);
    }

    private static VehicleCatalogReadService CreateService(params IVehicleProvider[] providers)
    {
        return new VehicleCatalogReadService(
            providers,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new VehicleCacheOptions { DurationMinutes = 5 }),
            TimeProvider.System,
            NullLogger<VehicleCatalogReadService>.Instance);
    }

    private sealed class FakeVehicleProvider : IVehicleProvider
    {
        private readonly IReadOnlyCollection<Vehicle> _vehicles;

        public FakeVehicleProvider(params Vehicle[] vehicles)
        {
            _vehicles = vehicles;
        }

        public string ProviderName => "Carzin";
        public int CallCount { get; private set; }

        public Task<VehicleProviderResult> GetVehiclesAsync(
            VehicleSearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            CallCount++;

            return Task.FromResult(new VehicleProviderResult
            {
                Vehicles = _vehicles,
                CurrentResults = _vehicles.Count,
                TotalResults = _vehicles.Count,
                CurrentPage = 1,
                TotalPages = 1
            });
        }
    }

    private sealed class ThrowingVehicleProvider : IVehicleProvider
    {
        public string ProviderName => "Carzin";
        public int CallCount { get; private set; }

        public Task<VehicleProviderResult> GetVehiclesAsync(
            VehicleSearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            CallCount++;
            throw new InvalidOperationException("Provider indisponivel.");
        }
    }
}

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

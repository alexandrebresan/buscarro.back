using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Handlers;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;

namespace Buscarro.Tests;

public sealed class AddFavoriteVehicleHandlerTests
{
    [Fact]
    public async Task Handle_saves_snapshot_resolved_by_provider_and_external_id()
    {
        var userId = Guid.NewGuid();
        var favorites = new FakeFavoriteVehicleRepository();
        var handler = new AddFavoriteVehicleHandler(
            new FakeCurrentUserService(userId),
            favorites,
            new FakeUnitOfWork(),
            new FakeVehicleCatalogReadService(new Vehicle
            {
                ExternalId = "42",
                Provider = "Carzin",
                Title = "Honda Civic Touring",
                Brand = "Honda",
                Model = "Civic",
                Version = "Touring",
                Year = 2022,
                Mileage = 15000,
                Fuel = "Flex",
                Transmission = "Automatico",
                Color = "Branco",
                Price = 100000,
                City = "Sao Paulo",
                State = "SP",
                Url = "https://example.com/ad",
                ImageUrl = "https://example.com/image.jpg"
            }));

        var response = await handler.Handle(new AddFavoriteVehicleCommand("Carzin", "42"), CancellationToken.None);

        Assert.NotNull(favorites.Added);
        Assert.Equal(userId, favorites.Added.UserId);
        Assert.Equal("Honda Civic Touring", favorites.Added.Title);
        Assert.Equal("Branco", favorites.Added.Color);
        Assert.Equal(response.Id, favorites.Added.Id);
    }

    [Fact]
    public async Task Handle_throws_not_found_when_snapshot_cannot_be_resolved()
    {
        var handler = new AddFavoriteVehicleHandler(
            new FakeCurrentUserService(Guid.NewGuid()),
            new FakeFavoriteVehicleRepository(),
            new FakeUnitOfWork(),
            new FakeVehicleCatalogReadService(null));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new AddFavoriteVehicleCommand("Carzin", "404"), CancellationToken.None));
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(Guid userId)
        {
            UserId = userId;
        }

        public Guid? UserId { get; }
    }

    private sealed class FakeVehicleCatalogReadService : IVehicleCatalogReadService
    {
        private readonly Vehicle? _vehicle;

        public FakeVehicleCatalogReadService(Vehicle? vehicle)
        {
            _vehicle = vehicle;
        }

        public Task<VehicleCatalogSnapshot> GetSnapshotAsync(
            VehicleSearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new VehicleCatalogSnapshot());
        }

        public Task<Vehicle?> FindVehicleByProviderAndExternalIdAsync(
            string provider,
            string externalId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_vehicle);
        }
    }

    private sealed class FakeFavoriteVehicleRepository : IFavoriteVehicleRepository
    {
        public FavoriteVehicle? Added { get; private set; }

        public Task<IReadOnlyCollection<FavoriteVehicle>> ListByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<FavoriteVehicle>>([]);
        }

        public Task<FavoriteVehicle?> GetByIdAndUserIdAsync(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<FavoriteVehicle?>(null);
        }

        public Task<bool> ExistsAsync(
            Guid userId,
            string provider,
            string externalId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task AddAsync(FavoriteVehicle favorite, CancellationToken cancellationToken)
        {
            Added = favorite;
            return Task.CompletedTask;
        }

        public void Remove(FavoriteVehicle favorite)
        {
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(1);
        }
    }
}

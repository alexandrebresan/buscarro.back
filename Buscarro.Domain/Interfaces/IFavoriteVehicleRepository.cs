using Buscarro.Domain.Entities;

namespace Buscarro.Domain.Interfaces;

public interface IFavoriteVehicleRepository
{
    Task<IReadOnlyCollection<FavoriteVehicle>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<FavoriteVehicle?> GetByIdAndUserIdAsync(Guid id, Guid userId, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid userId, string provider, string externalId, CancellationToken cancellationToken);
    Task AddAsync(FavoriteVehicle favorite, CancellationToken cancellationToken);
    void Remove(FavoriteVehicle favorite);
}

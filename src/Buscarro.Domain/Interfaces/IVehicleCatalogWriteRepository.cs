using Buscarro.Domain.Entities;

namespace Buscarro.Domain.Interfaces;

public interface IVehicleCatalogWriteRepository
{
    Task AddRangeAsync(IReadOnlyCollection<VehicleCatalogItem> items, CancellationToken cancellationToken);
}

using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;
using Buscarro.Infrastructure.Persistence;

namespace Buscarro.Infrastructure.Repositories;

public sealed class VehicleCatalogWriteRepository(BuscarroDbContext dbContext) : IVehicleCatalogWriteRepository
{
    public Task AddRangeAsync(IReadOnlyCollection<VehicleCatalogItem> items, CancellationToken cancellationToken)
        => dbContext.VehicleCatalog.AddRangeAsync(items, cancellationToken);
}

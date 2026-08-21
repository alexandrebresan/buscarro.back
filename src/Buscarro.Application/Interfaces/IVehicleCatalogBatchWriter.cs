using Buscarro.Domain.Entities;

namespace Buscarro.Application.Interfaces;

public interface IVehicleCatalogBatchWriter
{
    Task WriteBatchAsync(IReadOnlyCollection<VehicleCatalogItem> items, CancellationToken cancellationToken);
}

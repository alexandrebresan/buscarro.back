using Buscarro.Application.DTOs;

namespace Buscarro.Application.Interfaces;

public interface IVehicleCatalogReadService
{
    Task<VehicleCatalogSnapshot> GetSnapshotAsync(
        VehicleSearchCriteria criteria,
        CancellationToken cancellationToken);
}

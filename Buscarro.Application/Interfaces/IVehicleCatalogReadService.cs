using Buscarro.Application.DTOs;
using Buscarro.Domain.Entities;

namespace Buscarro.Application.Interfaces;

public interface IVehicleCatalogReadService
{
    Task<VehicleCatalogSnapshot> GetSnapshotAsync(
        VehicleSearchCriteria criteria,
        CancellationToken cancellationToken);

    Task<Vehicle?> FindVehicleByProviderAndExternalIdAsync(
        string provider,
        string externalId,
        CancellationToken cancellationToken);
}

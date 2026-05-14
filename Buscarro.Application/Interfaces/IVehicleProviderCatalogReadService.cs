using Buscarro.Application.DTOs;

namespace Buscarro.Application.Interfaces;

public interface IVehicleProviderCatalogReadService
{
    Task<VehicleProviderCatalogNames> ResolveNamesAsync(
        string providerName,
        int vehicleType,
        string manufacturerCode,
        string modelCode,
        string versionCode,
        CancellationToken cancellationToken);
}

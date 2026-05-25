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

    Task<IReadOnlyCollection<ManufacturerCatalogItemResponse>> ListManufacturersAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ModelCatalogItemResponse>> ListModelsAsync(
        Guid manufacturerId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<VersionCatalogItemResponse>> ListVersionsAsync(
        Guid modelId,
        CancellationToken cancellationToken);
}

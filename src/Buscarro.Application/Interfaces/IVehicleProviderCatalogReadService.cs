using Buscarro.Application.DTOs;

namespace Buscarro.Application.Interfaces;

public interface IVehicleProviderCatalogReadService
{
    Task<IReadOnlyCollection<ManufacturerCatalogItemResponse>> ListManufacturersAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ModelCatalogItemResponse>> ListModelsAsync(
        Guid manufacturerId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<VersionCatalogItemResponse>> ListVersionsAsync(
        Guid modelId,
        CancellationToken cancellationToken);
}

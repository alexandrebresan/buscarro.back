using Buscarro.Domain.Entities;

namespace Buscarro.Infrastructure.Providers;

public interface ICarzinCatalogStore
{
    Task<VehicleProviderManufacturer> UpsertManufacturerAsync(
        string providerName,
        int vehicleType,
        string providerCode,
        string name,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<VehicleProviderModel> UpsertModelAsync(
        VehicleProviderManufacturer manufacturer,
        string providerName,
        int vehicleType,
        string providerCode,
        string name,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<VehicleProviderVersion> UpsertVersionAsync(
        VehicleProviderModel model,
        string providerName,
        int vehicleType,
        string providerCode,
        string name,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

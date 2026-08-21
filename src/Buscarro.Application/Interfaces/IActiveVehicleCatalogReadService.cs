using Buscarro.Domain.Entities;

namespace Buscarro.Application.Interfaces;

public interface IActiveVehicleCatalogReadService
{
    Task<Vehicle?> FindByProviderAndExternalIdAsync(
        string provider,
        string externalId,
        CancellationToken cancellationToken);
}

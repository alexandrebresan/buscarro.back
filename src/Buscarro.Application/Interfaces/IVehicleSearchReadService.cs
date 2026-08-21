using Buscarro.Application.DTOs;

namespace Buscarro.Application.Interfaces;

public interface IVehicleSearchReadService
{
    Task<VehicleCatalogSearchResult> SearchAsync(
        NormalizedVehicleCatalogSearchCriteria criteria,
        CancellationToken cancellationToken);
}

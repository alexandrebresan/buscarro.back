using Buscarro.Application.DTOs;

namespace Buscarro.Application.Interfaces;

public interface IVehicleSearchTaxonomyProvider
{
    Task<VehicleSearchTaxonomy> GetAsync(
        CancellationToken cancellationToken);
}
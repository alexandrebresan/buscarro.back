using Buscarro.Application.DTOs;

namespace Buscarro.Application.Interfaces;

public interface IVehicleProvider
{
    string ProviderName { get; }

    Task<VehicleProviderResult> GetVehiclesAsync(
        VehicleSearchCriteria criteria,
        CancellationToken cancellationToken);
}

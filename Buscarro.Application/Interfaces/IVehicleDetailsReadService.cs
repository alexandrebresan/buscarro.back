using Buscarro.Application.DTOs;

namespace Buscarro.Application.Interfaces;

public interface IVehicleDetailsReadService
{
    Task<VehicleDetailsResponse?> GetVehicleDetailsAsync(
        string externalId,
        CancellationToken cancellationToken);
}

using Buscarro.Application.DTOs;

namespace Buscarro.Application.Interfaces;

public interface IVehicleDetailsReadService
{
    Task<VehicleDetailsResponse?> GetVehicleDetailsAsync(
        int externalId,
        CancellationToken cancellationToken);
}

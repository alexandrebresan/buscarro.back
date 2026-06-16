using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class GetVehicleDetailsHandler
    : IRequestHandler<GetVehicleDetailsQuery, VehicleDetailsResponse>
{
    private readonly IVehicleDetailsReadService _vehicleDetailsReadService;

    public GetVehicleDetailsHandler(IVehicleDetailsReadService vehicleDetailsReadService)
    {
        _vehicleDetailsReadService = vehicleDetailsReadService;
    }

    public async Task<VehicleDetailsResponse> Handle(
        GetVehicleDetailsQuery request,
        CancellationToken cancellationToken)
    {
        ValidateExternalId(request.ExternalId);
        var details = await _vehicleDetailsReadService.GetVehicleDetailsAsync(request.ExternalId, cancellationToken);

        return details ?? throw new NotFoundException("Detalhes do anuncio nao encontrados.");
    }

    private static void ValidateExternalId(int externalId)
    {
        if (externalId <= 0)
        {
            throw new ValidationException("ExternalId deve ser maior que zero.");
        }
    }
}

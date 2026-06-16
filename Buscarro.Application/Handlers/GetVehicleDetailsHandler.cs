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
        var externalId = NormalizeRequired(request.ExternalId, "ExternalId");
        var details = await _vehicleDetailsReadService.GetVehicleDetailsAsync(externalId, cancellationToken);

        return details ?? throw new NotFoundException("Detalhes do anuncio nao encontrados.");
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"{fieldName} e obrigatorio.");
        }

        return value.Trim();
    }
}

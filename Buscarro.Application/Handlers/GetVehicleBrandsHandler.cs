using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class GetVehicleBrandsHandler
    : VehicleQueryHandlerBase,
        IRequestHandler<GetVehicleBrandsQuery, IReadOnlyCollection<VehicleBrandResponse>>
{
    public GetVehicleBrandsHandler(IEnumerable<IVehicleProvider> providers) : base(providers)
    {
    }

    public async Task<IReadOnlyCollection<VehicleBrandResponse>> Handle(
        GetVehicleBrandsQuery request,
        CancellationToken cancellationToken)
    {
        var vehicles = await LoadVehiclesAsync(cancellationToken);

        return vehicles
            .GroupBy(vehicle => vehicle.Brand, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First().Brand)
            .OrderBy(brand => brand)
            .Select(brand => new VehicleBrandResponse(brand))
            .ToArray();
    }
}

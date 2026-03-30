using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class GetVehicleYearsHandler
    : VehicleQueryHandlerBase,
        IRequestHandler<GetVehicleYearsQuery, IReadOnlyCollection<VehicleYearResponse>>
{
    public GetVehicleYearsHandler(IEnumerable<IVehicleProvider> providers) : base(providers)
    {
    }

    public async Task<IReadOnlyCollection<VehicleYearResponse>> Handle(
        GetVehicleYearsQuery request,
        CancellationToken cancellationToken)
    {
        var vehicles = await LoadVehiclesAsync(cancellationToken);

        return vehicles
            .Select(vehicle => vehicle.Year)
            .Distinct()
            .OrderByDescending(year => year)
            .Select(year => new VehicleYearResponse(year))
            .ToArray();
    }
}

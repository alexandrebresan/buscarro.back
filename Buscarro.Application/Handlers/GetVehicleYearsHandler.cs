using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class GetVehicleYearsHandler
    : IRequestHandler<GetVehicleYearsQuery, IReadOnlyCollection<VehicleYearResponse>>
{
    private readonly IVehicleCatalogReadService _vehicleCatalogReadService;

    public GetVehicleYearsHandler(IVehicleCatalogReadService vehicleCatalogReadService)
    {
        _vehicleCatalogReadService = vehicleCatalogReadService;
    }

    public async Task<IReadOnlyCollection<VehicleYearResponse>> Handle(
        GetVehicleYearsQuery request,
        CancellationToken cancellationToken)
    {
        var snapshot = await _vehicleCatalogReadService.GetSnapshotAsync(new VehicleSearchCriteria(), cancellationToken);

        return snapshot.Vehicles
            .Select(vehicle => vehicle.Year)
            .Distinct()
            .OrderByDescending(year => year)
            .Select(year => new VehicleYearResponse(year))
            .ToArray();
    }
}

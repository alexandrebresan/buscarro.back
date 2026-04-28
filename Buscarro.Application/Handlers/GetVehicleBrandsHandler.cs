using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class GetVehicleBrandsHandler
    : IRequestHandler<GetVehicleBrandsQuery, IReadOnlyCollection<VehicleBrandResponse>>
{
    private readonly IVehicleCatalogReadService _vehicleCatalogReadService;

    public GetVehicleBrandsHandler(IVehicleCatalogReadService vehicleCatalogReadService)
    {
        _vehicleCatalogReadService = vehicleCatalogReadService;
    }

    public async Task<IReadOnlyCollection<VehicleBrandResponse>> Handle(
        GetVehicleBrandsQuery request,
        CancellationToken cancellationToken)
    {
        var snapshot = await _vehicleCatalogReadService.GetSnapshotAsync(new VehicleSearchCriteria(), cancellationToken);

        return snapshot.Vehicles
            .GroupBy(vehicle => vehicle.Brand, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First().Brand)
            .OrderBy(brand => brand)
            .Select(brand => new VehicleBrandResponse(brand))
            .ToArray();
    }
}

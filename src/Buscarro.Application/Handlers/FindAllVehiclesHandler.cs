using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class FindAllVehiclesHandler
    : IRequestHandler<FindAllVehiclesQuery, FindAllVehiclesResponse>
{
    private readonly IVehicleSearchReadService _vehicleSearchReadService;
    private readonly Services.VehicleCatalogSearchCriteriaNormalizer _normalizer;

    public FindAllVehiclesHandler(IVehicleSearchReadService vehicleSearchReadService, Services.VehicleCatalogSearchCriteriaNormalizer normalizer)
    {
        _vehicleSearchReadService = vehicleSearchReadService;
        _normalizer = normalizer;
    }

    public async Task<FindAllVehiclesResponse> Handle(
        FindAllVehiclesQuery request,
        CancellationToken cancellationToken)
    {
        var criteria = _normalizer.Normalize(request.Criteria);
        var snapshot = await _vehicleSearchReadService.SearchAsync(criteria, cancellationToken);

        return new FindAllVehiclesResponse
        {
            Vehicles = snapshot.Vehicles.ToArray(),
            Providers = snapshot.Providers.ToArray(),
            PublicationId = snapshot.PublicationId,
            CatalogUpdatedAtUtc = snapshot.CatalogUpdatedAtUtc,
            TotalCount = snapshot.TotalCount,
            Page = snapshot.Page,
            PageSize = snapshot.PageSize,
            TotalPages = snapshot.TotalPages,
            Results = snapshot.Vehicles.Count
        };
    }
}

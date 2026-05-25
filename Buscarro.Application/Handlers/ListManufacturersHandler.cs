using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class ListManufacturersHandler
    : IRequestHandler<ListManufacturersQuery, IReadOnlyCollection<ManufacturerCatalogItemResponse>>
{
    private readonly IVehicleProviderCatalogReadService _catalogReadService;

    public ListManufacturersHandler(IVehicleProviderCatalogReadService catalogReadService)
    {
        _catalogReadService = catalogReadService;
    }

    public Task<IReadOnlyCollection<ManufacturerCatalogItemResponse>> Handle(
        ListManufacturersQuery request,
        CancellationToken cancellationToken)
    {
        return _catalogReadService.ListManufacturersAsync(cancellationToken);
    }
}

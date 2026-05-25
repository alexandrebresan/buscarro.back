using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class ListModelsHandler
    : IRequestHandler<ListModelsQuery, IReadOnlyCollection<ModelCatalogItemResponse>>
{
    private readonly IVehicleProviderCatalogReadService _catalogReadService;

    public ListModelsHandler(IVehicleProviderCatalogReadService catalogReadService)
    {
        _catalogReadService = catalogReadService;
    }

    public Task<IReadOnlyCollection<ModelCatalogItemResponse>> Handle(
        ListModelsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ManufacturerId is null || request.ManufacturerId == Guid.Empty)
        {
            throw new ValidationException("manufacturer_id e obrigatorio.");
        }

        return _catalogReadService.ListModelsAsync(request.ManufacturerId.Value, cancellationToken);
    }
}

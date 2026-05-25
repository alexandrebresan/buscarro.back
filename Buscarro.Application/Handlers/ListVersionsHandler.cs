using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class ListVersionsHandler
    : IRequestHandler<ListVersionsQuery, IReadOnlyCollection<VersionCatalogItemResponse>>
{
    private readonly IVehicleProviderCatalogReadService _catalogReadService;

    public ListVersionsHandler(IVehicleProviderCatalogReadService catalogReadService)
    {
        _catalogReadService = catalogReadService;
    }

    public Task<IReadOnlyCollection<VersionCatalogItemResponse>> Handle(
        ListVersionsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ModelId is null || request.ModelId == Guid.Empty)
        {
            throw new ValidationException("model_id e obrigatorio.");
        }

        return _catalogReadService.ListVersionsAsync(request.ModelId.Value, cancellationToken);
    }
}

using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class ListModelsHandler(IGeneralVehicleCatalogReadService catalogReadService)
    : IRequestHandler<ListModelsQuery, IReadOnlyCollection<string>>
{
    public Task<IReadOnlyCollection<string>> Handle(
        ListModelsQuery request,
        CancellationToken cancellationToken)
    {
        return catalogReadService.ListModelsAsync(request.Manufacturer, cancellationToken);
    }
}

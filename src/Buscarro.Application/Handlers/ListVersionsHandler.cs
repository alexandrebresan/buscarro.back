using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class ListVersionsHandler(IGeneralVehicleCatalogReadService catalogReadService)
    : IRequestHandler<ListVersionsQuery, IReadOnlyCollection<string>>
{
    public Task<IReadOnlyCollection<string>> Handle(
        ListVersionsQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            throw new ValidationException("model e obrigatorio.");
        }

        return catalogReadService.ListVersionsAsync(request.Model, request.Manufacturer, cancellationToken);
    }
}

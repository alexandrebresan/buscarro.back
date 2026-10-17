using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class ListFuelsHandler(
    IGeneralVehicleCatalogReadService catalogReadService)
    : IRequestHandler<
        ListFuelsQuery,
        IReadOnlyCollection<string>>
{
    public Task<IReadOnlyCollection<string>> Handle(
        ListFuelsQuery request,
        CancellationToken cancellationToken)
    {
        return catalogReadService.ListFuelsAsync(
            request.Manufacturer,
            request.Model,
            cancellationToken);
    }
}
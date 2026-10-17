using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class ListTransmissionsHandler(
    IGeneralVehicleCatalogReadService catalogReadService)
    : IRequestHandler<
        ListTransmissionsQuery,
        IReadOnlyCollection<string>>
{
    public Task<IReadOnlyCollection<string>> Handle(
        ListTransmissionsQuery request,
        CancellationToken cancellationToken)
    {
        return catalogReadService.ListTransmissionsAsync(
            cancellationToken);
    }
}
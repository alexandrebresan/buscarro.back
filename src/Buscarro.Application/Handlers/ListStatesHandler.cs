using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class ListStatesHandler(
    IGeneralVehicleCatalogReadService catalogReadService)
    : IRequestHandler<ListStatesQuery, IReadOnlyCollection<string>>
{
    public Task<IReadOnlyCollection<string>> Handle(
        ListStatesQuery request,
        CancellationToken cancellationToken)
        => catalogReadService.ListStatesAsync(
            request.Countries,
            cancellationToken);
}
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class ListCitiesHandler(
    IGeneralVehicleCatalogReadService catalogReadService)
    : IRequestHandler<ListCitiesQuery, IReadOnlyCollection<string>>
{
    public Task<IReadOnlyCollection<string>> Handle(
        ListCitiesQuery request,
        CancellationToken cancellationToken)
        => catalogReadService.ListCitiesAsync(
            request.Countries,
            request.States,
            cancellationToken);
}
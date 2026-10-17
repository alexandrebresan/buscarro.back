using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class ListCountriesHandler(
    IGeneralVehicleCatalogReadService catalogReadService)
    : IRequestHandler<ListCountriesQuery, IReadOnlyCollection<string>>
{
    public Task<IReadOnlyCollection<string>> Handle(
        ListCountriesQuery request,
        CancellationToken cancellationToken)
        => catalogReadService.ListCountriesAsync(
            cancellationToken);
}
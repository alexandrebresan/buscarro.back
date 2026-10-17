using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class ListColorsHandler(
    IGeneralVehicleCatalogReadService catalogReadService)
    : IRequestHandler<
        ListColorsQuery,
        IReadOnlyCollection<string>>
{
    public Task<IReadOnlyCollection<string>> Handle(
        ListColorsQuery request,
        CancellationToken cancellationToken)
    {
        return catalogReadService.ListColorsAsync(
            cancellationToken);
    }
}
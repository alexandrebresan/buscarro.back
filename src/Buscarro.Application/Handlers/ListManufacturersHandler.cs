using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class ListManufacturersHandler(IGeneralVehicleCatalogReadService catalogReadService)
    : IRequestHandler<ListManufacturersQuery, IReadOnlyCollection<string>>
{
    public Task<IReadOnlyCollection<string>> Handle(
        ListManufacturersQuery request,
        CancellationToken cancellationToken)
    {
        return catalogReadService.ListManufacturersAsync(cancellationToken);
    }
}

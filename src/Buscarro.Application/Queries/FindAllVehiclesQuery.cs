using Buscarro.Application.DTOs;
using MediatR;

namespace Buscarro.Application.Queries;

public sealed record FindAllVehiclesQuery(VehicleCatalogSearchRequest Criteria) : IRequest<FindAllVehiclesResponse>
{
    public FindAllVehiclesQuery() : this(new VehicleCatalogSearchRequest())
    {
    }
}

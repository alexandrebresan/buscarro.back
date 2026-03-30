using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class FindAllVehiclesHandler
    : VehicleQueryHandlerBase,
        IRequestHandler<FindAllVehiclesQuery, IReadOnlyCollection<VehicleResponse>>
{
    public FindAllVehiclesHandler(IEnumerable<IVehicleProvider> providers) : base(providers)
    {
    }

    public async Task<IReadOnlyCollection<VehicleResponse>> Handle(
        FindAllVehiclesQuery request,
        CancellationToken cancellationToken)
    {
        var vehicles = await LoadVehiclesAsync(cancellationToken);

        return vehicles
            .Select(vehicle => new VehicleResponse
            {
                ExternalId = vehicle.ExternalId,
                Provider = vehicle.Provider,
                Title = vehicle.Title,
                Brand = vehicle.Brand,
                Model = vehicle.Model,
                Version = vehicle.Version,
                Year = vehicle.Year,
                Mileage = vehicle.Mileage,
                Fuel = vehicle.Fuel,
                Transmission = vehicle.Transmission,
                Color = vehicle.Color,
                Price = vehicle.Price,
                City = vehicle.City,
                State = vehicle.State,
                Url = vehicle.Url
            })
            .ToArray();
    }
}

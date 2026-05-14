using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class FindAllVehiclesHandler
    : IRequestHandler<FindAllVehiclesQuery, FindAllVehiclesResponse>
{
    private readonly IVehicleCatalogReadService _vehicleCatalogReadService;

    public FindAllVehiclesHandler(IVehicleCatalogReadService vehicleCatalogReadService)
    {
        _vehicleCatalogReadService = vehicleCatalogReadService;
    }

    public async Task<FindAllVehiclesResponse> Handle(
        FindAllVehiclesQuery request,
        CancellationToken cancellationToken)
    {
        var snapshot = await _vehicleCatalogReadService.GetSnapshotAsync(request.Criteria, cancellationToken);

        return new FindAllVehiclesResponse
        {
            Vehicles = snapshot.Vehicles
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
                    Doors = vehicle.Doors,
                    Armored = vehicle.Armored,
                    Fuel = vehicle.Fuel,
                    Transmission = vehicle.Transmission,
                    Color = vehicle.Color,
                    Price = vehicle.Price,
                    City = vehicle.City,
                    State = vehicle.State,
                    Url = vehicle.Url,
                    ImageUrl = vehicle.ImageUrl
                })
                .ToArray(),
            Providers = snapshot.Providers
                .Select(provider => new ProviderStatusResponse
                {
                    Provider = provider.Provider,
                    Success = provider.Success,
                    FromCache = provider.FromCache,
                    UsedStaleCache = provider.UsedStaleCache,
                    Error = provider.Error,
                    CurrentResults = provider.CurrentResults,
                    TotalResults = provider.TotalResults,
                    CurrentPage = provider.CurrentPage,
                    TotalPages = provider.TotalPages
                })
                .ToArray()
        };
    }
}

using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Contracts;

namespace Buscarro.Infrastructure.Providers;

public sealed class WebMotorsVehicleProvider : IVehicleProvider
{
    public string ProviderName => "WebMotors";

    public async Task<IReadOnlyCollection<Vehicle>> GetVehiclesAsync(CancellationToken cancellationToken)
    {
        var payload = await FetchMockResponseAsync(cancellationToken);

        return payload.SearchResults
            .Select(MapToVehicle)
            .ToArray();
    }

    private static async Task<WebMotorsVehiclesResponseContract> FetchMockResponseAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(55), cancellationToken);

        return new WebMotorsVehiclesResponseContract
        {
            SearchResults =
            [
                new WebMotorsVehicleContract
                {
                    ListingCode = "wm-901",
                    Title = "Jeep Compass Longitude T270",
                    Make = "Jeep",
                    Model = "Compass",
                    Version = "Longitude T270",
                    FabricationYear = 2023,
                    Odometer = 12000,
                    FuelType = "Flex",
                    TransmissionType = "Automatico",
                    ColorName = "Cinza",
                    Price = 165900m,
                    SellerCity = "Belo Horizonte",
                    SellerState = "MG",
                    VehicleUrl = "https://mock.webmotors.com.br/veiculos/wm-901"
                },
                new WebMotorsVehicleContract
                {
                    ListingCode = "wm-902",
                    Title = "Volkswagen Nivus Highline 200 TSI",
                    Make = "Volkswagen",
                    Model = "Nivus",
                    Version = "Highline 200 TSI",
                    FabricationYear = 2022,
                    Odometer = 27000,
                    FuelType = "Flex",
                    TransmissionType = "Automatico",
                    ColorName = "Azul",
                    Price = 119990m,
                    SellerCity = "Curitiba",
                    SellerState = "PR",
                    VehicleUrl = "https://mock.webmotors.com.br/veiculos/wm-902"
                }
            ]
        };
    }

    private Vehicle MapToVehicle(WebMotorsVehicleContract source)
    {
        return new Vehicle
        {
            ExternalId = source.ListingCode,
            Provider = ProviderName,
            Title = source.Title,
            Brand = source.Make,
            Model = source.Model,
            Version = source.Version,
            Year = source.FabricationYear,
            Mileage = source.Odometer,
            Fuel = source.FuelType,
            Transmission = source.TransmissionType,
            Color = source.ColorName,
            Price = source.Price,
            City = source.SellerCity,
            State = source.SellerState,
            Url = source.VehicleUrl
        };
    }
}

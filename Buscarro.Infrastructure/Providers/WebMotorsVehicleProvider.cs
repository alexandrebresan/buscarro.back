using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Contracts;
using System.Text.Json;

namespace Buscarro.Infrastructure.Providers;

public sealed class WebMotorsVehicleProvider : IVehicleProvider
{
    public string ProviderName => "WebMotors";

    public async Task<VehicleProviderResult> GetVehiclesAsync(
        VehicleSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var payload = await FetchMockResponseAsync(cancellationToken);

        var vehicles = payload.SearchResults
            .Select(MapToVehicle)
            .ToArray();

        return VehicleProviderSearch.Apply(vehicles, criteria);
    }

    private static async Task<WebMotorsVehiclesResponseContract> FetchMockResponseAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(55), cancellationToken);

        return JsonSerializer.Deserialize<WebMotorsVehiclesResponseContract>(
                   """
                   {
                     "SearchResults": [
                       {
                         "ID": "wm-901",
                         "name": "Jeep Compass Longitude T270",
                         "Make": "Jeep",
                         "Model": "Compass",
                         "Version": "Longitude T270",
                         "FabricationYear": 2023,
                         "Odometer": 12000,
                         "FuelType": "Flex",
                         "TransmissionType": "Automatico",
                         "ColorName": "Cinza",
                         "Price": 165900,
                         "SellerCity": "Belo Horizonte",
                         "SellerState": "MG",
                         "VehicleUrl": "https://mock.webmotors.com.br/veiculos/wm-901"
                       },
                       {
                         "ID": "wm-902",
                         "name": "Volkswagen Nivus Highline 200 TSI",
                         "Make": "Volkswagen",
                         "Model": "Nivus",
                         "Version": "Highline 200 TSI",
                         "FabricationYear": 2022,
                         "Odometer": 27000,
                         "FuelType": "Flex",
                         "TransmissionType": "Automatico",
                         "ColorName": "Azul",
                         "Price": 119990,
                         "SellerCity": "Curitiba",
                         "SellerState": "PR",
                         "VehicleUrl": "https://mock.webmotors.com.br/veiculos/wm-902"
                       }
                     ]
                   }
                   """) ?? new WebMotorsVehiclesResponseContract();
    }

    private Vehicle MapToVehicle(WebMotorsVehicleContract source)
    {
        return new Vehicle
        {
            ExternalId = source.ListingCode,
            Provider = ProviderName,
            Title = source.Name,
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
            Url = source.VehicleUrl,
            ImageUrl = string.Empty
        };
    }
}

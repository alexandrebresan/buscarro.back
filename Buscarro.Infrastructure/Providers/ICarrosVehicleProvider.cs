using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Contracts;
using System.Text.Json;

namespace Buscarro.Infrastructure.Providers;

public sealed class ICarrosVehicleProvider : IVehicleProvider
{
    public string ProviderName => "iCarros";

    public async Task<VehicleProviderResult> GetVehiclesAsync(
        VehicleSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var payload = await FetchMockResponseAsync(cancellationToken);

        var vehicles = payload.Vehicles
            .Select(MapToVehicle)
            .ToArray();

        return VehicleProviderSearch.Apply(vehicles, criteria);
    }

    private static async Task<ICarrosVehiclesResponseContract> FetchMockResponseAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(65), cancellationToken);

        return JsonSerializer.Deserialize<ICarrosVehiclesResponseContract>(
                   """
                   {
                     "vehicles": [
                       {
                         "ad_id": "ic-3001",
                         "nome_carro": "Chevrolet Onix Premier 1.0 Turbo",
                         "brand_name": "Chevrolet",
                         "model_name": "Onix",
                         "derivative": "Premier 1.0 Turbo",
                         "production_year": 2024,
                         "mileage_value": 8000,
                         "fuel_label": "Flex",
                         "transmission_label": "Automatico",
                         "color_label": "Preto",
                         "price_value": 109900,
                         "location_city": "Florianopolis",
                         "location_state": "SC",
                         "detail_url": "https://mock.icarros.com.br/veiculos/ic-3001"
                       },
                       {
                         "ad_id": "ic-3002",
                         "nome_carro": "Hyundai Creta Platinum 1.0 TGDI",
                         "brand_name": "Hyundai",
                         "model_name": "Creta",
                         "derivative": "Platinum 1.0 TGDI",
                         "production_year": 2023,
                         "mileage_value": 19000,
                         "fuel_label": "Flex",
                         "transmission_label": "Automatico",
                         "color_label": "Cinza",
                         "price_value": 154800,
                         "location_city": "Goiania",
                         "location_state": "GO",
                         "detail_url": "https://mock.icarros.com.br/veiculos/ic-3002"
                       }
                     ]
                   }
                   """) ?? new ICarrosVehiclesResponseContract();
    }

    private Vehicle MapToVehicle(ICarrosVehicleContract source)
    {
        return new Vehicle
        {
            ExternalId = source.AdId,
            Provider = ProviderName,
            Title = source.NomeCarro,
            Brand = source.BrandName,
            Model = source.ModelName,
            Version = source.Derivative,
            Year = source.ProductionYear,
            Mileage = source.MileageValue,
            Fuel = source.FuelLabel,
            Transmission = source.TransmissionLabel,
            Color = source.ColorLabel,
            Price = source.PriceValue,
            City = source.LocationCity,
            State = source.LocationState,
            Url = source.DetailUrl,
            ImageUrl = string.Empty
        };
    }
}

using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Contracts;
using System.Text.Json;

namespace Buscarro.Infrastructure.Providers;

public sealed class OlxVehicleProvider : IVehicleProvider
{
    public string ProviderName => "OLX";

    public async Task<VehicleProviderResult> GetVehiclesAsync(
        VehicleSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var payload = await FetchMockResponseAsync(cancellationToken);

        var vehicles = payload.Listings
            .Select(MapToVehicle)
            .ToArray();

        return VehicleProviderSearch.Apply(vehicles, criteria);
    }

    // Mantem a fronteira da integracao isolada para a troca futura pela API real.
    private static async Task<OlxVehiclesResponseContract> FetchMockResponseAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(40), cancellationToken);

        return JsonSerializer.Deserialize<OlxVehiclesResponseContract>(
                   """
                   {
                     "listings": [
                       {
                         "id": "olx-001",
                         "car_name": "Toyota Corolla XEi 2.0 Flex",
                         "automaker": "Toyota",
                         "model_name": "Corolla",
                         "trim": "XEi 2.0 Flex",
                         "model_year": 2022,
                         "km": 32000,
                         "fuel": "Flex",
                         "gearbox": "Automatico",
                         "paint": "Prata",
                         "price": 129900,
                         "city": "Sao Paulo",
                         "state": "SP",
                         "link": "https://mock.olx.com.br/veiculos/olx-001"
                       },
                       {
                         "id": "olx-002",
                         "car_name": "Honda Civic EXL 2.0",
                         "automaker": "Honda",
                         "model_name": "Civic",
                         "trim": "EXL 2.0",
                         "model_year": 2021,
                         "km": 41000,
                         "fuel": "Flex",
                         "gearbox": "Automatico",
                         "paint": "Branco",
                         "price": 118500,
                         "city": "Campinas",
                         "state": "SP",
                         "link": "https://mock.olx.com.br/veiculos/olx-002"
                       }
                     ]
                   }
                   """) ?? new OlxVehiclesResponseContract();
    }

    private Vehicle MapToVehicle(OlxVehicleContract source)
    {
        return new Vehicle
        {
            ExternalId = source.Id,
            Provider = ProviderName,
            Title = source.CarName,
            Brand = source.Automaker,
            Model = source.ModelName,
            Version = source.Trim,
            Year = source.ModelYear,
            Mileage = source.Kilometers,
            Fuel = source.Fuel,
            Transmission = source.Gearbox,
            Color = source.Paint,
            Price = source.Price,
            City = source.City,
            State = source.State,
            Url = source.Link,
            ImageUrl = string.Empty
        };
    }
}

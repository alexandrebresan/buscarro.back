using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Contracts;

namespace Buscarro.Infrastructure.Providers;

public sealed class OlxVehicleProvider : IVehicleProvider
{
    public string ProviderName => "OLX";

    public async Task<IReadOnlyCollection<Vehicle>> GetVehiclesAsync(CancellationToken cancellationToken)
    {
        var payload = await FetchMockResponseAsync(cancellationToken);

        return payload.Listings
            .Select(MapToVehicle)
            .ToArray();
    }

    // Mantem a fronteira da integracao isolada para a troca futura pela API real.
    private static async Task<OlxVehiclesResponseContract> FetchMockResponseAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(40), cancellationToken);

        return new OlxVehiclesResponseContract
        {
            Listings =
            [
                new OlxVehicleContract
                {
                    Id = "olx-001",
                    Subject = "Toyota Corolla XEi 2.0 Flex",
                    Automaker = "Toyota",
                    ModelName = "Corolla",
                    Trim = "XEi 2.0 Flex",
                    ModelYear = 2022,
                    Kilometers = 32000,
                    Fuel = "Flex",
                    Gearbox = "Automatico",
                    Paint = "Prata",
                    Price = 129900m,
                    City = "Sao Paulo",
                    State = "SP",
                    Link = "https://mock.olx.com.br/veiculos/olx-001"
                },
                new OlxVehicleContract
                {
                    Id = "olx-002",
                    Subject = "Honda Civic EXL 2.0",
                    Automaker = "Honda",
                    ModelName = "Civic",
                    Trim = "EXL 2.0",
                    ModelYear = 2021,
                    Kilometers = 41000,
                    Fuel = "Flex",
                    Gearbox = "Automatico",
                    Paint = "Branco",
                    Price = 118500m,
                    City = "Campinas",
                    State = "SP",
                    Link = "https://mock.olx.com.br/veiculos/olx-002"
                }
            ]
        };
    }

    private Vehicle MapToVehicle(OlxVehicleContract source)
    {
        return new Vehicle
        {
            ExternalId = source.Id,
            Provider = ProviderName,
            Title = source.Subject,
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
            Url = source.Link
        };
    }
}

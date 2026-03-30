using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Contracts;

namespace Buscarro.Infrastructure.Providers;

public sealed class ICarrosVehicleProvider : IVehicleProvider
{
    public string ProviderName => "iCarros";

    public async Task<IReadOnlyCollection<Vehicle>> GetVehiclesAsync(CancellationToken cancellationToken)
    {
        var payload = await FetchMockResponseAsync(cancellationToken);

        return payload.Vehicles
            .Select(MapToVehicle)
            .ToArray();
    }

    private static async Task<ICarrosVehiclesResponseContract> FetchMockResponseAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(65), cancellationToken);

        return new ICarrosVehiclesResponseContract
        {
            Vehicles =
            [
                new ICarrosVehicleContract
                {
                    AdId = "ic-3001",
                    Headline = "Chevrolet Onix Premier 1.0 Turbo",
                    BrandName = "Chevrolet",
                    ModelName = "Onix",
                    Derivative = "Premier 1.0 Turbo",
                    ProductionYear = 2024,
                    MileageValue = 8000,
                    FuelLabel = "Flex",
                    TransmissionLabel = "Automatico",
                    ColorLabel = "Preto",
                    PriceValue = 109900m,
                    LocationCity = "Florianopolis",
                    LocationState = "SC",
                    DetailUrl = "https://mock.icarros.com.br/veiculos/ic-3001"
                },
                new ICarrosVehicleContract
                {
                    AdId = "ic-3002",
                    Headline = "Hyundai Creta Platinum 1.0 TGDI",
                    BrandName = "Hyundai",
                    ModelName = "Creta",
                    Derivative = "Platinum 1.0 TGDI",
                    ProductionYear = 2023,
                    MileageValue = 19000,
                    FuelLabel = "Flex",
                    TransmissionLabel = "Automatico",
                    ColorLabel = "Cinza",
                    PriceValue = 154800m,
                    LocationCity = "Goiania",
                    LocationState = "GO",
                    DetailUrl = "https://mock.icarros.com.br/veiculos/ic-3002"
                }
            ]
        };
    }

    private Vehicle MapToVehicle(ICarrosVehicleContract source)
    {
        return new Vehicle
        {
            ExternalId = source.AdId,
            Provider = ProviderName,
            Title = source.Headline,
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
            Url = source.DetailUrl
        };
    }
}

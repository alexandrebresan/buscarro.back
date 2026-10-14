using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;

namespace Buscarro.Infrastructure.Providers;

public sealed class AutoDevVehicleCatalogItemNormalizer(IExchangeRateProvider exchangeRates) : IVehicleCatalogItemNormalizer
{
    private ExchangeRateQuote? _quote;
    public string Provider => "AutoDev";

    public async Task PrepareAsync(CancellationToken cancellationToken)
        => _quote = await exchangeRates.GetUsdToBrlQuoteAsync(cancellationToken);

    public CatalogItemNormalizationResult Normalize(CatalogSourceVehicle source)
    {
        if (_quote is null) throw new InvalidOperationException("AutoDev exchange rate has not been prepared.");
        if (string.IsNullOrWhiteSpace(source.ExternalId)) return CatalogItemNormalizationResult.Invalid("autodev_vin_missing");
        if (string.IsNullOrWhiteSpace(source.Url)) return CatalogItemNormalizationResult.Invalid("autodev_listing_url_missing");
        if (string.IsNullOrWhiteSpace(source.ImageUrl)) return CatalogItemNormalizationResult.Invalid("autodev_image_url_missing");
        if (source.Price is not > 0) return CatalogItemNormalizationResult.Invalid("autodev_price_invalid");
        int? mileage = null;
        if (source.Mileage is int miles)
        {
            var kilometers = decimal.Round(miles * 1.609344m, 0, MidpointRounding.AwayFromZero);
            if (kilometers is < 0 or > int.MaxValue)
                return CatalogItemNormalizationResult.Invalid("autodev_mileage_invalid");

            mileage = decimal.ToInt32(kilometers);
        }

        var title = string.Join(" ", new[] { source.Year?.ToString(System.Globalization.CultureInfo.InvariantCulture), source.Brand, source.Model, source.Version }
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));
        return CatalogItemNormalizationResult.Valid(source with
        {
            Title = title,
            Mileage = mileage,
            Price = decimal.Round(source.Price.Value * _quote.UsdToBrlRate, 2, MidpointRounding.AwayFromZero),
            Fuel = AutoDevVehicleAttributeTranslator.TranslateFuel(source.Fuel),
            Transmission = AutoDevVehicleAttributeTranslator.TranslateTransmission(source.Transmission),
            Color = AutoDevVehicleAttributeTranslator.TranslateColor(source.Color)
        });
    }
}

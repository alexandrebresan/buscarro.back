using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Infrastructure.Providers;

namespace Buscarro.Tests;

public sealed class AutoDevVehicleCatalogItemNormalizerTests
{
    [Fact]
    public async Task Normalizes_title_once_prepared_quote_and_rounds_brl()
    {
        var quotes = new QuoteProvider(5.555m);
        var normalizer = new AutoDevVehicleCatalogItemNormalizer(quotes);
        await normalizer.PrepareAsync(TestContext.Current.CancellationToken);
        var result = normalizer.Normalize(Vehicle() with { Price = 1.001m });
        Assert.True(result.IsValid); Assert.Equal("2024 Jeep Grand Cherokee 4xe", result.Vehicle!.Title); Assert.Equal(5.56m, result.Vehicle.Price); Assert.Equal(1, quotes.Calls);
    }

    [Theory]
    [InlineData(null, "autodev_vin_missing")]
    [InlineData("", "autodev_vin_missing")]
    public async Task Requires_vin(string? vin, string reason)
    {
        var normalizer = new AutoDevVehicleCatalogItemNormalizer(new QuoteProvider(1));
        await normalizer.PrepareAsync(TestContext.Current.CancellationToken);
        Assert.Equal(reason, normalizer.Normalize(Vehicle() with { ExternalId = vin }).Reason);
    }

    [Theory]
    [InlineData(true, "autodev_listing_url_missing")]
    [InlineData(false, "autodev_image_url_missing")]
    public async Task Requires_listing_and_image_presence(bool listing, string reason)
    {
        var normalizer = new AutoDevVehicleCatalogItemNormalizer(new QuoteProvider(1));
        await normalizer.PrepareAsync(TestContext.Current.CancellationToken);
        var source = listing ? Vehicle() with { Url = " " } : Vehicle() with { ImageUrl = " " };
        Assert.Equal(reason, normalizer.Normalize(source).Reason);
    }

    [Fact]
    public async Task Omits_blank_title_components_without_inventing_values()
    {
        var normalizer = new AutoDevVehicleCatalogItemNormalizer(new QuoteProvider(1));
        await normalizer.PrepareAsync(TestContext.Current.CancellationToken);
        var result = normalizer.Normalize(Vehicle() with { Year = null, Brand = " Jeep ", Model = " ", Version = null });
        Assert.Equal("Jeep", result.Vehicle!.Title);
    }

    [Fact]
    public async Task Translates_provider_vehicle_attributes_without_changing_title_or_price()
    {
        var normalizer = new AutoDevVehicleCatalogItemNormalizer(new QuoteProvider(2));
        await normalizer.PrepareAsync(TestContext.Current.CancellationToken);

        var result = normalizer.Normalize(Vehicle() with { Fuel = "plug-in hybrid", Transmission = "Automatic CVT", Color = "Blueprint Red" });

        Assert.Equal("2024 Jeep Grand Cherokee 4xe", result.Vehicle!.Title);
        Assert.Equal(2m, result.Vehicle.Price);
        Assert.Equal("Elétrico e Gasolina", result.Vehicle.Fuel);
        Assert.Equal("Automático CVT", result.Vehicle.Transmission);
        Assert.Equal("Azul", result.Vehicle.Color);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    [InlineData(50_000, 80_467)]
    public async Task Converts_provider_miles_to_kilometers_without_mutating_source(int? miles, int? expectedKilometers)
    {
        var normalizer = new AutoDevVehicleCatalogItemNormalizer(new QuoteProvider(1));
        await normalizer.PrepareAsync(TestContext.Current.CancellationToken);
        var source = Vehicle() with { Mileage = miles };

        var result = normalizer.Normalize(source);

        Assert.True(result.IsValid);
        Assert.Equal(expectedKilometers, result.Vehicle!.Mileage);
        Assert.Equal(miles, source.Mileage);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task Rejects_mileage_that_cannot_be_converted_safely(int miles)
    {
        var normalizer = new AutoDevVehicleCatalogItemNormalizer(new QuoteProvider(1));
        await normalizer.PrepareAsync(TestContext.Current.CancellationToken);

        var result = normalizer.Normalize(Vehicle() with { Mileage = miles });

        Assert.False(result.IsValid);
        Assert.Null(result.Vehicle);
        Assert.Equal("autodev_mileage_invalid", result.Reason);
    }

    private static CatalogSourceVehicle Vehicle() => new("vin", null, "Jeep", "Grand Cherokee", "4xe", null, null, null, 2024, null, null, null, null, null, null, 1, null, null, "not-url", "not-image");
    private sealed class QuoteProvider(decimal rate) : IExchangeRateProvider
    { public int Calls { get; private set; } public Task<ExchangeRateQuote> GetUsdToBrlQuoteAsync(CancellationToken cancellationToken) { Calls++; return Task.FromResult(new ExchangeRateQuote(rate, new DateOnly(2026, 8, 22))); } }
}

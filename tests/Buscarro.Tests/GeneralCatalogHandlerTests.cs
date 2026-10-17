using Buscarro.Application.Exceptions;
using Buscarro.Application.Handlers;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;

namespace Buscarro.Tests;

public sealed class GeneralCatalogHandlerTests
{
    [Fact]
    public async Task Manufacturers_returns_names_and_preserves_cancellation()
    {
        var service = new FakeGeneralCatalogReadService();

        using var cancellation =
            new CancellationTokenSource();

        var result =
            await new ListManufacturersHandler(service)
                .Handle(
                    new(),
                    cancellation.Token);

        Assert.Equal(["Jeep"], result);
        Assert.Equal(
            cancellation.Token,
            service.Token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Jeep")]
    public async Task Models_accepts_optional_manufacturer_name(
        string? manufacturer)
    {
        var service =
            new FakeGeneralCatalogReadService();

        using var cancellation =
            new CancellationTokenSource();

        var result =
            await new ListModelsHandler(service)
                .Handle(
                    new(manufacturer),
                    cancellation.Token);

        Assert.Equal(["Wrangler"], result);
        Assert.Equal(
            manufacturer,
            service.Manufacturer);

        Assert.Equal(
            cancellation.Token,
            service.Token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Versions_requires_non_blank_model(
        string? model)
    {
        var service =
            new FakeGeneralCatalogReadService();

        var exception =
            await Assert.ThrowsAsync<
                ValidationException>(
                () =>
                    new ListVersionsHandler(service)
                        .Handle(
                            new(model),
                            CancellationToken.None));

        Assert.Equal(
            "model e obrigatorio.",
            exception.Message);

        Assert.False(service.Called);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Jeep")]
    public async Task Versions_passes_model_and_optional_manufacturer(
        string? manufacturer)
    {
        var service =
            new FakeGeneralCatalogReadService();

        using var cancellation =
            new CancellationTokenSource();

        var result =
            await new ListVersionsHandler(service)
                .Handle(
                    new(
                        "Wrangler",
                        manufacturer),
                    cancellation.Token);

        Assert.Equal(["Rubicon"], result);
        Assert.Equal(
            "Wrangler",
            service.Model);

        Assert.Equal(
            manufacturer,
            service.Manufacturer);

        Assert.Equal(
            cancellation.Token,
            service.Token);
    }

    [Fact]
    public async Task Fuels_returns_global_catalog_values()
    {
        var service =
            new FakeGeneralCatalogReadService();

        using var cancellation =
            new CancellationTokenSource();

        var result =
            await new ListFuelsHandler(service)
                .Handle(
                    new ListFuelsQuery(),
                    cancellation.Token);

        Assert.Equal(
            ["Gasolina", "Flex"],
            result);

        Assert.Equal(
            cancellation.Token,
            service.Token);
    }

    [Fact]
    public async Task Transmissions_returns_global_catalog_values()
    {
        var service =
            new FakeGeneralCatalogReadService();

        using var cancellation =
            new CancellationTokenSource();

        var result =
            await new ListTransmissionsHandler(service)
                .Handle(
                    new ListTransmissionsQuery(),
                    cancellation.Token);

        Assert.Equal(
            ["Automático", "Manual"],
            result);

        Assert.Equal(
            cancellation.Token,
            service.Token);
    }

    [Fact]
    public async Task Colors_returns_global_catalog_values()
    {
        var service =
            new FakeGeneralCatalogReadService();

        using var cancellation =
            new CancellationTokenSource();

        var result =
            await new ListColorsHandler(service)
                .Handle(
                    new ListColorsQuery(),
                    cancellation.Token);

        Assert.Equal(
            ["Preto", "Branco"],
            result);

        Assert.Equal(
            cancellation.Token,
            service.Token);
    }

    private sealed class FakeGeneralCatalogReadService
        : IGeneralVehicleCatalogReadService
    {
        public string? Manufacturer
        {
            get;
            private set;
        }

        public string? Model
        {
            get;
            private set;
        }

        public CancellationToken Token
        {
            get;
            private set;
        }

        public bool Called
        {
            get;
            private set;
        }

        public Task<IReadOnlyCollection<string>>
            ListManufacturersAsync(
                CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            Called = true;

            return Task.FromResult<
                IReadOnlyCollection<string>>(
                ["Jeep"]);
        }

        public Task<IReadOnlyCollection<string>>
            ListModelsAsync(
                string? manufacturer,
                CancellationToken cancellationToken)
        {
            Manufacturer = manufacturer;
            Token = cancellationToken;
            Called = true;

            return Task.FromResult<
                IReadOnlyCollection<string>>(
                ["Wrangler"]);
        }

        public Task<IReadOnlyCollection<string>>
            ListVersionsAsync(
                string model,
                string? manufacturer,
                CancellationToken cancellationToken)
        {
            Model = model;
            Manufacturer = manufacturer;
            Token = cancellationToken;
            Called = true;

            return Task.FromResult<
                IReadOnlyCollection<string>>(
                ["Rubicon"]);
        }

        public Task<IReadOnlyCollection<string>>
            ListFuelsAsync(
                CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            Called = true;

            return Task.FromResult<
                IReadOnlyCollection<string>>(
                ["Gasolina", "Flex"]);
        }

        public Task<IReadOnlyCollection<string>>
            ListTransmissionsAsync(
                CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            Called = true;

            return Task.FromResult<
                IReadOnlyCollection<string>>(
                ["Automático", "Manual"]);
        }

        public Task<IReadOnlyCollection<string>>
            ListColorsAsync(
                CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            Called = true;

            return Task.FromResult<
                IReadOnlyCollection<string>>(
                ["Preto", "Branco"]);
        }
    }
}
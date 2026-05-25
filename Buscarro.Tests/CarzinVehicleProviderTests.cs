using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Infrastructure.Providers;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace Buscarro.Tests;

public sealed class CarzinVehicleProviderTests
{
    [Fact]
    public void VehicleSearchCriteria_exposes_only_supported_get_vehicles_filters()
    {
        var propertyNames = typeof(VehicleSearchCriteria)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain("CitySlug", propertyNames);
        Assert.DoesNotContain("Seller", propertyNames);
        Assert.DoesNotContain("SellerType", propertyNames);
        Assert.DoesNotContain("Doors", propertyNames);
        Assert.DoesNotContain("Armored", propertyNames);
        Assert.DoesNotContain("IncludeInactive", propertyNames);
    }

    [Fact]
    public async Task GetVehiclesAsync_forwards_carzin_search_contract()
    {
        var handler = new CaptureFormHandler(EmptySearchJson);
        var provider = new CarzinVehicleProvider(
            new HttpClient(handler) { BaseAddress = new Uri("https://carzin.test") },
            Options.Create(new CarzinOptions { BaseUrl = "https://carzin.test", Token = "token" }),
            new FakeCatalogReadService(new VehicleProviderCatalogNames(null, null, null)));

        await provider.GetVehiclesAsync(
            new VehicleSearchCriteria
            {
                Order = "price",
                Results = 20,
                Page = 2,
                Query = "civic",
                State = "SP",
                City = 123,
                Manufacturer = 10,
                Model = 20,
                Version = 30,
                PriceMin = 10000,
                PriceMax = 30000,
                YearMin = 2018,
                YearMax = 2024,
                Color = 5,
                Gear = 1,
                Fuel = 3
            },
            CancellationToken.None);

        Assert.Contains("name=type", handler.FormBody);
        Assert.Contains("1", handler.FormBody);
        Assert.Contains("name=order", handler.FormBody);
        Assert.Contains("name=results", handler.FormBody);
        Assert.Contains("name=page", handler.FormBody);
        Assert.Contains("name=query", handler.FormBody);
        Assert.Contains("name=state", handler.FormBody);
        Assert.Contains("name=city", handler.FormBody);
        Assert.Contains("name=manufacturer", handler.FormBody);
        Assert.Contains("name=model", handler.FormBody);
        Assert.Contains("name=version", handler.FormBody);
        Assert.Contains("name=price_min", handler.FormBody);
        Assert.Contains("name=price_max", handler.FormBody);
        Assert.Contains("name=year_min", handler.FormBody);
        Assert.Contains("name=year_max", handler.FormBody);
        Assert.Contains("name=color", handler.FormBody);
        Assert.Contains("name=gear", handler.FormBody);
        Assert.Contains("name=fuel", handler.FormBody);
        Assert.DoesNotContain("name=city_slug", handler.FormBody);
        Assert.DoesNotContain("name=seller", handler.FormBody);
        Assert.DoesNotContain("name=seller_type", handler.FormBody);
        Assert.DoesNotContain("name=doors", handler.FormBody);
        Assert.DoesNotContain("name=armored", handler.FormBody);
        Assert.DoesNotContain("name=include_inactive", handler.FormBody);
    }

    [Fact]
    public async Task GetVehiclesAsync_uses_catalog_names_when_provider_returns_known_codes()
    {
        const string json = """
        {
          "current_results": 1,
          "total_results": 1,
          "current_page": 1,
          "total_pages": 1,
          "results": [
            {
              "id": 99,
              "title": "Texto original",
              "manufacturer": "Marca antiga",
              "model": "Modelo antigo",
              "version": "Versao antiga",
              "manufacturer_id": 10,
              "model_id": 20,
              "version_id": 30,
              "year_manufacture": 2021,
              "year_model": 2022,
              "price": 100000,
              "mileage": 15000,
              "doors": 4,
              "armored": 1,
              "color": "Branco",
              "fuel": "Flex",
              "gear": "Automatico",
              "city": "Sao Paulo",
              "state": "SP",
              "image": "https://example.com/image.jpg",
              "url": "https://example.com/ad"
            }
          ]
        }
        """;
        var provider = new CarzinVehicleProvider(
            new HttpClient(new StaticJsonHandler(json)) { BaseAddress = new Uri("https://carzin.test") },
            Options.Create(new CarzinOptions { BaseUrl = "https://carzin.test", Token = "token" }),
            new FakeCatalogReadService(new VehicleProviderCatalogNames("Honda", "Civic", "Touring")));

        var result = await provider.GetVehiclesAsync(new VehicleSearchCriteria(), CancellationToken.None);

        var vehicle = Assert.Single(result.Vehicles);
        Assert.Equal("Honda", vehicle.Brand);
        Assert.Equal("Civic", vehicle.Model);
        Assert.Equal("Touring", vehicle.Version);
        Assert.Equal(4, vehicle.Doors);
        Assert.True(vehicle.Armored);
    }

    [Fact]
    public async Task GetVehiclesAsync_falls_back_to_carzin_text_when_catalog_does_not_have_codes()
    {
        const string json = """
        {
          "current_results": 1,
          "total_results": 1,
          "current_page": 1,
          "total_pages": 1,
          "results": [
            {
              "id": 99,
              "title": "Texto original",
              "manufacturer": "Marca Carzin",
              "model": "Modelo Carzin",
              "version": "Versao Carzin",
              "manufacturer_id": 10,
              "model_id": 20,
              "version_id": 30,
              "year_manufacture": 2021,
              "year_model": 2022,
              "price": 100000,
              "mileage": 15000,
              "doors": 4,
              "armored": 0,
              "color": "Branco",
              "fuel": "Flex",
              "gear": "Automatico",
              "city": "Sao Paulo",
              "state": "SP",
              "image": "https://example.com/image.jpg",
              "url": "https://example.com/ad"
            }
          ]
        }
        """;
        var provider = new CarzinVehicleProvider(
            new HttpClient(new StaticJsonHandler(json)) { BaseAddress = new Uri("https://carzin.test") },
            Options.Create(new CarzinOptions { BaseUrl = "https://carzin.test", Token = "token" }),
            new FakeCatalogReadService(new VehicleProviderCatalogNames(null, null, null)));

        var result = await provider.GetVehiclesAsync(new VehicleSearchCriteria(), CancellationToken.None);

        var vehicle = Assert.Single(result.Vehicles);
        Assert.Equal("Marca Carzin", vehicle.Brand);
        Assert.Equal("Modelo Carzin", vehicle.Model);
        Assert.Equal("Versao Carzin", vehicle.Version);
        Assert.Equal(4, vehicle.Doors);
        Assert.False(vehicle.Armored);
    }

    [Fact]
    public async Task GetVehiclesAsync_falls_back_to_carzin_text_when_catalog_ids_are_missing()
    {
        const string json = """
        {
          "current_results": 1,
          "total_results": 1,
          "current_page": 1,
          "total_pages": 1,
          "results": [
            {
              "id": 99,
              "title": "Texto original",
              "manufacturer": "Fiat",
              "model": "Palio",
              "version": "1.0 MPI",
              "manufacturer_id": null,
              "model_id": null,
              "version_id": null,
              "year_manufacture": 2015,
              "year_model": 2016,
              "price": 35000,
              "mileage": 80000,
              "doors": 4,
              "armored": 0,
              "color": "Prata",
              "fuel": "Flex",
              "gear": "Manual",
              "city": "Sao Paulo",
              "state": "SP",
              "image": "https://example.com/image.jpg",
              "url": "https://example.com/ad"
            }
          ]
        }
        """;
        var provider = new CarzinVehicleProvider(
            new HttpClient(new StaticJsonHandler(json)) { BaseAddress = new Uri("https://carzin.test") },
            Options.Create(new CarzinOptions { BaseUrl = "https://carzin.test", Token = "token" }),
            new ThrowingCatalogReadService());

        var result = await provider.GetVehiclesAsync(new VehicleSearchCriteria { Query = "palio" }, CancellationToken.None);

        var vehicle = Assert.Single(result.Vehicles);
        Assert.Equal("Fiat", vehicle.Brand);
        Assert.Equal("Palio", vehicle.Model);
        Assert.Equal("1.0 MPI", vehicle.Version);
    }

    [Fact]
    public async Task GetVehiclesAsync_tolerates_loose_numeric_values_from_carzin()
    {
        const string json = """
        {
          "current_results": "1",
          "total_results": "1",
          "current_page": "1",
          "total_pages": "1",
          "results": [
            {
              "id": "99",
              "title": "Texto original",
              "manufacturer": "Volkswagen",
              "model": "Nivus",
              "version": "Comfortline",
              "manufacturer_id": "",
              "model_id": null,
              "version_id": "0",
              "year_manufacture": null,
              "year_model": "2023",
              "price": "98990.50",
              "mileage": "",
              "doors": "",
              "armored": "",
              "color": null,
              "fuel": "Flex",
              "gear": "Automatico",
              "city": "Sao Paulo",
              "state": "SP",
              "image": null,
              "url": "https://example.com/ad"
            }
          ]
        }
        """;
        var provider = new CarzinVehicleProvider(
            new HttpClient(new StaticJsonHandler(json)) { BaseAddress = new Uri("https://carzin.test") },
            Options.Create(new CarzinOptions { BaseUrl = "https://carzin.test", Token = "token" }),
            new ThrowingCatalogReadService());

        var result = await provider.GetVehiclesAsync(new VehicleSearchCriteria { Fuel = 2 }, CancellationToken.None);

        Assert.Equal(1, result.CurrentResults);
        var vehicle = Assert.Single(result.Vehicles);
        Assert.Equal("99", vehicle.ExternalId);
        Assert.Equal("Volkswagen", vehicle.Brand);
        Assert.Equal("Nivus", vehicle.Model);
        Assert.Equal("Comfortline", vehicle.Version);
        Assert.Equal(2023, vehicle.Year);
        Assert.Equal(98990.50m, vehicle.Price);
        Assert.Equal(0, vehicle.Mileage);
        Assert.Null(vehicle.Doors);
        Assert.Null(vehicle.Armored);
        Assert.Equal(string.Empty, vehicle.Color);
        Assert.Equal(string.Empty, vehicle.ImageUrl);
    }

    [Fact]
    public async Task GetVehiclesAsync_falls_back_to_carzin_text_when_catalog_lookup_fails()
    {
        const string json = """
        {
          "current_results": 1,
          "total_results": 1,
          "current_page": 1,
          "total_pages": 1,
          "results": [
            {
              "id": 99,
              "title": "Texto original",
              "manufacturer": "Marca Carzin",
              "model": "Modelo Carzin",
              "version": "Versao Carzin",
              "manufacturer_id": 10,
              "model_id": 20,
              "version_id": 30,
              "year_manufacture": 2021,
              "year_model": 2022,
              "price": 100000,
              "mileage": 15000,
              "doors": 4,
              "armored": 0,
              "color": "Branco",
              "fuel": "Flex",
              "gear": "Automatico",
              "city": "Sao Paulo",
              "state": "SP",
              "image": "https://example.com/image.jpg",
              "url": "https://example.com/ad"
            }
          ]
        }
        """;
        var provider = new CarzinVehicleProvider(
            new HttpClient(new StaticJsonHandler(json)) { BaseAddress = new Uri("https://carzin.test") },
            Options.Create(new CarzinOptions { BaseUrl = "https://carzin.test", Token = "token" }),
            new ThrowingCatalogReadService());

        var result = await provider.GetVehiclesAsync(new VehicleSearchCriteria(), CancellationToken.None);

        var vehicle = Assert.Single(result.Vehicles);
        Assert.Equal("Marca Carzin", vehicle.Brand);
        Assert.Equal("Modelo Carzin", vehicle.Model);
        Assert.Equal("Versao Carzin", vehicle.Version);
    }

    private sealed class FakeCatalogReadService : IVehicleProviderCatalogReadService
    {
        private readonly VehicleProviderCatalogNames _names;

        public FakeCatalogReadService(VehicleProviderCatalogNames names)
        {
            _names = names;
        }

        public Task<VehicleProviderCatalogNames> ResolveNamesAsync(
            string providerName,
            int vehicleType,
            string manufacturerCode,
            string modelCode,
            string versionCode,
            CancellationToken cancellationToken)
        {
            Assert.Equal("Carzin", providerName);
            Assert.Equal("10", manufacturerCode);
            Assert.Equal("20", modelCode);
            Assert.Equal("30", versionCode);

            return Task.FromResult(_names);
        }

        public Task<IReadOnlyCollection<ManufacturerCatalogItemResponse>> ListManufacturersAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<ManufacturerCatalogItemResponse>>([]);
        }

        public Task<IReadOnlyCollection<ModelCatalogItemResponse>> ListModelsAsync(
            Guid manufacturerId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<ModelCatalogItemResponse>>([]);
        }

        public Task<IReadOnlyCollection<VersionCatalogItemResponse>> ListVersionsAsync(
            Guid modelId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<VersionCatalogItemResponse>>([]);
        }
    }

    private sealed class ThrowingCatalogReadService : IVehicleProviderCatalogReadService
    {
        public Task<VehicleProviderCatalogNames> ResolveNamesAsync(
            string providerName,
            int vehicleType,
            string manufacturerCode,
            string modelCode,
            string versionCode,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Catalogo indisponivel.");
        }

        public Task<IReadOnlyCollection<ManufacturerCatalogItemResponse>> ListManufacturersAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<ManufacturerCatalogItemResponse>>([]);
        }

        public Task<IReadOnlyCollection<ModelCatalogItemResponse>> ListModelsAsync(
            Guid manufacturerId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<ModelCatalogItemResponse>>([]);
        }

        public Task<IReadOnlyCollection<VersionCatalogItemResponse>> ListVersionsAsync(
            Guid modelId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<VersionCatalogItemResponse>>([]);
        }
    }

    private sealed class StaticJsonHandler : HttpMessageHandler
    {
        private readonly string _json;

        public StaticJsonHandler(string json)
        {
            _json = json;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class CaptureFormHandler : HttpMessageHandler
    {
        private readonly string _json;

        public CaptureFormHandler(string json)
        {
            _json = json;
        }

        public string FormBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            FormBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            };
        }
    }

    private const string EmptySearchJson = """
    {
      "current_results": 0,
      "total_results": 0,
      "current_page": 1,
      "total_pages": 0,
      "results": []
    }
    """;
}

using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Contracts;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Buscarro.Infrastructure.Providers;

public sealed class CarzinVehicleProvider : IVehicleProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly CarzinOptions _options;

    public CarzinVehicleProvider(HttpClient httpClient, IOptions<CarzinOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        if (_httpClient.BaseAddress is null && Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            _httpClient.BaseAddress = baseUri;
        }
    }

    public string ProviderName => "Carzin";

    public async Task<VehicleProviderResult> GetVehiclesAsync(
        VehicleSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            throw new InvalidOperationException("Token da Carzin nao configurado.");
        }

        var type = criteria.Type <= 0 ? 1 : criteria.Type;
        var resolved = await ResolveFiltersAsync(type, criteria, cancellationToken);

        if (resolved.ShouldReturnEmpty)
        {
            return EmptyResult(criteria);
        }

        var fields = BuildSearchFields(type, criteria, resolved);
        var payload = await PostFormAsync<CarzinClassifiedSearchResponseContract>(
            "/api/classified-search",
            fields,
            cancellationToken);

        return new VehicleProviderResult
        {
            Vehicles = payload.Results
                .Select(result => MapToVehicle(result, resolved))
                .ToArray(),
            CurrentResults = payload.CurrentResults,
            TotalResults = payload.TotalResults,
            CurrentPage = payload.CurrentPage,
            TotalPages = payload.TotalPages
        };
    }

    private async Task<ResolvedCarzinFilters> ResolveFiltersAsync(
        int type,
        VehicleSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var resolved = new ResolvedCarzinFilters();

        if (!string.IsNullOrWhiteSpace(criteria.Brand))
        {
            var manufacturers = await PostFormAsync<CarzinManufacturersResponseContract>(
                "/api/classified-manufacturers",
                new Dictionary<string, string>
                {
                    ["type"] = type.ToString(CultureInfo.InvariantCulture)
                },
                cancellationToken);

            var manufacturer = FindByName(manufacturers.Manufacturers, criteria.Brand, item => item.Name);
            if (manufacturer is null)
            {
                return resolved with { ShouldReturnEmpty = true };
            }

            resolved = resolved with
            {
                ManufacturerId = manufacturer.Id,
                ManufacturerName = manufacturer.Name
            };
        }

        if (!string.IsNullOrWhiteSpace(criteria.Model))
        {
            if (resolved.ManufacturerId is null)
            {
                return resolved with { ShouldReturnEmpty = true };
            }

            var models = await PostFormAsync<CarzinModelsResponseContract>(
                "/api/classified-models",
                new Dictionary<string, string>
                {
                    ["type"] = type.ToString(CultureInfo.InvariantCulture),
                    ["manufacturer"] = resolved.ManufacturerId.Value.ToString(CultureInfo.InvariantCulture)
                },
                cancellationToken);

            var model = FindByName(models.Models, criteria.Model, item => item.Name);
            if (model is null)
            {
                return resolved with { ShouldReturnEmpty = true };
            }

            resolved = resolved with
            {
                ModelId = model.Id,
                ModelName = model.Name
            };
        }

        if (!string.IsNullOrWhiteSpace(criteria.Version))
        {
            if (resolved.ModelId is null)
            {
                return resolved with { ShouldReturnEmpty = true };
            }

            var versions = await PostFormAsync<CarzinVersionsResponseContract>(
                "/api/classified-versions",
                new Dictionary<string, string>
                {
                    ["type"] = type.ToString(CultureInfo.InvariantCulture),
                    ["model"] = resolved.ModelId.Value.ToString(CultureInfo.InvariantCulture)
                },
                cancellationToken);

            var version = FindByName(versions.Versions, criteria.Version, item => item.Name);
            if (version is null)
            {
                return resolved with { ShouldReturnEmpty = true };
            }

            resolved = resolved with
            {
                VersionId = version.Id,
                VersionName = version.Name
            };
        }

        return resolved;
    }

    private Dictionary<string, string> BuildSearchFields(
        int type,
        VehicleSearchCriteria criteria,
        ResolvedCarzinFilters resolved)
    {
        var fields = new Dictionary<string, string>
        {
            ["type"] = type.ToString(CultureInfo.InvariantCulture),
            ["page"] = Math.Max(1, criteria.Page).ToString(CultureInfo.InvariantCulture),
            ["results"] = Math.Max(1, criteria.Results).ToString(CultureInfo.InvariantCulture)
        };

        AddIfPresent(fields, "order", criteria.Order);
        AddIfPresent(fields, "color", criteria.Color);
        AddIfPresent(fields, "fuel", criteria.Fuel);
        AddIfPresent(fields, "gear", criteria.Transmission);
        AddIfPresent(fields, "state", criteria.State);
        AddIfPresent(fields, "city", criteria.City);
        AddIfPresent(fields, "price_min", criteria.PriceMin);
        AddIfPresent(fields, "price_max", criteria.PriceMax);
        AddIfPresent(fields, "year_min", criteria.YearMin);
        AddIfPresent(fields, "year_max", criteria.YearMax);
        AddIfPresent(fields, "seller_type", criteria.SellerType);

        if (resolved.ManufacturerId is not null)
        {
            fields["manufacturer"] = resolved.ManufacturerId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (resolved.ModelId is not null)
        {
            fields["model"] = resolved.ModelId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (resolved.VersionId is not null)
        {
            fields["version"] = resolved.VersionId.Value.ToString(CultureInfo.InvariantCulture);
        }

        return fields;
    }

    private async Task<T> PostFormAsync<T>(
        string path,
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken)
        where T : new()
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(_options.Token), "token");

        foreach (var field in fields)
        {
            content.Add(new StringContent(field.Value), field.Key);
        }

        using var response = await _httpClient.PostAsync(path, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken)
            ?? new T();
    }

    private Vehicle MapToVehicle(
        CarzinClassifiedContract source,
        ResolvedCarzinFilters resolved)
    {
        return new Vehicle
        {
            ExternalId = source.Id.ToString(CultureInfo.InvariantCulture),
            Provider = ProviderName,
            Title = source.Title,
            Brand = resolved.ManufacturerName ?? string.Empty,
            Model = resolved.ModelName ?? string.Empty,
            Version = resolved.VersionName ?? string.Empty,
            Year = source.YearModel > 0 ? source.YearModel : source.YearManufacture,
            Mileage = source.Mileage,
            Fuel = source.Fuel,
            Transmission = source.Gear,
            Color = source.Color,
            Price = source.Price,
            City = WebUtility.HtmlDecode(source.City),
            State = source.State,
            Url = source.Url,
            ImageUrl = source.Image
        };
    }

    private static VehicleProviderResult EmptyResult(VehicleSearchCriteria criteria)
    {
        return new VehicleProviderResult
        {
            Vehicles = [],
            CurrentResults = 0,
            TotalResults = 0,
            CurrentPage = Math.Max(1, criteria.Page),
            TotalPages = 0
        };
    }

    private static T? FindByName<T>(
        IEnumerable<T> items,
        string name,
        Func<T, string> getName)
    {
        return items.FirstOrDefault(item =>
            string.Equals(getName(item), name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static void AddIfPresent(
        IDictionary<string, string> fields,
        string name,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields[name] = value.Trim();
        }
    }

    private static void AddIfPresent(
        IDictionary<string, string> fields,
        string name,
        decimal? value)
    {
        if (value is not null)
        {
            fields[name] = value.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static void AddIfPresent(
        IDictionary<string, string> fields,
        string name,
        int? value)
    {
        if (value is not null)
        {
            fields[name] = value.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private sealed record ResolvedCarzinFilters(
        bool ShouldReturnEmpty = false,
        int? ManufacturerId = null,
        string? ManufacturerName = null,
        int? ModelId = null,
        string? ModelName = null,
        int? VersionId = null,
        string? VersionName = null);
}

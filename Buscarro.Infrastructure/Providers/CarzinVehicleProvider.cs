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
        var fields = BuildSearchFields(type, criteria);
        var payload = await PostFormAsync<CarzinClassifiedSearchResponseContract>(
            "/api/classified-search",
            fields,
            cancellationToken);

        return new VehicleProviderResult
        {
            Vehicles = payload.Results
                .Select(MapToVehicle)
                .ToArray(),
            CurrentResults = payload.CurrentResults,
            TotalResults = payload.TotalResults,
            CurrentPage = payload.CurrentPage,
            TotalPages = payload.TotalPages
        };
    }

    private Dictionary<string, string> BuildSearchFields(
        int type,
        VehicleSearchCriteria criteria)
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
        AddIfPresent(fields, "manufacturer", criteria.Brand);
        AddIfPresent(fields, "model", criteria.Model);
        AddIfPresent(fields, "version", criteria.Version);

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
        CarzinClassifiedContract source)
    {
        return new Vehicle
        {
            ExternalId = source.Id.ToString(CultureInfo.InvariantCulture),
            Provider = ProviderName,
            Title = source.Title,
            Brand = source.Manufacturer,
            Model = source.Model,
            Version = source.Version,
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
}

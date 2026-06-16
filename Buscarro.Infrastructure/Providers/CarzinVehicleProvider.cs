using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Contracts;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Buscarro.Infrastructure.Providers;

public sealed class CarzinVehicleProvider : IVehicleProvider, IVehicleDetailsReadService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IVehicleProviderCatalogReadService _catalogReadService;
    private readonly CarzinOptions _options;

    public CarzinVehicleProvider(
        HttpClient httpClient,
        IOptions<CarzinOptions> options,
        IVehicleProviderCatalogReadService catalogReadService)
    {
        _httpClient = httpClient;
        _catalogReadService = catalogReadService;
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

        const int type = 1;
        var fields = BuildSearchFields(type, criteria);
        var payload = await PostFormAsync<CarzinClassifiedSearchResponseContract>(
            "/api/classified-search",
            fields,
            cancellationToken);

        var vehicles = await Task.WhenAll((payload.Results ?? [])
            .Select(result => MapToVehicleAsync(result, type, cancellationToken)));

        return new VehicleProviderResult
        {
            Vehicles = vehicles,
            CurrentResults = payload.CurrentResults,
            TotalResults = payload.TotalResults,
            CurrentPage = payload.CurrentPage,
            TotalPages = payload.TotalPages
        };
    }

    public async Task<VehicleDetailsResponse?> GetVehicleDetailsAsync(
        int externalId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            throw new InvalidOperationException("Token da Carzin nao configurado.");
        }

        var fields = new Dictionary<string, string>
        {
            ["type"] = "1",
            ["classified"] = externalId.ToString(CultureInfo.InvariantCulture)
        };
        var payload = await PostFormAsync<JsonElement>(
            "/api/classified-details",
            fields,
            cancellationToken);
        var details = ReadVehicleDetails(payload);

        return details is null
            ? null
            : MapToVehicleDetails(details);
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
        AddIfPresent(fields, "query", criteria.Query);
        AddIfPresent(fields, "state", criteria.State);
        AddIfPresent(fields, "city", criteria.City);
        AddIfPresent(fields, "manufacturer", criteria.Manufacturer);
        AddIfPresent(fields, "model", criteria.Model);
        AddIfPresent(fields, "version", criteria.Version);
        AddIfPresent(fields, "price_min", criteria.PriceMin);
        AddIfPresent(fields, "price_max", criteria.PriceMax);
        AddIfPresent(fields, "year_min", criteria.YearMin);
        AddIfPresent(fields, "year_max", criteria.YearMax);
        AddIfPresent(fields, "color", criteria.Color);
        AddIfPresent(fields, "gear", criteria.Gear);
        AddIfPresent(fields, "fuel", criteria.Fuel);

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

    private async Task<Vehicle> MapToVehicleAsync(
        CarzinClassifiedContract source,
        int vehicleType,
        CancellationToken cancellationToken)
    {
        var manufacturerId = source.ManufacturerId.GetValueOrDefault();
        var modelId = source.ModelId.GetValueOrDefault();
        var versionId = source.VersionId.GetValueOrDefault();

        var catalogNames = HasCatalogCodes(manufacturerId, modelId, versionId)
            ? await ResolveCatalogNamesOrFallbackAsync(
                ProviderName,
                vehicleType,
                manufacturerId.ToString(CultureInfo.InvariantCulture),
                modelId.ToString(CultureInfo.InvariantCulture),
                versionId.ToString(CultureInfo.InvariantCulture),
                cancellationToken)
            : new VehicleProviderCatalogNames(null, null, null);

        return new Vehicle
        {
            ExternalId = source.Id?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            Provider = ProviderName,
            Title = UseProviderValue(source.Title),
            Brand = UseCatalogOrProviderValue(catalogNames.ManufacturerName, source.Manufacturer),
            Model = UseCatalogOrProviderValue(catalogNames.ModelName, source.Model),
            Version = UseCatalogOrProviderValue(catalogNames.VersionName, source.Version),
            Year = source.YearModel.GetValueOrDefault() > 0
                ? source.YearModel.GetValueOrDefault()
                : source.YearManufacture.GetValueOrDefault(),
            Mileage = source.Mileage.GetValueOrDefault(),
            Doors = source.Doors,
            Armored = ReadBoolean(source.Armored),
            Fuel = UseProviderValue(source.Fuel),
            Transmission = UseProviderValue(source.Gear),
            Color = UseProviderValue(source.Color),
            Price = source.Price.GetValueOrDefault(),
            City = UseProviderValue(WebUtility.HtmlDecode(source.City)),
            State = UseProviderValue(source.State),
            Url = UseProviderValue(source.Url),
            ImageUrl = UseProviderValue(source.Image)
        };
    }

    private static bool HasCatalogCodes(
        int manufacturerId,
        int modelId,
        int versionId)
    {
        return manufacturerId > 0 && modelId > 0 && versionId > 0;
    }

    private async Task<VehicleProviderCatalogNames> ResolveCatalogNamesOrFallbackAsync(
        string providerName,
        int vehicleType,
        string manufacturerCode,
        string modelCode,
        string versionCode,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _catalogReadService.ResolveNamesAsync(
                providerName,
                vehicleType,
                manufacturerCode,
                modelCode,
                versionCode,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new VehicleProviderCatalogNames(null, null, null);
        }
    }

    private static string UseCatalogOrProviderValue(string? catalogValue, string? providerValue)
    {
        return string.IsNullOrWhiteSpace(catalogValue)
            ? UseProviderValue(providerValue)
            : catalogValue.Trim();
    }

    private static string UseProviderValue(string? providerValue)
    {
        return string.IsNullOrWhiteSpace(providerValue)
            ? string.Empty
            : providerValue.Trim();
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

    private static bool? ReadBoolean(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt32(out var number) => number == 1,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var boolean) => boolean,
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number == 1,
            _ => null
        };
    }

    private static VehicleDetailsResponse MapToVehicleDetails(CarzinClassifiedDetailsContract source)
    {
        return new VehicleDetailsResponse
        {
            Observations = NormalizeTextList(source.Observations),
            Optionals = NormalizeTextList(source.Optionals),
            Images = source.Images
                .Select(MapToVehicleDetailsImage)
                .Where(image =>
                    !string.IsNullOrWhiteSpace(image.ImageUrl) ||
                    !string.IsNullOrWhiteSpace(image.ThumbUrl))
                .ToArray()
        };
    }

    private static VehicleDetailsImageResponse MapToVehicleDetailsImage(CarzinClassifiedImageContract source)
    {
        return new VehicleDetailsImageResponse
        {
            ImageUrl = UseProviderValue(source.Image),
            ThumbUrl = UseProviderValue(source.ImageThumb)
        };
    }

    private static string[] NormalizeTextList(IEnumerable<string?> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();
    }

    private static CarzinClassifiedDetailsContract? ReadVehicleDetails(JsonElement payload)
    {
        if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        if (payload.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        ThrowIfCarzinReturnedError(payload);

        var detailsElement = payload.TryGetProperty("classified", out var classifiedElement)
            ? classifiedElement
            : payload;

        if (detailsElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        if (detailsElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return JsonSerializer.Deserialize<CarzinClassifiedDetailsContract>(
            detailsElement.GetRawText(),
            SerializerOptions);
    }

    private static void ThrowIfCarzinReturnedError(JsonElement payload)
    {
        if (!payload.TryGetProperty("error", out var errorElement))
        {
            return;
        }

        var errorCode = ReadInt(errorElement);
        if (errorCode.GetValueOrDefault() == 0)
        {
            return;
        }

        throw new ProviderAccessDeniedException(
            "Carzin",
            "Nao temos permissao na Carzin para acessar os detalhes deste classificado.");
    }

    private static int? ReadInt(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
            _ => null
        };
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

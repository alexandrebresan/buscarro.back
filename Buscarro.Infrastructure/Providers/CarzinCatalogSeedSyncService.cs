using Buscarro.Infrastructure.Contracts;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace Buscarro.Infrastructure.Providers;

public sealed class CarzinCatalogSeedSyncService
{
    private const string ProviderName = "Carzin";
    private const int VehicleType = 1;

    private readonly HttpClient _httpClient;
    private readonly CarzinOptions _carzinOptions;
    private readonly CarzinCatalogOptions _catalogOptions;
    private readonly ICarzinCatalogStore _store;
    private readonly TimeProvider _timeProvider;

    public CarzinCatalogSeedSyncService(
        HttpClient httpClient,
        IOptions<CarzinOptions> carzinOptions,
        IOptions<CarzinCatalogOptions> catalogOptions,
        ICarzinCatalogStore store,
        TimeProvider timeProvider)
    {
        _httpClient = httpClient;
        _carzinOptions = carzinOptions.Value;
        _catalogOptions = catalogOptions.Value;
        _store = store;
        _timeProvider = timeProvider;

        if (_httpClient.BaseAddress is null &&
            Uri.TryCreate(_carzinOptions.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            _httpClient.BaseAddress = baseUri;
        }
    }

    public async Task<CarzinCatalogSeedSyncResult> SyncAsync(CancellationToken cancellationToken)
    {
        ValidateConfiguration();

        var manufacturersCount = 0;
        var modelsCount = 0;
        var versionsCount = 0;
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var manufacturers = await GetCatalogItemsAsync(
            _catalogOptions.ManufacturersPath,
            new Dictionary<string, string>
            {
                ["type"] = VehicleType.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken);

        foreach (var manufacturerItem in manufacturers)
        {
            var manufacturer = await _store.UpsertManufacturerAsync(
                ProviderName,
                VehicleType,
                manufacturerItem.ProviderCode,
                manufacturerItem.Name,
                now,
                cancellationToken);
            manufacturersCount++;

            var models = await GetCatalogItemsAsync(
                _catalogOptions.ModelsPath,
                new Dictionary<string, string>
                {
                    ["type"] = VehicleType.ToString(CultureInfo.InvariantCulture),
                    ["manufacturer"] = manufacturerItem.ProviderCode
                },
                cancellationToken);

            foreach (var modelItem in models)
            {
                var model = await _store.UpsertModelAsync(
                    manufacturer,
                    ProviderName,
                    VehicleType,
                    modelItem.ProviderCode,
                    modelItem.Name,
                    now,
                    cancellationToken);
                modelsCount++;

                var versions = await GetCatalogItemsAsync(
                    _catalogOptions.VersionsPath,
                    new Dictionary<string, string>
                    {
                        ["type"] = VehicleType.ToString(CultureInfo.InvariantCulture),
                        ["manufacturer"] = manufacturerItem.ProviderCode,
                        ["model"] = modelItem.ProviderCode
                    },
                    cancellationToken);

                foreach (var versionItem in versions)
                {
                    await _store.UpsertVersionAsync(
                        model,
                        ProviderName,
                        VehicleType,
                        versionItem.ProviderCode,
                        versionItem.Name,
                        now,
                        cancellationToken);
                    versionsCount++;
                }
            }
        }

        await _store.SaveChangesAsync(cancellationToken);
        return new CarzinCatalogSeedSyncResult(manufacturersCount, modelsCount, versionsCount);
    }

    private async Task<IReadOnlyCollection<CarzinCatalogItemContract>> GetCatalogItemsAsync(
        string path,
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(_carzinOptions.Token), "token");

        foreach (var field in fields)
        {
            content.Add(new StringContent(field.Value), field.Key);
        }

        using var response = await _httpClient.PostAsync(path, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ReadCatalogItems(document.RootElement);
    }

    private void ValidateConfiguration()
    {
        Require(_carzinOptions.BaseUrl, "Carzin:BaseUrl");
        Require(_carzinOptions.Token, "Carzin:Token");
        Require(_catalogOptions.ManufacturersPath, "CarzinCatalog:ManufacturersPath");
        Require(_catalogOptions.ModelsPath, "CarzinCatalog:ModelsPath");
        Require(_catalogOptions.VersionsPath, "CarzinCatalog:VersionsPath");
    }

    private static void Require(string value, string configurationKey)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{configurationKey} nao configurado.");
        }
    }

    private static IReadOnlyCollection<CarzinCatalogItemContract> ReadCatalogItems(JsonElement root)
    {
        var items = EnumerateItems(root)
            .Select(ReadCatalogItem)
            .Where(item => !string.IsNullOrWhiteSpace(item.ProviderCode) &&
                           !string.IsNullOrWhiteSpace(item.Name))
            .ToArray();

        return items;
    }

    private static IEnumerable<JsonElement> EnumerateItems(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray();
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        foreach (var propertyName in new[] { "results", "data", "manufacturers", "models", "versions" })
        {
            if (root.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.Array)
            {
                return property.EnumerateArray();
            }
        }

        return [];
    }

    private static CarzinCatalogItemContract ReadCatalogItem(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return new CarzinCatalogItemContract();
        }

        return new CarzinCatalogItemContract
        {
            ProviderCode = ReadStringProperty(element, "id", "code", "value"),
            Name = ReadStringProperty(element, "name", "title", "label")
        };
    }

    private static string ReadStringProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var property))
            {
                continue;
            }

            var value = property.ValueKind switch
            {
                JsonValueKind.Number when property.TryGetInt64(out var longValue) =>
                    longValue.ToString(CultureInfo.InvariantCulture),
                JsonValueKind.String => property.GetString(),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }
}

using Buscarro.Infrastructure.Contracts;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
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

            await _store.SaveChangesAsync(cancellationToken);
        }

        return new CarzinCatalogSeedSyncResult(manufacturersCount, modelsCount, versionsCount);
    }

    private async Task<IReadOnlyCollection<CarzinCatalogItemContract>> GetCatalogItemsAsync(
        string path,
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(1, _catalogOptions.MaxRetryAttempts);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var content = CreateCatalogRequestContent(fields);
            using var response = await _httpClient.PostAsync(path, content, cancellationToken);

            if (!IsTransientStatusCode(response.StatusCode))
            {
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var items = ReadCatalogItems(document.RootElement);
                await WaitAfterSuccessfulCatalogRequestAsync(cancellationToken);
                return items;
            }

            if (attempt == maxAttempts)
            {
                throw new HttpRequestException(
                    $"Carzin catalog request failed with transient status {(int)response.StatusCode} after {maxAttempts} attempts. Path: {path}. Fields: {DescribeFields(fields)}.",
                    null,
                    response.StatusCode);
            }

            await Task.Delay(GetRetryDelay(response, attempt), cancellationToken);
        }

        throw new InvalidOperationException("Fluxo de retry do catalogo Carzin encerrado inesperadamente.");
    }

    private MultipartFormDataContent CreateCatalogRequestContent(IReadOnlyDictionary<string, string> fields)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(_carzinOptions.Token), "token");

        foreach (var field in fields)
        {
            content.Add(new StringContent(field.Value), field.Key);
        }

        return content;
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
    }

    private async Task WaitAfterSuccessfulCatalogRequestAsync(CancellationToken cancellationToken)
    {
        if (_catalogOptions.RequestDelayMilliseconds <= 0)
        {
            return;
        }

        await Task.Delay(
            TimeSpan.FromMilliseconds(_catalogOptions.RequestDelayMilliseconds),
            cancellationToken);
    }

    private TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (response.Headers.RetryAfter?.Date is { } retryDate)
        {
            var delay = retryDate - _timeProvider.GetUtcNow();
            return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        }

        var baseDelaySeconds = Math.Max(1, _catalogOptions.RetryBaseDelaySeconds);
        var maxDelaySeconds = Math.Max(baseDelaySeconds, _catalogOptions.RetryMaxDelaySeconds);
        var exponentialDelay = baseDelaySeconds * Math.Pow(2, attempt - 1);
        return TimeSpan.FromSeconds(Math.Min(maxDelaySeconds, exponentialDelay));
    }

    private static string DescribeFields(IReadOnlyDictionary<string, string> fields)
    {
        return string.Join(", ", fields.Select(field => $"{field.Key}={field.Value}"));
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

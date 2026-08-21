using System.Globalization;
using System.Net;
using System.Text.Json;
using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Infrastructure.Contracts;
using Microsoft.Extensions.Options;
using Buscarro.Application.Services;
using System.Diagnostics;

namespace Buscarro.Infrastructure.Providers;

public sealed class CarzinCatalogSource : IVehicleCatalogSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;
    private readonly CarzinOptions _carzin;
    private readonly CarzinCatalogSourceOptions _options;
    private readonly CarzinCircuitBreaker _breaker;
    private readonly TimeProvider _timeProvider;
    private readonly ICarzinRetryDelay _retryDelay;

    public CarzinCatalogSource(HttpClient client, IOptions<CarzinOptions> carzin, CarzinCatalogSourceOptions options,
        CarzinCircuitBreaker breaker, TimeProvider timeProvider, ICarzinRetryDelay retryDelay)
    {
        _client = client; _carzin = carzin.Value; _options = options; _breaker = breaker; _timeProvider = timeProvider; _retryDelay = retryDelay;
        if (_client.BaseAddress is null) _client.BaseAddress = new Uri(_carzin.BaseUrl);
    }

    public string Provider => "Carzin";

    public async Task<CatalogSourcePage> ReadPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var requestStarted=Stopwatch.GetTimestamp();
        try
        {
        if (string.IsNullOrWhiteSpace(_carzin.Token)) throw new InvalidOperationException("Carzin token is not configured.");
        using var probe = _breaker.Enter();
        Exception? last = null;
        for (var attempt = 0; attempt <= _options.RetryCount; attempt++)
        {
            try
            {
                using var deadline = new CancellationTokenSource(_options.RequestTimeout, _timeProvider);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
                using var content = new MultipartFormDataContent();
                content.Add(new StringContent(_carzin.Token), "token");
                content.Add(new StringContent("1"), "type");
                content.Add(new StringContent(page.ToString(CultureInfo.InvariantCulture)), "page");
                content.Add(new StringContent(pageSize.ToString(CultureInfo.InvariantCulture)), "results");
                using var response = await _client.PostAsync("/api/classified-search", content, timeout.Token);
                if (response.StatusCode == HttpStatusCode.RequestTimeout)
                    CatalogMetrics.RecordTimeout("provider_request");
                if (IsTransient(response.StatusCode) && attempt < _options.RetryCount)
                {
                    CatalogMetrics.RecordRetry(Provider);
                    await _retryDelay.DelayAsync(attempt, response.Headers.RetryAfter, cancellationToken); continue;
                }
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var payload = await JsonSerializer.DeserializeAsync<CarzinClassifiedSearchResponseContract>(stream, JsonOptions, cancellationToken)
                    ?? new CarzinClassifiedSearchResponseContract();
                _breaker.Success();
                return new(page, payload.CurrentPage, payload.CurrentResults, payload.TotalPages, payload.TotalResults,
                    (payload.Results ?? []).Select(Map).ToArray());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (OperationCanceledException exception)
            {
                CatalogMetrics.RecordTimeout("provider_request");
                last = exception;
                if (attempt >= _options.RetryCount)
                    break;

                CatalogMetrics.RecordRetry(Provider);
                await _retryDelay.DelayAsync(attempt, null, cancellationToken);
            }
            catch (HttpRequestException exception) when (attempt < _options.RetryCount)
            {
                CatalogMetrics.RecordRetry(Provider);
                last = exception;
                await _retryDelay.DelayAsync(attempt, null, cancellationToken);
            }
            catch (Exception exception) { last = exception; break; }
        }
        _breaker.Failure();
        throw new HttpRequestException("Carzin catalog page request failed after retries.", last);
        }
        finally{CatalogMetrics.RecordProviderRequest(Provider,Stopwatch.GetElapsedTime(requestStarted));}
    }

    private static bool IsTransient(HttpStatusCode status) => status is HttpStatusCode.RequestTimeout or (HttpStatusCode)429 || (int)status >= 500;

    private static CatalogSourceVehicle Map(CarzinClassifiedContract value) => new(
        Positive(value.Id), Clean(value.Title), null, null, null,
        Positive(value.ManufacturerId), Positive(value.ModelId), Positive(value.VersionId),
        value.YearModel is > 0 ? value.YearModel : value.YearManufacture is > 0 ? value.YearManufacture : null,
        value.Mileage, value.Doors, ReadBoolean(value.Armored),
        Clean(value.Fuel), Clean(value.Gear), Clean(value.Color), value.Price,
        Clean(WebUtility.HtmlDecode(value.City)), Clean(value.State), Clean(value.Url), Clean(value.Image));

    private static string? Positive(int? value) => value is > 0 ? value.Value.ToString(CultureInfo.InvariantCulture) : null;
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool? ReadBoolean(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true, JsonValueKind.False => false,
        JsonValueKind.Number when value.TryGetInt32(out var number) => number != 0,
        JsonValueKind.String when bool.TryParse(value.GetString(), out var boolean) => boolean,
        JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number != 0,
        _ => null
    };
}

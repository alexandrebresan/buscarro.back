using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Infrastructure.Contracts;
using Microsoft.Extensions.Options;

namespace Buscarro.Infrastructure.Providers;

public sealed class AutoDevCatalogSource : IVehicleCatalogSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;
    private readonly AutoDevOptions _autoDev;
    private readonly AutoDevCatalogSourceOptions _options;
    private readonly IAutoDevRequestPacer _pacer;
    private readonly TimeProvider _timeProvider;
    private readonly HashSet<string> _acceptedPageFingerprints = new(StringComparer.Ordinal);
    private int _attempts;

    public AutoDevCatalogSource(HttpClient client, IOptions<AutoDevOptions> autoDev, AutoDevCatalogSourceOptions options,
        IAutoDevRequestPacer pacer, TimeProvider timeProvider)
    {
        _client = client; _autoDev = autoDev.Value; _options = options; _pacer = pacer; _timeProvider = timeProvider;
        if (_client.BaseAddress is null) _client.BaseAddress = new Uri(_autoDev.BaseUrl);
    }

    public string Provider => "AutoDev";
    public int? PageSize => _options.PageSize;

    public async Task<CatalogSourcePage> ReadPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page > _options.MaximumSuccessfulPages)
            return new(page, page, 0, null, null, [], false);
        if (string.IsNullOrWhiteSpace(_autoDev.ApiKey)) throw new InvalidOperationException("AutoDev API key is not configured.");

        Exception? last = null;
        for (var retry = 0; retry <= _options.RetryCount; retry++)
        {
            try
            {
                if (Interlocked.Increment(ref _attempts) > _options.MaximumHttpAttempts)
                    throw new InvalidOperationException("autodev_http_attempt_limit_exceeded");
                await _pacer.WaitAsync(cancellationToken);
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    $"listings?page={page}&limit={_options.PageSize}&sort=updatedAt.desc&includeUnpriced=false");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _autoDev.ApiKey);
                using var deadline = new CancellationTokenSource(_options.RequestTimeout, _timeProvider);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
                using var response = await _client.SendAsync(request, timeout.Token);
                if (IsTransient(response.StatusCode) && retry < _options.RetryCount)
                {
                    last = new HttpRequestException($"AutoDev returned {(int)response.StatusCode}.");
                    continue;
                }
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                var payload = await JsonSerializer.DeserializeAsync<AutoDevListingsContract>(stream, JsonOptions, timeout.Token) ?? new();
                var items = (payload.Data ?? []).Select(Map).ToArray();
                if (_acceptedPageFingerprints.Add(Fingerprint(items)))
                    return new(page, page, items.Length, null, null, items,
                        !string.IsNullOrWhiteSpace(payload.Links?.Next) && page < _options.MaximumSuccessfulPages);
                if (retry < _options.RetryCount) continue;
                return new(page, page, 0, null, null, [], false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (OperationCanceledException exception)
            {
                last = exception;
                if (retry >= _options.RetryCount) break;
            }
            catch (HttpRequestException exception) when (exception.StatusCode is null && retry < _options.RetryCount)
            {
                last = exception;
            }
        }
        throw new HttpRequestException("AutoDev catalog page request failed after retries.", last);
    }

    private static bool IsTransient(HttpStatusCode status) => status is HttpStatusCode.RequestTimeout or (HttpStatusCode)429 || (int)status >= 500;

    private static string Fingerprint(CatalogSourceVehicle[] items)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(items, JsonOptions)));

    private static CatalogSourceVehicle Map(AutoDevListingContract listing)
    {
        var vehicle = listing.Vehicle;
        var retail = listing.RetailListing;
        return new(Clean(listing.Vin) ?? Clean(vehicle?.Vin), null, Clean(vehicle?.Make), Clean(vehicle?.Model), Clean(vehicle?.Trim),
            null, null, null, vehicle?.Year, vehicle?.Miles ?? retail?.Miles, vehicle?.Doors, null, Clean(vehicle?.Fuel),
            Clean(vehicle?.Transmission), Clean(vehicle?.ExteriorColor), retail?.Price, Clean(retail?.City), Clean(retail?.State),
            Clean(retail?.Vdp), Clean(retail?.PrimaryImage));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

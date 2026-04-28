using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Buscarro.Infrastructure.Providers;

public sealed class VehicleCatalogReadService : IVehicleCatalogReadService
{
    private const string CacheKeyPrefix = "vehicles:provider";
    private const string SanitizedProviderError = "Falha ao consultar os dados deste provider.";

    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _providerLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<VehicleCatalogReadService> _logger;
    private readonly VehicleCacheOptions _options;
    private readonly IReadOnlyCollection<IVehicleProvider> _providers;
    private readonly TimeProvider _timeProvider;

    public VehicleCatalogReadService(
        IEnumerable<IVehicleProvider> providers,
        IMemoryCache cache,
        IOptions<VehicleCacheOptions> options,
        TimeProvider timeProvider,
        ILogger<VehicleCatalogReadService> logger)
    {
        _providers = providers.ToArray();
        _cache = cache;
        _logger = logger;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<VehicleCatalogSnapshot> GetSnapshotAsync(
        VehicleSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var providerTasks = _providers.Select(provider =>
        {
            return GetProviderSnapshotAsync(
                provider,
                criteria,
                cancellationToken);
        });

        var providers = await Task.WhenAll(providerTasks);

        return ToSnapshot(providers);
    }

    private async Task<VehicleProviderSnapshot> GetProviderSnapshotAsync(
        IVehicleProvider provider,
        VehicleSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildCacheKey(provider.ProviderName, criteria);
        var now = _timeProvider.GetUtcNow();

        if (TryGetFreshProviderState(cacheKey, now, out var cachedState))
        {
            return ToSnapshot(cachedState, servedFromCurrentCache: true);
        }

        var providerLock = _providerLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await providerLock.WaitAsync(cancellationToken);

        try
        {
            now = _timeProvider.GetUtcNow();

            if (TryGetFreshProviderState(cacheKey, now, out cachedState))
            {
                return ToSnapshot(cachedState, servedFromCurrentCache: true);
            }

            var refreshedState = await LoadProviderStateAsync(provider, criteria, now, cancellationToken);
            _cache.Set(cacheKey, refreshedState);

            return ToSnapshot(refreshedState, servedFromCurrentCache: false);
        }
        finally
        {
            providerLock.Release();
        }
    }

    private bool TryGetFreshProviderState(
        string cacheKey,
        DateTimeOffset now,
        out VehicleProviderCacheState cachedState)
    {
        cachedState = null!;

        if (!_cache.TryGetValue(cacheKey, out VehicleProviderCacheState? state) || state is null)
        {
            return false;
        }

        if (state.FreshUntilUtc <= now)
        {
            cachedState = state;
            return false;
        }

        cachedState = state;
        return true;
    }

    private async Task<VehicleProviderCacheState> LoadProviderStateAsync(
        IVehicleProvider provider,
        VehicleSearchCriteria criteria,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await provider.GetVehiclesAsync(criteria, cancellationToken);

            return new VehicleProviderCacheState
            {
                Provider = provider.ProviderName,
                FreshUntilUtc = GetFreshUntilUtc(provider.ProviderName, now),
                Vehicles = CloneVehicles(result.Vehicles),
                Success = true,
                CurrentResults = result.CurrentResults,
                TotalResults = result.TotalResults,
                CurrentPage = result.CurrentPage,
                TotalPages = result.TotalPages
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao consultar o provider {Provider}.", provider.ProviderName);

            return new VehicleProviderCacheState
            {
                Provider = provider.ProviderName,
                FreshUntilUtc = GetFreshUntilUtc(provider.ProviderName, now),
                Vehicles = [],
                Success = false,
                Error = SanitizedProviderError,
                CurrentResults = 0,
                TotalResults = 0,
                CurrentPage = Math.Max(1, criteria.Page),
                TotalPages = 0
            };
        }
    }

    private static VehicleCatalogSnapshot ToSnapshot(IReadOnlyCollection<VehicleProviderSnapshot> providers)
    {
        return new VehicleCatalogSnapshot
        {
            Vehicles = providers
                .SelectMany(provider => provider.Vehicles)
                .OrderBy(vehicle => vehicle.Brand)
                .ThenBy(vehicle => vehicle.Model)
                .ThenByDescending(vehicle => vehicle.Year)
                .ToArray(),
            Providers = providers
        };
    }

    private static VehicleProviderSnapshot ToSnapshot(
        VehicleProviderCacheState provider,
        bool servedFromCurrentCache)
    {
        return new VehicleProviderSnapshot
        {
            Provider = provider.Provider,
            Vehicles = CloneVehicles(provider.Vehicles),
            Success = provider.Success,
            FromCache = servedFromCurrentCache || provider.FromCache,
            UsedStaleCache = provider.UsedStaleCache,
            Error = provider.Error,
            CurrentResults = provider.CurrentResults,
            TotalResults = provider.TotalResults,
            CurrentPage = provider.CurrentPage,
            TotalPages = provider.TotalPages
        };
    }

    private static Vehicle[] CloneVehicles(IEnumerable<Vehicle> vehicles)
    {
        return vehicles
            .Select(vehicle => new Vehicle
            {
                ExternalId = vehicle.ExternalId,
                Provider = vehicle.Provider,
                Title = vehicle.Title,
                Brand = vehicle.Brand,
                Model = vehicle.Model,
                Version = vehicle.Version,
                Year = vehicle.Year,
                Mileage = vehicle.Mileage,
                Fuel = vehicle.Fuel,
                Transmission = vehicle.Transmission,
                Color = vehicle.Color,
                Price = vehicle.Price,
                City = vehicle.City,
                State = vehicle.State,
                Url = vehicle.Url,
                ImageUrl = vehicle.ImageUrl
            })
            .ToArray();
    }

    private DateTimeOffset GetFreshUntilUtc(string providerName, DateTimeOffset now)
    {
        var durationMinutes = _options.ProviderDurationMinutes.TryGetValue(providerName, out var providerDurationMinutes)
            ? providerDurationMinutes
            : _options.DurationMinutes;

        return now.AddMinutes(Math.Max(0, durationMinutes));
    }

    private static string BuildCacheKey(string providerName, VehicleSearchCriteria criteria)
    {
        return string.Join(
            "|",
            CacheKeyPrefix,
            Normalize(providerName),
            criteria.Type,
            Normalize(criteria.Brand),
            Normalize(criteria.Model),
            Normalize(criteria.Version),
            Normalize(criteria.Color),
            Normalize(criteria.Fuel),
            Normalize(criteria.Transmission),
            Normalize(criteria.State),
            Normalize(criteria.City),
            criteria.PriceMin?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            criteria.PriceMax?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            criteria.YearMin?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            criteria.YearMax?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            Normalize(criteria.Order),
            Math.Max(1, criteria.Page),
            Math.Max(1, criteria.Results),
            criteria.SellerType?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private sealed class VehicleProviderCacheState
    {
        public string Provider { get; init; } = string.Empty;
        public DateTimeOffset FreshUntilUtc { get; init; }
        public IReadOnlyCollection<Vehicle> Vehicles { get; init; } = [];
        public bool Success { get; init; }
        public bool FromCache { get; init; }
        public bool UsedStaleCache { get; init; }
        public string? Error { get; init; }
        public int? CurrentResults { get; init; }
        public int? TotalResults { get; init; }
        public int? CurrentPage { get; init; }
        public int? TotalPages { get; init; }
    }
}

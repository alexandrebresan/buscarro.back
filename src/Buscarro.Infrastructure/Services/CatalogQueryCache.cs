using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Buscarro.Application.DTOs;
using Buscarro.Application.Services;
using Buscarro.Infrastructure.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Buscarro.Infrastructure.Services;

public sealed class CatalogQueryCache : IDisposable
{
    private readonly MemoryCache _cache;
    private readonly CatalogQueryOptions _options;
    private readonly TimeProvider _timeProvider;

    private readonly object _unitLock =
        new();

    private readonly Dictionary<string, CacheUnit> _units =
        new(StringComparer.Ordinal);

    public CatalogQueryCache(
        IOptions<CatalogQueryOptions> options,
        TimeProvider? timeProvider = null)
    {
        _options =
            options.Value;

        _timeProvider =
            timeProvider ??
            TimeProvider.System;

        _cache =
            new MemoryCache(
                new MemoryCacheOptions
                {
                    SizeLimit =
                        Math.Max(
                            1,
                            _options.CacheSizeLimit)
                });
    }

    public bool TryGetCount(
        string key,
        out int value)
    {
        var hit =
            _cache.TryGetValue(
                "count:" + key,
                out value);

        CatalogMetrics.RecordCache(
            "count",
            hit);

        return hit;
    }

    public void SetCount(
        string key,
        int value)
    {
        Set(
            "count:" + key,
            value,
            1,
            TimeSpan.FromSeconds(
                _options.CountCacheTtlSeconds));
    }

    internal bool TryGetPage(
        string key,
        out CatalogCachedPage value)
    {
        var hit =
            _cache.TryGetValue(
                "page:" + key,
                out value!);

        CatalogMetrics.RecordCache(
            "page",
            hit);

        return hit;
    }

    internal void SetPage(
        string key,
        CatalogCachedPage value)
    {
        Set(
            "page:" + key,
            value,
            Math.Max(
                1,
                value.Vehicles.Count),
            TimeSpan.FromSeconds(
                _options.PageCacheTtlSeconds));
    }

    internal bool TryGetResult(
        string key,
        out VehicleCatalogSearchResult value)
    {
        var cacheKey =
            "result:" + key;

        var hit =
            _cache.TryGetValue<CatalogCachedResult>(
                cacheKey,
                out var entry);

        if (
            hit &&
            entry is not null &&
            _timeProvider.GetUtcNow() >=
            entry.ExpiresAtUtc
        )
        {
            _cache.Remove(cacheKey);

            hit = false;
        }

        CatalogMetrics.RecordCache(
            "result",
            hit);

        value =
            hit &&
            entry is not null
                ? CatalogSearchResultSnapshot.Copy(
                    entry.Result,
                    fromCache: true)
                : null!;

        return hit;
    }

    internal void SetResult(
        string key,
        VehicleCatalogSearchResult value)
    {
        var ttl =
            TimeSpan.FromSeconds(
                _options.ResultCacheTtlSeconds);

        Set(
            "result:" + key,
            new CatalogCachedResult(
                CatalogSearchResultSnapshot.Copy(
                    value),
                _timeProvider.GetUtcNow() +
                ttl),
            Math.Max(
                1,
                value.Vehicles.Count),
            ttl);
    }

    internal static string BuildResultKey(
        NormalizedVehicleCatalogSearchCriteria criteria)
    {
        return
            $"catalog:result:{Hash(Canonical(criteria, true))}";
    }

    public static string BuildCountKey(
        int publicationId,
        NormalizedVehicleCatalogSearchCriteria criteria)
    {
        return
            $"catalog:{publicationId}:count:" +
            $"{Hash(Canonical(criteria, false))}";
    }

    public static string BuildPageKey(
        int publicationId,
        NormalizedVehicleCatalogSearchCriteria criteria)
    {
        return
            $"catalog:{publicationId}:page:" +
            $"{Hash(Canonical(criteria, true))}";
    }

    private static string Canonical(
        NormalizedVehicleCatalogSearchCriteria criteria,
        bool page)
    {
        return JsonSerializer.Serialize(
            new
            {
                criteria.Query,

                criteria.Providers,

                criteria.Countries,

                criteria.States,

                criteria.Cities,

                criteria.Manufacturers,

                criteria.Models,

                criteria.Versions,

                criteria.Colors,

                criteria.Fuels,

                criteria.Gears,

                criteria.PriceMin,

                criteria.PriceMax,

                criteria.YearMin,

                criteria.YearMax,

                Order =
                    page
                        ? criteria.Order
                        : null,

                Page =
                    page
                        ? criteria.Page
                        : 0,

                PageSize =
                    page
                        ? criteria.PageSize
                        : 0
            });
    }

    private static string Hash(
        string value)
    {
        return Convert
            .ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        value)))
            .ToLowerInvariant();
    }

    private void Set<T>(
        string key,
        T value,
        long size,
        TimeSpan ttl)
    {
        var unit =
            new CacheUnit(
                key,
                size);

        long delta;

        lock (_unitLock)
        {
            delta =
                size -
                (
                    _units.TryGetValue(
                        key,
                        out var old)
                        ? old.Size
                        : 0
                );

            _units[key] =
                unit;
        }

        CatalogMetrics.AdjustCacheUnits(
            delta);

        _cache.Set(
            key,
            value,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow =
                    ttl,

                Size =
                    size
            }
            .RegisterPostEvictionCallback(
                (
                    _,
                    _,
                    _,
                    state
                ) =>
                {
                    ((CatalogQueryCache)state!)
                        .Evicted(unit);
                },
                this));
    }

    private void Evicted(
        CacheUnit unit)
    {
        lock (_unitLock)
        {
            if (
                !_units.TryGetValue(
                    unit.Key,
                    out var current)
                ||
                !ReferenceEquals(
                    current,
                    unit)
            )
            {
                return;
            }

            _units.Remove(
                unit.Key);
        }

        CatalogMetrics.AdjustCacheUnits(
            -unit.Size);
    }

    public void Dispose()
    {
        long units;

        lock (_unitLock)
        {
            units =
                _units.Values.Sum(
                    item => item.Size);

            _units.Clear();
        }

        CatalogMetrics.AdjustCacheUnits(
            -units);

        _cache.Dispose();
    }

    private sealed record CacheUnit(
        string Key,
        long Size);

    private sealed record CatalogCachedResult(
        VehicleCatalogSearchResult Result,
        DateTimeOffset ExpiresAtUtc);
}

internal sealed record CatalogCachedPage(
    IReadOnlyCollection<VehicleResponse> Vehicles,
    IReadOnlyDictionary<string, int> ProviderCounts);
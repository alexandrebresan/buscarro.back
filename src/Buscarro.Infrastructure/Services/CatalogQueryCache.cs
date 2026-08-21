using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Buscarro.Application.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Buscarro.Infrastructure.Providers;
using Buscarro.Application.Services;

namespace Buscarro.Infrastructure.Services;

public sealed class CatalogQueryCache : IDisposable
{
    private readonly MemoryCache _cache;
    private readonly CatalogQueryOptions _options;
    private readonly object _unitLock=new();
    private readonly Dictionary<string,CacheUnit> _units=new(StringComparer.Ordinal);
    public CatalogQueryCache(IOptions<CatalogQueryOptions> options)
    {
        _options = options.Value;
        _cache = new(new MemoryCacheOptions { SizeLimit = Math.Max(1, _options.CacheSizeLimit) });
    }
    public bool TryGetCount(string key, out int value) {var hit=_cache.TryGetValue("count:"+key,out value);CatalogMetrics.RecordCache("count",hit);return hit;}
    public void SetCount(string key, int value) => Set("count:"+key,value,1,TimeSpan.FromSeconds(_options.CountCacheTtlSeconds));
    internal bool TryGetPage(string key, out CatalogCachedPage value) {var hit=_cache.TryGetValue("page:"+key,out value!);CatalogMetrics.RecordCache("page",hit);return hit;}
    internal void SetPage(string key, CatalogCachedPage value) => Set("page:"+key,value,Math.Max(1,value.Vehicles.Count),TimeSpan.FromSeconds(_options.PageCacheTtlSeconds));
    public static string BuildCountKey(int publicationId, NormalizedVehicleCatalogSearchCriteria c) => $"catalog:{publicationId}:count:{Hash(Canonical(c, false))}";
    public static string BuildPageKey(int publicationId, NormalizedVehicleCatalogSearchCriteria c) => $"catalog:{publicationId}:page:{Hash(Canonical(c, true))}";
    private static string Canonical(NormalizedVehicleCatalogSearchCriteria c, bool page) => JsonSerializer.Serialize(new
    {
        c.Query,c.Providers,c.States,c.Cities,c.Manufacturers,c.Models,c.Versions,c.Colors,c.Fuels,c.Gears,
        c.PriceMin,c.PriceMax,c.YearMin,c.YearMax,Order=page?c.Order:null,Page=page?c.Page:0,PageSize=page?c.PageSize:0
    });
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private void Set<T>(string key,T value,long size,TimeSpan ttl)
    {
        var unit=new CacheUnit(key,size);long delta;
        lock(_unitLock){delta=size-(_units.TryGetValue(key,out var old)?old.Size:0);_units[key]=unit;}
        CatalogMetrics.AdjustCacheUnits(delta);
        _cache.Set(key,value,new MemoryCacheEntryOptions{AbsoluteExpirationRelativeToNow=ttl,Size=size}.RegisterPostEvictionCallback((_,_,_,state)=>((CatalogQueryCache)state!).Evicted(unit),this));
    }
    private void Evicted(CacheUnit unit)
    {
        lock(_unitLock){if(!_units.TryGetValue(unit.Key,out var current)||!ReferenceEquals(current,unit))return;_units.Remove(unit.Key);}
        CatalogMetrics.AdjustCacheUnits(-unit.Size);
    }
    public void Dispose()
    {
        long units;lock(_unitLock){units=_units.Values.Sum(x=>x.Size);_units.Clear();}CatalogMetrics.AdjustCacheUnits(-units);_cache.Dispose();
    }
    private sealed record CacheUnit(string Key,long Size);
}

internal sealed record CatalogCachedPage(IReadOnlyCollection<VehicleResponse> Vehicles, IReadOnlyDictionary<string, int> ProviderCounts);

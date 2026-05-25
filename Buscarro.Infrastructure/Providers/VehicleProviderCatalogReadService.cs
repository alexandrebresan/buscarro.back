using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;

namespace Buscarro.Infrastructure.Providers;

public sealed class VehicleProviderCatalogReadService : IVehicleProviderCatalogReadService
{
    private const string CacheKeyPrefix = "vehicle-provider-catalog";
    private const string DefaultProviderName = "Carzin";
    private const int DefaultVehicleType = 1;

    private readonly IDbContextFactory<BuscarroDbContext> _dbContextFactory;
    private readonly IMemoryCache _cache;

    public VehicleProviderCatalogReadService(
        IDbContextFactory<BuscarroDbContext> dbContextFactory,
        IMemoryCache cache)
    {
        _dbContextFactory = dbContextFactory;
        _cache = cache;
    }

    public async Task<VehicleProviderCatalogNames> ResolveNamesAsync(
        string providerName,
        int vehicleType,
        string manufacturerCode,
        string modelCode,
        string versionCode,
        CancellationToken cancellationToken)
    {
        var manufacturerCacheKey = BuildManufacturerCacheKey(providerName, vehicleType, manufacturerCode);
        var modelCacheKey = BuildModelCacheKey(providerName, vehicleType, manufacturerCode, modelCode);
        var versionCacheKey = BuildVersionCacheKey(providerName, vehicleType, manufacturerCode, modelCode, versionCode);

        var hasManufacturerName = TryGetCachedName(manufacturerCacheKey, out var manufacturerName);
        var hasModelName = TryGetCachedName(modelCacheKey, out var modelName);
        var hasVersionName = TryGetCachedName(versionCacheKey, out var versionName);

        if (hasManufacturerName && hasModelName && hasVersionName)
        {
            return new VehicleProviderCatalogNames(manufacturerName, modelName, versionName);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (!hasManufacturerName)
        {
            manufacturerName = await dbContext.VehicleProviderManufacturers
                .AsNoTracking()
                .Where(manufacturer =>
                    manufacturer.ProviderName == providerName &&
                    manufacturer.VehicleType == vehicleType &&
                    manufacturer.ProviderCode == manufacturerCode)
                .Select(manufacturer => manufacturer.Name)
                .FirstOrDefaultAsync(cancellationToken);

            CacheNameIfPresent(manufacturerCacheKey, manufacturerName);
        }

        if (!hasModelName)
        {
            modelName = await dbContext.VehicleProviderModels
                .AsNoTracking()
                .Where(model =>
                    model.ProviderName == providerName &&
                    model.VehicleType == vehicleType &&
                    model.ProviderCode == modelCode &&
                    model.Manufacturer != null &&
                    model.Manufacturer.ProviderCode == manufacturerCode)
                .Select(model => model.Name)
                .FirstOrDefaultAsync(cancellationToken);

            CacheNameIfPresent(modelCacheKey, modelName);
        }

        if (!hasVersionName)
        {
            versionName = await dbContext.VehicleProviderVersions
                .AsNoTracking()
                .Where(version =>
                    version.ProviderName == providerName &&
                    version.VehicleType == vehicleType &&
                    version.ProviderCode == versionCode &&
                    version.Model != null &&
                    version.Model.ProviderCode == modelCode &&
                    version.Model.Manufacturer != null &&
                    version.Model.Manufacturer.ProviderCode == manufacturerCode)
                .Select(version => version.Name)
                .FirstOrDefaultAsync(cancellationToken);

            CacheNameIfPresent(versionCacheKey, versionName);
        }

        return new VehicleProviderCatalogNames(manufacturerName, modelName, versionName);
    }

    public async Task<IReadOnlyCollection<ManufacturerCatalogItemResponse>> ListManufacturersAsync(
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.VehicleProviderManufacturers
            .AsNoTracking()
            .Where(manufacturer =>
                manufacturer.ProviderName == DefaultProviderName &&
                manufacturer.VehicleType == DefaultVehicleType)
            .OrderBy(manufacturer => manufacturer.Name)
            .Select(manufacturer => new ManufacturerCatalogItemResponse(
                manufacturer.ProviderCode,
                manufacturer.Name,
                manufacturer.Id))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ModelCatalogItemResponse>> ListModelsAsync(
        Guid manufacturerId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.VehicleProviderModels
            .AsNoTracking()
            .Where(model =>
                model.ProviderName == DefaultProviderName &&
                model.VehicleType == DefaultVehicleType &&
                model.ManufacturerId == manufacturerId)
            .OrderBy(model => model.Name)
            .Select(model => new ModelCatalogItemResponse(
                model.ProviderCode,
                model.Name,
                model.Id))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<VersionCatalogItemResponse>> ListVersionsAsync(
        Guid modelId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.VehicleProviderVersions
            .AsNoTracking()
            .Where(version =>
                version.ProviderName == DefaultProviderName &&
                version.VehicleType == DefaultVehicleType &&
                version.ModelId == modelId)
            .OrderBy(version => version.Name)
            .Select(version => new VersionCatalogItemResponse(
                version.ProviderCode,
                version.Name))
            .ToArrayAsync(cancellationToken);
    }

    private bool TryGetCachedName(string cacheKey, out string? name)
    {
        return _cache.TryGetValue(cacheKey, out name);
    }

    private void CacheNameIfPresent(string cacheKey, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            _cache.Set(cacheKey, name);
        }
    }

    private static string BuildManufacturerCacheKey(
        string providerName,
        int vehicleType,
        string manufacturerCode)
    {
        return string.Join(
            "|",
            CacheKeyPrefix,
            "manufacturer",
            Normalize(providerName),
            vehicleType.ToString(CultureInfo.InvariantCulture),
            Normalize(manufacturerCode));
    }

    private static string BuildModelCacheKey(
        string providerName,
        int vehicleType,
        string manufacturerCode,
        string modelCode)
    {
        return string.Join(
            "|",
            CacheKeyPrefix,
            "model",
            Normalize(providerName),
            vehicleType.ToString(CultureInfo.InvariantCulture),
            Normalize(manufacturerCode),
            Normalize(modelCode));
    }

    private static string BuildVersionCacheKey(
        string providerName,
        int vehicleType,
        string manufacturerCode,
        string modelCode,
        string versionCode)
    {
        return string.Join(
            "|",
            CacheKeyPrefix,
            "version",
            Normalize(providerName),
            vehicleType.ToString(CultureInfo.InvariantCulture),
            Normalize(manufacturerCode),
            Normalize(modelCode),
            Normalize(versionCode));
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }
}

using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Buscarro.Infrastructure.Providers;

public sealed class VehicleProviderCatalogReadService(
    IDbContextFactory<BuscarroDbContext> dbContextFactory) : IVehicleProviderCatalogReadService
{
    private const string ProviderName = "Carzin";
    private const int VehicleType = 1;

    public async Task<IReadOnlyCollection<ManufacturerCatalogItemResponse>> ListManufacturersAsync(
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.VehicleProviderManufacturers
            .AsNoTracking()
            .Where(item => item.ProviderName == ProviderName && item.VehicleType == VehicleType)
            .OrderBy(item => item.Name)
            .Select(item => new ManufacturerCatalogItemResponse(item.ProviderCode, item.Name, item.Id))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ModelCatalogItemResponse>> ListModelsAsync(
        Guid manufacturerId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.VehicleProviderModels
            .AsNoTracking()
            .Where(item => item.ProviderName == ProviderName &&
                item.VehicleType == VehicleType && item.ManufacturerId == manufacturerId)
            .OrderBy(item => item.Name)
            .Select(item => new ModelCatalogItemResponse(item.ProviderCode, item.Name, item.Id))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<VersionCatalogItemResponse>> ListVersionsAsync(
        Guid modelId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.VehicleProviderVersions
            .AsNoTracking()
            .Where(item => item.ProviderName == ProviderName &&
                item.VehicleType == VehicleType && item.ModelId == modelId)
            .OrderBy(item => item.Name)
            .Select(item => new VersionCatalogItemResponse(item.ProviderCode, item.Name))
            .ToArrayAsync(cancellationToken);
    }
}

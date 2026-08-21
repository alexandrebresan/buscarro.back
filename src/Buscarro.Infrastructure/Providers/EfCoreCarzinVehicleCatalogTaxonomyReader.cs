using System.Data;
using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Buscarro.Infrastructure.Providers;

public sealed class EfCoreCarzinVehicleCatalogTaxonomyReader(BuscarroDbContext dbContext)
    : ICarzinVehicleCatalogTaxonomyReader
{
    private const string Provider = "Carzin";
    private const int VehicleType = 1;

    public async Task<CarzinVehicleCatalogTaxonomySnapshot> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            : null;

        var manufacturers = await dbContext.VehicleProviderManufacturers.AsNoTracking()
            .Where(value => value.ProviderName == Provider && value.VehicleType == VehicleType)
            .Select(value => new CarzinManufacturerTaxonomyRow(value.Id, value.ProviderCode, value.Name))
            .ToArrayAsync(cancellationToken);

        var models = await dbContext.VehicleProviderModels.AsNoTracking()
            .Where(value => value.ProviderName == Provider && value.VehicleType == VehicleType &&
                value.Manufacturer != null && value.Manufacturer.ProviderName == Provider &&
                value.Manufacturer.VehicleType == VehicleType)
            .Select(value => new CarzinModelTaxonomyRow(
                value.Id, value.ManufacturerId, value.ProviderCode, value.Name, value.Manufacturer!.ProviderCode))
            .ToArrayAsync(cancellationToken);

        var versions = await dbContext.VehicleProviderVersions.AsNoTracking()
            .Where(value => value.ProviderName == Provider && value.VehicleType == VehicleType &&
                value.Model != null && value.Model.ProviderName == Provider && value.Model.VehicleType == VehicleType &&
                value.Model.Manufacturer != null && value.Model.Manufacturer.ProviderName == Provider &&
                value.Model.Manufacturer.VehicleType == VehicleType)
            .Select(value => new CarzinVersionTaxonomyRow(
                value.ModelId, value.ProviderCode, value.Name, value.Model!.ProviderCode,
                value.Model.Manufacturer!.ProviderCode))
            .ToArrayAsync(cancellationToken);

        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return new(manufacturers, models, versions);
    }
}

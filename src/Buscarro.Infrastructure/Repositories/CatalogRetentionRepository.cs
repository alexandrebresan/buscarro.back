using Buscarro.Domain.Enums;
using Buscarro.Domain.Interfaces;
using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Buscarro.Infrastructure.Repositories;

public sealed class CatalogRetentionRepository(BuscarroDbContext dbContext) : ICatalogRetentionRepository
{
    public Task<int> DeleteVehicleRowsForFailedGenerationsAsync(CancellationToken cancellationToken)
        => dbContext.VehicleCatalog
            .Where(item => dbContext.CatalogGenerations.Any(generation => generation.Id == item.GenerationId && generation.Status == CatalogGenerationStatus.Failed))
            .ExecuteDeleteAsync(cancellationToken);

    public async Task<int> DeleteUnreferencedAsync(
        DateTimeOffset readyBeforeUtc,
        DateTimeOffset failedBeforeUtc,
        CancellationToken cancellationToken)
    {
        var removable = await dbContext.CatalogGenerations
            .Where(generation =>
                ((generation.Status == CatalogGenerationStatus.Ready && generation.UpdatedAtUtc < readyBeforeUtc) ||
                 (generation.Status == CatalogGenerationStatus.Failed && generation.FailedAtUtc < failedBeforeUtc)) &&
                !dbContext.ProviderCatalogCurrents.Any(pointer =>
                    pointer.CurrentGenerationId == generation.Id || pointer.PreviousGenerationId == generation.Id))
            .ToListAsync(cancellationToken);

        dbContext.CatalogGenerations.RemoveRange(removable);
        return removable.Count;
    }
}

using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;
using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Buscarro.Infrastructure.Repositories;

public sealed class CatalogGenerationRepository(BuscarroDbContext dbContext) : ICatalogGenerationRepository
{
    public Task AddAsync(CatalogGeneration generation, CancellationToken cancellationToken)
        => dbContext.CatalogGenerations.AddAsync(generation, cancellationToken).AsTask();

    public Task<CatalogGeneration?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => dbContext.CatalogGenerations.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);

    public Task<CatalogGeneration?> GetCurrentByProviderAsync(string provider, CancellationToken cancellationToken)
        => dbContext.CatalogGenerations.AsNoTracking().SingleOrDefaultAsync(
            value => value.Provider == provider && value.Status == Domain.Enums.CatalogGenerationStatus.Current,
            cancellationToken);

    public async Task<IReadOnlyCollection<CatalogGeneration>> ListLoadingBeforeAsync(DateTimeOffset createdBeforeUtc, CancellationToken cancellationToken)
        => await dbContext.CatalogGenerations
            .AsNoTracking()
            .Where(value => value.Status == Domain.Enums.CatalogGenerationStatus.Loading && value.CreatedAtUtc < createdBeforeUtc)
            .ToListAsync(cancellationToken);
}

using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;
using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Buscarro.Infrastructure.Repositories;

public sealed class CatalogPublicationRepository(BuscarroDbContext dbContext) : ICatalogPublicationRepository
{
    public Task<CatalogPublication?> GetLatestAsync(CancellationToken cancellationToken)
        => dbContext.CatalogPublications
            .OrderByDescending(value => value.Id)
            .FirstOrDefaultAsync(cancellationToken);
}

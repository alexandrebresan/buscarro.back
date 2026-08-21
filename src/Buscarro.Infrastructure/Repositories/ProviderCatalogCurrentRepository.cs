using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;
using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Buscarro.Infrastructure.Repositories;

public sealed class ProviderCatalogCurrentRepository(BuscarroDbContext dbContext) : IProviderCatalogCurrentRepository
{
    public Task<ProviderCatalogCurrent?> GetByProviderAsync(string provider, CancellationToken cancellationToken)
        => dbContext.ProviderCatalogCurrents.SingleOrDefaultAsync(value => value.Provider == provider, cancellationToken);
}

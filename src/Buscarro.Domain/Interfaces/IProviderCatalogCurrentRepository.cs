using Buscarro.Domain.Entities;

namespace Buscarro.Domain.Interfaces;

public interface IProviderCatalogCurrentRepository
{
    Task<ProviderCatalogCurrent?> GetByProviderAsync(string provider, CancellationToken cancellationToken);
}

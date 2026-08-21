using Buscarro.Domain.Entities;

namespace Buscarro.Domain.Interfaces;

public interface ICatalogGenerationRepository
{
    Task AddAsync(CatalogGeneration generation, CancellationToken cancellationToken);
    Task<CatalogGeneration?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<CatalogGeneration?> GetCurrentByProviderAsync(string provider, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CatalogGeneration>> ListLoadingBeforeAsync(DateTimeOffset createdBeforeUtc, CancellationToken cancellationToken);
}

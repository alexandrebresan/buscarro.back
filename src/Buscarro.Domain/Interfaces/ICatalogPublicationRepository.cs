using Buscarro.Domain.Entities;

namespace Buscarro.Domain.Interfaces;

public interface ICatalogPublicationRepository
{
    Task<CatalogPublication?> GetLatestAsync(CancellationToken cancellationToken);
}

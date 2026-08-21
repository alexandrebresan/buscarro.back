using Buscarro.Domain.Entities;
using Buscarro.Application.DTOs;

namespace Buscarro.Application.Interfaces;

public interface ICatalogPublisher
{
    Task<CatalogPublication> PublishAsync(
        int readyGenerationId,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken);
    Task<CatalogRollbackResult> RollbackAsync(string provider,int targetGenerationId,DateTimeOffset publishedAtUtc,CancellationToken cancellationToken)
        =>throw new NotSupportedException("Catalog rollback is not supported by this publisher.");
}

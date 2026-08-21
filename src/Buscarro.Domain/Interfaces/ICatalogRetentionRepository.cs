namespace Buscarro.Domain.Interfaces;

public interface ICatalogRetentionRepository
{
    Task<int> DeleteVehicleRowsForFailedGenerationsAsync(CancellationToken cancellationToken);
    Task<int> DeleteUnreferencedAsync(DateTimeOffset readyBeforeUtc, DateTimeOffset failedBeforeUtc, CancellationToken cancellationToken);
}

namespace Buscarro.Application.Interfaces;

public interface ICatalogGenerationFailureRecorder
{
    Task<bool> TryMarkLoadingAsFailedAsync(int generationId, string sanitizedError, DateTimeOffset failedAtUtc, CancellationToken cancellationToken);
}

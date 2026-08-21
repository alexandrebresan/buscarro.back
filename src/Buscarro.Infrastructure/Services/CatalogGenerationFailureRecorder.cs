using Buscarro.Application.Interfaces;
using Buscarro.Domain.Enums;
using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Buscarro.Infrastructure.Services;

public sealed class CatalogGenerationFailureRecorder(
    IDbContextFactory<BuscarroDbContext> factory,
    ILogger<CatalogGenerationFailureRecorder> logger) : ICatalogGenerationFailureRecorder
{
    public async Task<bool> TryMarkLoadingAsFailedAsync(int generationId, string sanitizedError, DateTimeOffset failedAtUtc, CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var generation = await context.CatalogGenerations.SingleOrDefaultAsync(value => value.Id == generationId, cancellationToken);
            if (generation?.Status != CatalogGenerationStatus.Loading) return false;
            generation.MarkFailed(sanitizedError, failedAtUtc);
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Catalog failure recording failed for generation {GenerationId}; failureType={FailureType}.",
                generationId,
                exception.GetType().Name);
            return false;
        }
    }
}

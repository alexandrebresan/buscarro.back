using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Services;
using Buscarro.Domain.Enums;
using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Buscarro.Infrastructure.Services;

public sealed class PostgresCatalogReadinessProbe(
    IDbContextFactory<BuscarroDbContext> contextFactory,
    IOptions<CatalogReadinessOptions> options,
    TimeProvider timeProvider) : ICatalogReadinessProbe
{
    public async Task<CatalogReadinessProbeResult> CheckAsync(CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(options.Value.DatabaseTimeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(linked.Token);
            var publication = await db.CatalogPublications.AsNoTracking()
                .OrderByDescending(value => value.Id).FirstOrDefaultAsync(linked.Token);
            if (publication is null) return Failure("publication_missing");

            var now = timeProvider.GetUtcNow();
            var age = now - publication.PublishedAtUtc;
            var ageSeconds = (long)Math.Floor(age.TotalSeconds);
            CatalogMetrics.SetRuntimePublication(publication.Id, age);
            if (publication.PublishedAtUtc > now + options.Value.AllowedClockSkew)
                return Failure("publication_from_future", publication.Id, ageSeconds, publication.ProviderCount);
            if (age > options.Value.MaximumCatalogAge)
                return Failure("catalog_stale", publication.Id, ageSeconds, publication.ProviderCount);
            if (publication.ProviderCount <= 0)
                return Failure("pointer_inconsistent", publication.Id, ageSeconds, publication.ProviderCount);

            var pointers = await db.ProviderCatalogCurrents.AsNoTracking().ToListAsync(linked.Token);
            if (pointers.Count != publication.ProviderCount)
                return Failure("pointer_inconsistent", publication.Id, ageSeconds, publication.ProviderCount);
            var current = await db.CatalogGenerations.AsNoTracking()
                .Where(value => value.Status == CatalogGenerationStatus.Current)
                .Select(value => new { value.Id, value.Provider, value.ValidItemCount })
                .ToListAsync(linked.Token);
            var consistent = current.Count == pointers.Count &&
                current.Sum(value => value.ValidItemCount) == publication.TotalCatalogCount &&
                pointers.All(pointer => current.Any(generation =>
                    generation.Id == pointer.CurrentGenerationId && generation.Provider == pointer.Provider));
            if (!consistent)
                return Failure("pointer_inconsistent", publication.Id, ageSeconds, publication.ProviderCount);

            CatalogMetrics.RecordHealth("ready");
            return new(true, "ready", publication.Id, ageSeconds, publication.ProviderCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Failure("database_unavailable");
        }
    }

    private static CatalogReadinessProbeResult Failure(
        string reason,
        int? publicationId = null,
        long? ageSeconds = null,
        int? providerCount = null)
    {
        CatalogMetrics.RecordHealth(reason);
        return new(false, reason, publicationId, ageSeconds, providerCount);
    }
}

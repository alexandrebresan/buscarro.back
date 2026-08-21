using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Interfaces;
using MediatR;
using System.Diagnostics;
using Buscarro.Application.Services;

namespace Buscarro.Application.Handlers;

public sealed class CleanupCatalogHandler(
    ICatalogGenerationRepository generations,
    ICatalogRetentionRepository retention,
    IUnitOfWork unitOfWork,
    IProviderSynchronizationLock synchronizationLock,
    TimeProvider timeProvider) : IRequestHandler<CleanupCatalogCommand, CatalogCleanupResult>
{
    public async Task<CatalogCleanupResult> Handle(CleanupCatalogCommand request, CancellationToken cancellationToken)
    {
        var started=Stopwatch.GetTimestamp();
        var candidates = await generations.ListLoadingBeforeAsync(request.AbandonedBeforeUtc, cancellationToken);
        var markedFailed = 0;
        foreach (var candidate in candidates)
        {
            await using var lease = await synchronizationLock.TryAcquireAsync(candidate.Provider, cancellationToken);
            if (lease is null) continue;
            var generation = await generations.GetByIdAsync(candidate.Id, cancellationToken);
            if (generation is null || generation.Status != Domain.Enums.CatalogGenerationStatus.Loading ||
                generation.CreatedAtUtc >= request.AbandonedBeforeUtc) continue;
            generation.MarkFailed("catalog_sync_abandoned", timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            markedFailed++;
        }
        var rows = await retention.DeleteVehicleRowsForFailedGenerationsAsync(cancellationToken);
        var removed = await retention.DeleteUnreferencedAsync(request.ReadyBeforeUtc, request.FailedBeforeUtc, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        CatalogMetrics.RecordCleanup(markedFailed+removed,rows,Stopwatch.GetElapsedTime(started));
        return new(markedFailed, rows, removed);
    }
}

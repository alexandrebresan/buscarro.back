using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using MediatR;
using Microsoft.Extensions.Options;

namespace Buscarro.CatalogSync.Worker;

public sealed class CatalogSynchronizationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<CatalogWorkerOptions> options,
    CatalogSyncHealthState health,
    TimeProvider timeProvider,
    ILogger<CatalogSynchronizationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(options.Value.InitialDelay, timeProvider, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var provider in options.Value.Providers)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SynchronizeProviderCatalogCommand(provider), stoppingToken);
                    var completedAt = timeProvider.GetUtcNow();
                    RecordResult(provider, result, completedAt, health, logger);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception exception)
                {
                    health.Failure(provider, timeProvider.GetUtcNow(), exception);
                    logger.LogError(exception, "Catalog synchronization failed for provider {Provider}.", provider);
                }
            }
            await Task.Delay(options.Value.SynchronizationInterval, timeProvider, stoppingToken);
        }
    }

    internal static void RecordResult(
        string provider,
        CatalogSynchronizationResult result,
        DateTimeOffset completedAt,
        CatalogSyncHealthState health,
        ILogger<CatalogSynchronizationWorker> logger)
    {
        if (!result.Acquired)
        {
            health.SkippedLockUnavailable(provider, completedAt);
            logger.LogInformation("Catalog synchronization skipped for {Provider}; reason={Reason}.", provider, "LockUnavailable");
            return;
        }

        health.Success(provider, completedAt);
        logger.LogInformation(
            "Catalog synchronization completed for {Provider}; generation={GenerationId}, valid={ValidItems}.",
            provider, result.GenerationId, result.ValidItemCount);
    }
}

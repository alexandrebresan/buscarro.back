using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using MediatR;
using Microsoft.Extensions.Options;

namespace Buscarro.CatalogSync.Worker;

public sealed class CatalogCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<CatalogWorkerOptions> options,
    CatalogSynchronizationOptions synchronizationOptions,
    TimeProvider timeProvider,
    ILogger<CatalogCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(options.Value.CleanupInterval, timeProvider, stoppingToken);
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var now = timeProvider.GetUtcNow();
                var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    CreateCommand(now, synchronizationOptions), stoppingToken);
                logger.LogInformation("Catalog cleanup completed: {Generations} generations and {Rows} rows removed.", result.GenerationsDeleted, result.VehicleRowsDeleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogError(exception, "Catalog cleanup failed."); }
        }
    }

    internal static CleanupCatalogCommand CreateCommand(DateTimeOffset now, CatalogSynchronizationOptions options) => new(
        now - (options.Timeout + options.AbandonedLoadingGrace),
        now - options.ReadyRetention,
        now - options.FailedRetention);
}

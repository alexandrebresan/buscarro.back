using Buscarro.Application.Commands;
using MediatR;

namespace Buscarro.Api.Configurations;

public sealed class AuthCleanupWorker(IServiceScopeFactory scopes, ILogger<AuthCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));
        try
        {
            // The first tick is delayed so migration deployment can finish before cleanup.
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<IMediator>()
                        .Send(new CleanupAuthSessionsCommand(), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception) { logger.LogError(exception, "Authentication session cleanup failed."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}

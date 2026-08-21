using Buscarro.CatalogSync.Worker;
using Buscarro.Infrastructure.Providers;
using Buscarro.Application.DTOs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Buscarro.Tests;

public sealed class CatalogWorkerTests
{
    [Fact]
    public void Health_preserves_last_success_and_failure_independently()
    {
        var health = new CatalogSyncHealthState();
        var failedAt = new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);
        var succeededAt = failedAt.AddMinutes(5);

        health.Failure("Carzin", failedAt, new HttpRequestException("sensitive provider response"));
        health.Success("Carzin", succeededAt);
        var latestFailure = succeededAt.AddMinutes(5);
        health.Failure("Carzin", latestFailure, new InvalidOperationException("do not expose"));

        var snapshot = health.Providers["Carzin"];
        Assert.Equal(latestFailure, snapshot.LastFailureUtc);
        Assert.Equal(succeededAt, snapshot.LastSuccessUtc);
        Assert.Equal(nameof(InvalidOperationException), snapshot.FailureCategory);
    }

    [Fact]
    public void Worker_assembly_contains_exactly_two_background_services()
    {
        var services = typeof(CatalogSynchronizationWorker).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(BackgroundService).IsAssignableFrom(type))
            .ToArray();

        Assert.Equal(2, services.Length);
        Assert.Contains(typeof(CatalogSynchronizationWorker), services);
        Assert.Contains(typeof(CatalogCleanupWorker), services);
        Assert.All(services, service => Assert.Contains(
            service.GetConstructors().Single().GetParameters(),
            parameter => parameter.ParameterType == typeof(IServiceScopeFactory)));
    }

    [Fact]
    public void Lock_unavailable_is_a_distinct_skipped_attempt_and_not_a_success()
    {
        var health = new CatalogSyncHealthState();
        var success = new DateTimeOffset(2026, 8, 15, 8, 0, 0, TimeSpan.Zero);
        var skipped = success.AddMinutes(5);
        health.Success("Carzin", success);

        CatalogSynchronizationWorker.RecordResult(
            "Carzin",
            new CatalogSynchronizationResult("Carzin", false, null, 0, 0, 0, 0),
            skipped,
            health,
            NullLogger<CatalogSynchronizationWorker>.Instance);

        var snapshot = health.Providers["Carzin"];
        Assert.Equal(success, snapshot.LastSuccessUtc);
        Assert.Equal(skipped, snapshot.LastAttemptUtc);
        Assert.Equal("SkippedLockUnavailable", snapshot.LastOutcome);
    }

    [Fact]
    public void Retry_options_require_exactly_three_retries_and_consistent_delays()
    {
        Assert.True(new CarzinCatalogSourceOptions().IsValidForWorker());
        Assert.False(new CarzinCatalogSourceOptions { RetryCount = 2 }.IsValidForWorker());
        Assert.False(new CarzinCatalogSourceOptions
        {
            RetryBaseDelay = TimeSpan.FromSeconds(2), RetryMaxDelay = TimeSpan.FromSeconds(1)
        }.IsValidForWorker());
    }

    [Fact]
    public void Cleanup_cutoff_combines_full_sync_timeout_with_abandoned_grace()
    {
        var now = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        var command = CatalogCleanupWorker.CreateCommand(now, new CatalogSynchronizationOptions
        {
            Timeout = TimeSpan.FromHours(2), AbandonedLoadingGrace = TimeSpan.FromMinutes(15),
            ReadyRetention = TimeSpan.FromDays(1), FailedRetention = TimeSpan.FromDays(7)
        });
        Assert.Equal(now.AddHours(-2).AddMinutes(-15), command.AbandonedBeforeUtc);
    }
}

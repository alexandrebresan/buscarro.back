using Buscarro.CatalogSync.Worker;
using Buscarro.Infrastructure.Providers;
using Buscarro.Application.DTOs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MediatR;
using Buscarro.Application.Queries;
using Buscarro.Application.Commands;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Buscarro.Tests;

public sealed class CatalogWorkerTests
{
    [Fact]
    public void Versioned_worker_appsettings_bind_to_valid_provider_intervals()
    {
        var repository = FindRepositoryRoot();
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(repository, "src", "Buscarro.CatalogSync.Worker", "appsettings.json"), optional: false)
            .Build();
        var options = configuration.GetSection(CatalogWorkerOptions.SectionName).Get<CatalogWorkerOptions>();

        Assert.NotNull(options);
        Assert.Equal(["Carzin", "AutoDev"], options.Providers);
        Assert.Equal(TimeSpan.FromDays(1), options.SynchronizationInterval);
        Assert.Equal(TimeSpan.FromDays(2), options.GetSynchronizationInterval("AutoDev"));
        Assert.True(options.IsValid());
    }
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

    [Fact]
    public async Task Not_due_provider_does_not_send_sync_command_or_mutate_health()
    {
        using var stopping = new CancellationTokenSource();
        var sender = new NotDueSender(stopping);
        using var provider = new ServiceCollection().AddSingleton<ISender>(sender).BuildServiceProvider();
        var health = new CatalogSyncHealthState();
        var worker = new CatalogSynchronizationWorker(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new CatalogWorkerOptions { Providers = ["AutoDev"], InitialDelay = TimeSpan.Zero, SynchronizationInterval = TimeSpan.FromDays(1) }),
            health, TimeProvider.System, NullLogger<CatalogSynchronizationWorker>.Instance);
        var execute = typeof(CatalogSynchronizationWorker).GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var task = (Task)execute.Invoke(worker, [stopping.Token])!;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.False(sender.SyncSent); Assert.Empty(health.Providers);
    }

    [Fact]
    public void Provider_intervals_override_only_the_named_provider()
    {
        var options = new CatalogWorkerOptions
        {
            Providers = ["Carzin", "AutoDev"], SynchronizationInterval = TimeSpan.FromDays(1),
            ProviderSynchronizationIntervals = new() { ["AutoDev"] = TimeSpan.FromDays(2) }
        };
        Assert.True(options.IsValid());
        Assert.Equal(TimeSpan.FromDays(1), options.GetSynchronizationInterval("carzin"));
        Assert.Equal(TimeSpan.FromDays(2), options.GetSynchronizationInterval("AUTODEV"));
    }

    private sealed class NotDueSender(CancellationTokenSource stopping) : ISender
    {
        public bool SyncSent { get; private set; }
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is GetProviderSynchronizationDueQuery) { stopping.Cancel(); return Task.FromResult((TResponse)(object)false); }
            if (request is SynchronizeProviderCatalogCommand) SyncSent = true;
            throw new Xunit.Sdk.XunitException("Unexpected request.");
        }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new Xunit.Sdk.XunitException("Unexpected request.");
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new Xunit.Sdk.XunitException("Unexpected request.");
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new Xunit.Sdk.XunitException("Unexpected request.");
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new Xunit.Sdk.XunitException("Unexpected request.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Buscarro.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}

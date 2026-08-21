using System.Collections.Concurrent;

namespace Buscarro.CatalogSync.Worker;

public sealed class CatalogSyncHealthState
{
    private readonly ConcurrentDictionary<string, CatalogSyncHealthSnapshot> _providers = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, CatalogSyncHealthSnapshot> Providers => _providers;
    public void Success(string provider, DateTimeOffset atUtc) => _providers.AddOrUpdate(
        provider,
        _ => new(atUtc, null, null, atUtc, "Succeeded"),
        (_, current) => current with { LastSuccessUtc = atUtc, LastAttemptUtc = atUtc, LastOutcome = "Succeeded" });
    public void Failure(string provider, DateTimeOffset atUtc, Exception exception) => _providers.AddOrUpdate(
        provider,
        _ => new(null, atUtc, exception.GetType().Name, atUtc, "Failed"),
        (_, current) => current with
        {
            LastFailureUtc = atUtc, FailureCategory = exception.GetType().Name, LastAttemptUtc = atUtc, LastOutcome = "Failed"
        });
    public void SkippedLockUnavailable(string provider, DateTimeOffset atUtc) => _providers.AddOrUpdate(
        provider,
        _ => new(null, null, null, atUtc, "SkippedLockUnavailable"),
        (_, current) => current with { LastAttemptUtc = atUtc, LastOutcome = "SkippedLockUnavailable" });
}

public sealed record CatalogSyncHealthSnapshot(
    DateTimeOffset? LastSuccessUtc,
    DateTimeOffset? LastFailureUtc,
    string? FailureCategory,
    DateTimeOffset LastAttemptUtc,
    string LastOutcome);

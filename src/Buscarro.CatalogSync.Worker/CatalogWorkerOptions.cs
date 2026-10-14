namespace Buscarro.CatalogSync.Worker;

public sealed class CatalogWorkerOptions
{
    public const string SectionName = "CatalogWorker";
    public string[] Providers { get; init; } = [];
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan SynchronizationInterval { get; init; } = TimeSpan.FromHours(6);
    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromHours(24);
    public Dictionary<string, TimeSpan> ProviderSynchronizationIntervals { get; init; } = [];

    public TimeSpan GetSynchronizationInterval(string provider)
        => ProviderSynchronizationIntervals.FirstOrDefault(pair => string.Equals(pair.Key, provider, StringComparison.OrdinalIgnoreCase)).Value is var interval && interval > TimeSpan.Zero
            ? interval : SynchronizationInterval;

    public bool IsValid() => Providers.Length > 0 && Providers.All(value => !string.IsNullOrWhiteSpace(value)) &&
        Providers.Distinct(StringComparer.OrdinalIgnoreCase).Count() == Providers.Length &&
        InitialDelay >= TimeSpan.Zero && SynchronizationInterval > TimeSpan.Zero && CleanupInterval > TimeSpan.Zero &&
        ProviderSynchronizationIntervals.Keys.All(key => !string.IsNullOrWhiteSpace(key) && Providers.Contains(key, StringComparer.OrdinalIgnoreCase)) &&
        ProviderSynchronizationIntervals.Values.All(value => value > TimeSpan.Zero) &&
        ProviderSynchronizationIntervals.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() == ProviderSynchronizationIntervals.Count;
}

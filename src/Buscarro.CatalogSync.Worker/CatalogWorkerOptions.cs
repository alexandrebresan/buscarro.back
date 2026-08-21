namespace Buscarro.CatalogSync.Worker;

public sealed class CatalogWorkerOptions
{
    public const string SectionName = "CatalogWorker";
    public string[] Providers { get; init; } = ["Carzin"];
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan SynchronizationInterval { get; init; } = TimeSpan.FromHours(6);
    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromHours(24);

    public bool IsValid() => Providers.Length > 0 && Providers.All(value => !string.IsNullOrWhiteSpace(value)) &&
        InitialDelay >= TimeSpan.Zero && SynchronizationInterval > TimeSpan.Zero && CleanupInterval > TimeSpan.Zero;
}

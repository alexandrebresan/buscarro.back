namespace Buscarro.Infrastructure.Providers;

public sealed class CarzinCatalogSourceOptions
{
    public int RetryCount { get; init; } = 3;
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan RetryMaxDelay { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(60);
    public int CircuitFailureThreshold { get; init; } = 5;
    public TimeSpan CircuitBreakDuration { get; init; } = TimeSpan.FromMinutes(1);

    public bool IsValidForWorker() => RetryCount == 3 && RetryBaseDelay > TimeSpan.Zero &&
        RetryMaxDelay >= RetryBaseDelay && RequestTimeout > TimeSpan.Zero && CircuitFailureThreshold > 0 &&
        CircuitBreakDuration > TimeSpan.Zero;
}

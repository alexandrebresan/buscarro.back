namespace Buscarro.Infrastructure.Providers;

public sealed class AutoDevCatalogSourceOptions
{
    public int PageSize { get; init; } = 20;
    public int MaximumSuccessfulPages { get; init; } = 50;
    public int MaximumHttpAttempts { get; init; } = 60;
    public int MaximumRequestsPerSecond { get; init; } = 5;
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public int RetryCount { get; init; } = 3;

    public bool IsValidForWorker() => PageSize == 20 && MaximumSuccessfulPages == 50 && MaximumHttpAttempts == 60 &&
        MaximumRequestsPerSecond is >= 1 and <= 5 && RetryCount == 3 && RequestTimeout > TimeSpan.Zero;
}

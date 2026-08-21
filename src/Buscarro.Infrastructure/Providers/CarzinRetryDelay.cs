using System.Net.Http.Headers;

namespace Buscarro.Infrastructure.Providers;

public interface ICarzinRetryDelay
{
    Task DelayAsync(int attempt, RetryConditionHeaderValue? retryAfter, CancellationToken cancellationToken);
}

public sealed class CarzinRetryDelay(
    CarzinCatalogSourceOptions options,
    TimeProvider timeProvider,
    Func<int>? jitterMilliseconds = null) : ICarzinRetryDelay
{
    private readonly Func<int> _jitterMilliseconds = jitterMilliseconds ?? (() => Random.Shared.Next(0, 251));

    public Task DelayAsync(int attempt, RetryConditionHeaderValue? retryAfter, CancellationToken cancellationToken)
        => Task.Delay(CalculateDelay(attempt, retryAfter), timeProvider, cancellationToken);

    internal TimeSpan CalculateDelay(int attempt, RetryConditionHeaderValue? retryAfter)
    {
        var serverDelay = retryAfter?.Delta ?? (retryAfter?.Date - timeProvider.GetUtcNow());
        var exponential = TimeSpan.FromMilliseconds(Math.Min(options.RetryMaxDelay.TotalMilliseconds,
            options.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, attempt)));
        var jittered = exponential + TimeSpan.FromMilliseconds(_jitterMilliseconds());
        return serverDelay is not null && serverDelay > jittered ? serverDelay.Value : jittered;
    }
}

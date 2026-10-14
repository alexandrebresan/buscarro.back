namespace Buscarro.Infrastructure.Providers;

public sealed class AutoDevRequestPacer(TimeProvider timeProvider, AutoDevCatalogSourceOptions options) : IAutoDevRequestPacer
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _nextStart = DateTimeOffset.MinValue;

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow();
            if (_nextStart > now) await Task.Delay(_nextStart - now, timeProvider, cancellationToken);
            _nextStart = timeProvider.GetUtcNow() + TimeSpan.FromSeconds(1d / options.MaximumRequestsPerSecond);
        }
        finally { _gate.Release(); }
    }
}

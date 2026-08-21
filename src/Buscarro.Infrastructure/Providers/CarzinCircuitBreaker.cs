namespace Buscarro.Infrastructure.Providers;

public sealed class CarzinCircuitBreaker(TimeProvider timeProvider, CarzinCatalogSourceOptions options)
{
    private readonly object _gate = new();
    private int _failures;
    private DateTimeOffset? _openUntil;
    private bool _probeInProgress;

    public IDisposable Enter()
    {
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            if (_openUntil is not null && now < _openUntil) throw new InvalidOperationException("carzin_circuit_open");
            if (_openUntil is not null)
            {
                if (_probeInProgress) throw new InvalidOperationException("carzin_circuit_open");
                _probeInProgress = true;
            }
            return new Probe(this);
        }
    }

    public void Success()
    {
        lock (_gate) { _failures = 0; _openUntil = null; _probeInProgress = false; }
    }

    public void Failure()
    {
        lock (_gate)
        {
            _probeInProgress = false;
            if (++_failures >= options.CircuitFailureThreshold) _openUntil = timeProvider.GetUtcNow() + options.CircuitBreakDuration;
        }
    }

    private sealed class Probe(CarzinCircuitBreaker owner) : IDisposable
    {
        public void Dispose() { lock (owner._gate) owner._probeInProgress = false; }
    }
}

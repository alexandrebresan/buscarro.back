namespace Buscarro.Application.Interfaces;

public interface IProviderSynchronizationLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(string provider, CancellationToken cancellationToken);
}

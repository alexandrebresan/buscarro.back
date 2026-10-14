namespace Buscarro.Infrastructure.Providers;

public interface IAutoDevRequestPacer
{
    Task WaitAsync(CancellationToken cancellationToken);
}

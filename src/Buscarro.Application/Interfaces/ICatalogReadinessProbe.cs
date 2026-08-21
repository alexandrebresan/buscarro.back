using Buscarro.Application.DTOs;

namespace Buscarro.Application.Interfaces;

public interface ICatalogReadinessProbe
{
    Task<CatalogReadinessProbeResult> CheckAsync(CancellationToken cancellationToken);
}

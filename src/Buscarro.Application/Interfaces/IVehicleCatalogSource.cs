using Buscarro.Application.DTOs;

namespace Buscarro.Application.Interfaces;

public interface IVehicleCatalogSource
{
    string Provider { get; }
    Task<CatalogSourcePage> ReadPageAsync(int page, int pageSize, CancellationToken cancellationToken);
}

using Buscarro.Application.DTOs;

namespace Buscarro.Application.Interfaces;

public interface IVehicleCatalogItemNormalizer
{
    string Provider { get; }
    Task PrepareAsync(CancellationToken cancellationToken);
    CatalogItemNormalizationResult Normalize(CatalogSourceVehicle source);
}

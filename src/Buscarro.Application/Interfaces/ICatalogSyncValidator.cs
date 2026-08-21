using Buscarro.Application.DTOs;

namespace Buscarro.Application.Interfaces;

public interface ICatalogSyncValidator
{
    CatalogItemValidationResult ValidateAndNormalize(CatalogSourceVehicle source);
    string? ValidateSnapshot(int validItemCount, int? previousItemCount);
}

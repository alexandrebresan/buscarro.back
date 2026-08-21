namespace Buscarro.Application.DTOs;

public sealed record CatalogCleanupResult(int AbandonedMarkedFailed, int VehicleRowsDeleted, int GenerationsDeleted);

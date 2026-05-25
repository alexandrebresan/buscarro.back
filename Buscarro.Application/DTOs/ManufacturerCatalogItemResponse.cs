namespace Buscarro.Application.DTOs;

public sealed record ManufacturerCatalogItemResponse(
    string ProviderCode,
    string Name,
    Guid ManufacturerId);

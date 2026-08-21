namespace Buscarro.Application.DTOs;

public sealed record ModelCatalogItemResponse(
    string ProviderCode,
    string Name,
    Guid ModelId);

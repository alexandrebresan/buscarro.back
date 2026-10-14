namespace Buscarro.Application.DTOs;

public sealed record CatalogSourcePage(
    int RequestedPage,
    int? ReportedPage,
    int? CurrentResults,
    int? TotalPages,
    int? TotalResults,
    IReadOnlyCollection<CatalogSourceVehicle> Items,
    bool? HasNext = null);

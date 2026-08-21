namespace Buscarro.Application.DTOs;

public sealed record NormalizedVehicleCatalogSearchCriteria(
    string? Query,
    IReadOnlyList<string> Providers,
    IReadOnlyList<string> States,
    IReadOnlyList<string> Cities,
    IReadOnlyList<string> Manufacturers,
    IReadOnlyList<string> Models,
    IReadOnlyList<string> Versions,
    IReadOnlyList<string> Colors,
    IReadOnlyList<string> Fuels,
    IReadOnlyList<string> Gears,
    decimal? PriceMin,
    decimal? PriceMax,
    int? YearMin,
    int? YearMax,
    string? Order,
    int Page,
    int PageSize)
{
}

namespace Buscarro.Application.DTOs;

public sealed record NormalizedVehicleCatalogSearchCriteria(
    string? Query,
    IReadOnlyList<string> Providers,
    IReadOnlyList<string> Countries,
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
    // Compatibilidade com o construtor anterior,
    // utilizado por testes e possíveis chamadas antigas.
    //
    // Antes da implementação do filtro de País,
    // o objeto não possuía a propriedade Countries.
    public NormalizedVehicleCatalogSearchCriteria(
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
        : this(
            Query,
            Providers,
            Array.Empty<string>(),
            States,
            Cities,
            Manufacturers,
            Models,
            Versions,
            Colors,
            Fuels,
            Gears,
            PriceMin,
            PriceMax,
            YearMin,
            YearMax,
            Order,
            Page,
            PageSize)
    {
    }
}
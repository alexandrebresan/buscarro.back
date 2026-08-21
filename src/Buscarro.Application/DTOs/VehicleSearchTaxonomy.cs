namespace Buscarro.Application.DTOs;

public sealed record VehicleSearchTaxonomy(
    IReadOnlyList<string> Colors,
    IReadOnlyList<string> Gears,
    IReadOnlyList<string> Fuels);
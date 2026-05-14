namespace Buscarro.Application.DTOs;

public sealed class VehicleSearchCriteria
{
    public string? Query { get; set; }
    public string? Order { get; set; }
    public int Page { get; set; } = 1;
    public int Results { get; set; } = 20;
    public string? State { get; set; }
    public int? City { get; set; }
    public int? Manufacturer { get; set; }
    public int? Model { get; set; }
    public int? Version { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public int? YearMin { get; set; }
    public int? YearMax { get; set; }
    public int? Color { get; set; }
    public int? Gear { get; set; }
    public int? Fuel { get; set; }
}

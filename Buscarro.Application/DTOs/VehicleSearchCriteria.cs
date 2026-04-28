namespace Buscarro.Application.DTOs;

public sealed class VehicleSearchCriteria
{
    public int Type { get; set; } = 1;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Version { get; set; }
    public string? Color { get; set; }
    public string? Fuel { get; set; }
    public string? Transmission { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public int? YearMin { get; set; }
    public int? YearMax { get; set; }
    public string? Order { get; set; }
    public int Page { get; set; } = 1;
    public int Results { get; set; } = 20;
    public int? SellerType { get; set; }
}

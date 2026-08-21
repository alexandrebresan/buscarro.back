namespace Buscarro.Application.DTOs;

public sealed class VehicleCatalogSearchRequest
{
    public string? Query { get; set; }
    public List<string> Provider { get; set; } = [];
    public List<string> State { get; set; } = [];
    public List<string> City { get; set; } = [];
    public List<string> Manufacturer { get; set; } = [];
    public List<string> Model { get; set; } = [];
    public List<string> Version { get; set; } = [];
    public List<string> Color { get; set; } = [];
    public List<string> Fuel { get; set; } = [];
    public List<string> Gear { get; set; } = [];
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public int? YearMin { get; set; }
    public int? YearMax { get; set; }
    public string? Order { get; set; }
    public int Page { get; set; } = 1;
    public int? PageSize { get; set; }
    public int? Results { get; set; }
}

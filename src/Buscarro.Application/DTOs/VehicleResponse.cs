namespace Buscarro.Application.DTOs;

public sealed class VehicleResponse
{
    public string ExternalId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Version { get; set; }
    public int? Year { get; set; }
    public int? Mileage { get; set; }
    public int? Doors { get; set; }
    public bool? Armored { get; set; }
    public string? Fuel { get; set; }
    public string? Transmission { get; set; }
    public string? Color { get; set; }
    public decimal? Price { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Url { get; set; }
    public string? ImageUrl { get; set; }
}

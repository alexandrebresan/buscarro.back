namespace Buscarro.Application.DTOs;

public sealed class FavoriteVehicleResponse
{
    public Guid Id { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public int Year { get; init; }
    public int Mileage { get; init; }
    public int? Doors { get; init; }
    public bool? Armored { get; init; }
    public string Fuel { get; init; } = string.Empty;
    public string Transmission { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}

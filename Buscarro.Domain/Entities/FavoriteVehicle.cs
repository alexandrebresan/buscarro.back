namespace Buscarro.Domain.Entities;

public class FavoriteVehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Mileage { get; set; }
    public string Fuel { get; set; } = string.Empty;
    public string Transmission { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

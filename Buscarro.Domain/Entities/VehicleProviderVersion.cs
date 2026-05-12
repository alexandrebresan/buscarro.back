namespace Buscarro.Domain.Entities;

public class VehicleProviderVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ModelId { get; set; }
    public VehicleProviderModel? Model { get; set; }
    public string ProviderName { get; set; } = "Carzin";
    public string ProviderCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int VehicleType { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

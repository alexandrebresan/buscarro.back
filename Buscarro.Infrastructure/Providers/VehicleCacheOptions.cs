namespace Buscarro.Infrastructure.Providers;

public sealed class VehicleCacheOptions
{
    public int DurationMinutes { get; set; } = 5;
    public Dictionary<string, int> ProviderDurationMinutes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

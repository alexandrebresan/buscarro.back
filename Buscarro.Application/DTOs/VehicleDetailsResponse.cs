namespace Buscarro.Application.DTOs;

public sealed class VehicleDetailsResponse
{
    public IReadOnlyCollection<string> Observations { get; set; } = [];
    public IReadOnlyCollection<string> Optionals { get; set; } = [];
    public IReadOnlyCollection<VehicleDetailsImageResponse> Images { get; set; } = [];
}

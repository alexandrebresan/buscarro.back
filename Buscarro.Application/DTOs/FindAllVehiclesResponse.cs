namespace Buscarro.Application.DTOs;

public sealed class FindAllVehiclesResponse
{
    public IReadOnlyCollection<VehicleResponse> Vehicles { get; init; } = [];
    public IReadOnlyCollection<ProviderStatusResponse> Providers { get; init; } = [];
}

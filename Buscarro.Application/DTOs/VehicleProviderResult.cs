using Buscarro.Domain.Entities;

namespace Buscarro.Application.DTOs;

public sealed class VehicleProviderResult
{
    public IReadOnlyCollection<Vehicle> Vehicles { get; init; } = [];
    public int? CurrentResults { get; init; }
    public int? TotalResults { get; init; }
    public int? CurrentPage { get; init; }
    public int? TotalPages { get; init; }
}

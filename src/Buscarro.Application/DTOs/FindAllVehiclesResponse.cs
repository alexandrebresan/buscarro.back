namespace Buscarro.Application.DTOs;

public sealed class FindAllVehiclesResponse
{
    public IReadOnlyCollection<VehicleResponse> Vehicles { get; init; } = [];
    public IReadOnlyCollection<ProviderStatusResponse> Providers { get; init; } = [];
    public int? PublicationId { get; init; }
    public DateTimeOffset? CatalogUpdatedAtUtc { get; init; }
    public int? TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int? TotalPages { get; init; }
    public int Results { get; init; }
}

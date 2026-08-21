namespace Buscarro.Infrastructure.Providers;

public sealed record CarzinManufacturerTaxonomyRow(Guid Id, string Code, string Name);
public sealed record CarzinModelTaxonomyRow(Guid Id, Guid ManufacturerId, string Code, string Name, string ManufacturerCode);
public sealed record CarzinVersionTaxonomyRow(Guid ModelId, string Code, string Name, string ModelCode, string ManufacturerCode);
public sealed record CarzinVehicleCatalogTaxonomySnapshot(
    IReadOnlyCollection<CarzinManufacturerTaxonomyRow> Manufacturers,
    IReadOnlyCollection<CarzinModelTaxonomyRow> Models,
    IReadOnlyCollection<CarzinVersionTaxonomyRow> Versions);

public interface ICarzinVehicleCatalogTaxonomyReader
{
    Task<CarzinVehicleCatalogTaxonomySnapshot> ReadSnapshotAsync(CancellationToken cancellationToken);
}

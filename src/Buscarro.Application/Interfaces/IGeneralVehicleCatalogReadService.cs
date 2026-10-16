namespace Buscarro.Application.Interfaces;

public interface IGeneralVehicleCatalogReadService
{
    Task<IReadOnlyCollection<string>> ListManufacturersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> ListModelsAsync(string? manufacturer, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> ListVersionsAsync(string model, string? manufacturer, CancellationToken cancellationToken);
}

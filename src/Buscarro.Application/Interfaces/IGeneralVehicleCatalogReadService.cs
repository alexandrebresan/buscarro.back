namespace Buscarro.Application.Interfaces;

public interface IGeneralVehicleCatalogReadService
{
    Task<IReadOnlyCollection<string>> ListCountriesAsync(
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<string>>([]);

    Task<IReadOnlyCollection<string>> ListStatesAsync(
        IReadOnlyCollection<string> countries,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<string>>([]);

    Task<IReadOnlyCollection<string>> ListCitiesAsync(
        IReadOnlyCollection<string> countries,
        IReadOnlyCollection<string> states,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<string>>([]);

    Task<IReadOnlyCollection<string>> ListManufacturersAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> ListModelsAsync(
        string? manufacturer,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> ListVersionsAsync(
        string model,
        string? manufacturer,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> ListFuelsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> ListFuelsAsync(
        string? manufacturer,
        string? model,
        CancellationToken cancellationToken)
        => ListFuelsAsync(cancellationToken);

    Task<IReadOnlyCollection<string>> ListTransmissionsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> ListTransmissionsAsync(
        string? manufacturer,
        string? model,
        CancellationToken cancellationToken)
        => ListTransmissionsAsync(cancellationToken);

    Task<IReadOnlyCollection<string>> ListColorsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> ListColorsAsync(
        string? manufacturer,
        string? model,
        CancellationToken cancellationToken)
        => ListColorsAsync(cancellationToken);
}
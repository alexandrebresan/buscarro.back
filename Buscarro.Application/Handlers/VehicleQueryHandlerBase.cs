using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;

namespace Buscarro.Application.Handlers;

public abstract class VehicleQueryHandlerBase
{
    private readonly IEnumerable<IVehicleProvider> _providers;

    protected VehicleQueryHandlerBase(IEnumerable<IVehicleProvider> providers)
    {
        _providers = providers;
    }

    protected async Task<IReadOnlyCollection<Vehicle>> LoadVehiclesAsync(CancellationToken cancellationToken)
    {
        var tasks = _providers.Select(provider => LoadProviderVehiclesAsync(provider, cancellationToken));
        var results = await Task.WhenAll(tasks);

        return results
            .SelectMany(vehicles => vehicles)
            .OrderBy(vehicle => vehicle.Brand)
            .ThenBy(vehicle => vehicle.Model)
            .ThenByDescending(vehicle => vehicle.Year)
            .ToArray();
    }

    private async Task<IReadOnlyCollection<Vehicle>> LoadProviderVehiclesAsync(
        IVehicleProvider provider,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.GetVehiclesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Erro ao consultar o provider {provider.ProviderName}: {ex.Message}");
            return [];
        }
    }
}

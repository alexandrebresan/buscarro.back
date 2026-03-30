using Buscarro.Domain.Entities;

namespace Buscarro.Application.Interfaces;

public interface IVehicleProvider
{
    string ProviderName { get; }

    Task<IReadOnlyCollection<Vehicle>> GetVehiclesAsync(CancellationToken cancellationToken);
}

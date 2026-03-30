using Buscarro.Domain.Entities;

namespace Buscarro.Application.Interfaces;

public interface IPokemonApiService
{
    Task<Pokemon?> GetPokemonAsync(string name, CancellationToken cancellationToken);
}

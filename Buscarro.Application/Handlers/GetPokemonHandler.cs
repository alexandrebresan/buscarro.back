using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class GetPokemonHandler : IRequestHandler<GetPokemonQuery, IReadOnlyCollection<PokemonResponse>>
{
    private readonly IEnumerable<IPokemonApiService> _services;

    public GetPokemonHandler(IEnumerable<IPokemonApiService> services)
    {
        _services = services;
    }

    public async Task<IReadOnlyCollection<PokemonResponse>> Handle(GetPokemonQuery request, CancellationToken cancellationToken)
    {
        var tasks = _services.Select(async service =>
        {
            try
            {
                return await service.GetPokemonAsync(request.Name, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"Erro ao chamar {service.GetType().Name} para o pokemon '{request.Name}': {ex.Message}");
                return null;
            }
        });

        var results = await Task.WhenAll(tasks);

        return results
            .Where(result => result is not null)
            .Select(result => new PokemonResponse
            {
                From = result!.From,
                Name = result.Name,
                BaseExperience = result.BaseExperience,
                Height = result.Height,
                Weight = result.Weight,
                Abilities = result.Abilities
            })
            .ToArray();
    }
}

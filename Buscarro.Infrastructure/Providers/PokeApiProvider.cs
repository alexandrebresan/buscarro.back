using System.Net;
using System.Net.Http.Json;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Contracts;

namespace Buscarro.Infrastructure.Providers;

public class PokeApiProvider : IPokemonApiService
{
    private readonly HttpClient _httpClient;

    public PokeApiProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Pokemon?> GetPokemonAsync(string name, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            $"https://pokeapi.co/api/v2/pokemon/{Uri.EscapeDataString(name)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PokeApiResponse>(cancellationToken: cancellationToken);

        if (payload is null)
        {
            throw new InvalidOperationException("A resposta da PokeAPI veio vazia.");
        }

        return new Pokemon
        {
            From = "PokeAPI",
            Name = payload.Name,
            BaseExperience = payload.BaseExperience,
            Height = payload.Height,
            Weight = payload.Weight,
            Abilities = payload.Abilities.Select(ability => ability.Ability.Name).ToList()
        };
    }
}

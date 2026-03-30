using System.Net;
using System.Net.Http.Json;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Contracts;

namespace Buscarro.Infrastructure.Providers;

public class PokeDevsProvider : IPokemonApiService
{
    private readonly HttpClient _httpClient;

    public PokeDevsProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Pokemon?> GetPokemonAsync(string name, CancellationToken cancellationToken)
    {
        const string query = """
            query getPokemon($name: String!) {
              pokemon(where: {name: {_eq: $name}}, limit: 1) {
                name
                base_experience
                height
                weight
              }
            }
            """;

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://graphql.pokeapi.co/v1beta2")
        {
            Content = JsonContent.Create(new
            {
                query,
                operationName = "getPokemon",
                variables = new
                {
                    name
                }
            })
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PokeDevsGraphQlResponse>(cancellationToken: cancellationToken);

        if (payload is null)
        {
            throw new InvalidOperationException("A resposta da PokeDevs veio vazia.");
        }

        if (payload.Errors.Count > 0)
        {
            var errors = string.Join("; ", payload.Errors.Select(error => error.Message));
            throw new InvalidOperationException($"A PokeDevs retornou erro: {errors}");
        }

        var pokemon = payload.Data?.Pokemon.FirstOrDefault();

        if (pokemon is null)
        {
            return null;
        }

        return new Pokemon
        {
            From = "PokeDevs",
            Name = pokemon.Name,
            BaseExperience = pokemon.BaseExperience,
            Height = pokemon.Height,
            Weight = pokemon.Weight,
            Abilities = null
        };
    }
}

using System.Text.Json.Serialization;

namespace Buscarro.Infrastructure.Contracts;

public class PokeDevsGraphQlResponse
{
    [JsonPropertyName("data")]
    public PokeDevsGraphQlData? Data { get; set; }

    [JsonPropertyName("errors")]
    public List<PokeDevsGraphQlError> Errors { get; set; } = [];
}

public class PokeDevsGraphQlData
{
    [JsonPropertyName("pokemon")]
    public List<PokeDevsPokemon> Pokemon { get; set; } = [];
}

public class PokeDevsPokemon
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("base_experience")]
    public int BaseExperience { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("weight")]
    public int Weight { get; set; }
}

public class PokeDevsGraphQlError
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

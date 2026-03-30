using System.Text.Json.Serialization;

namespace Buscarro.Infrastructure.Contracts;

public class PokeDevsResponse
{
    [JsonPropertyName("pokemon_name")]
    public string PokemonName { get; set; } = string.Empty;

    [JsonPropertyName("xp")]
    public int Xp { get; set; }

    [JsonPropertyName("height_dm")]
    public int HeightDm { get; set; }

    [JsonPropertyName("weight_hg")]
    public int WeightHg { get; set; }

    [JsonPropertyName("abilities")]
    public List<PokeDevsAbility> Abilities { get; set; } = [];
}

public class PokeDevsAbility
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

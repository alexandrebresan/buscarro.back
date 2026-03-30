using System.Text.Json.Serialization;

namespace Buscarro.Infrastructure.Contracts;

public class PokeApiResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("base_experience")]
    public int BaseExperience { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("weight")]
    public int Weight { get; set; }

    [JsonPropertyName("abilities")]
    public List<PokeApiAbilitySlot> Abilities { get; set; } = [];
}

public class PokeApiAbilitySlot
{
    [JsonPropertyName("ability")]
    public PokeApiAbilityName Ability { get; set; } = new();
}

public class PokeApiAbilityName
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

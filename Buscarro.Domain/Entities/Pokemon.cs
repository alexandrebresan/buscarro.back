using System.Text.Json.Serialization;

namespace Buscarro.Domain.Entities;

public class Pokemon
{
    public string From { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int BaseExperience { get; set; }
    public int Height { get; set; }
    public int Weight { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Abilities { get; set; }
}

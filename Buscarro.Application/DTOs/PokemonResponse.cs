namespace Buscarro.Application.DTOs;

public sealed class PokemonResponse
{
    public string From { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int BaseExperience { get; set; }
    public int Height { get; set; }
    public int Weight { get; set; }
    public IReadOnlyCollection<string>? Abilities { get; set; }
}

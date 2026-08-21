namespace Buscarro.Application.DTOs;

public sealed class AiSearchResponse
{
    public string Prompt { get; set; } = string.Empty;
    public string SearchUrl { get; set; } = string.Empty;
    public bool AiSearch { get; set; }

}

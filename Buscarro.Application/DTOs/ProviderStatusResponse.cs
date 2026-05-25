namespace Buscarro.Application.DTOs;

public sealed class ProviderStatusResponse
{
    public string Provider { get; init; } = string.Empty;
    public bool Success { get; init; }
    public bool FromCache { get; init; }
    public bool UsedStaleCache { get; init; }
    public string? Error { get; init; }
    public int? CurrentResults { get; init; }
    public int? CurrentPage { get; init; }
}

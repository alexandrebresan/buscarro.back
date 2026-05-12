namespace Buscarro.Application.DTOs;

public sealed class AccessTokenResponse
{
    public string Token { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
}

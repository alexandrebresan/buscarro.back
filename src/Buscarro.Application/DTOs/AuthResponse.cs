namespace Buscarro.Application.DTOs;

public sealed class AuthResponse
{
    public UserResponse User { get; init; } = new();
    public string Token { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
    public DateTime ExpiresAtUtc
    {
        get; init;
    }
    public DateTime SessionExpiresAtUtc
    {
        get; init;
    }
}

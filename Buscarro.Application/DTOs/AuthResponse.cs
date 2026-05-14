namespace Buscarro.Application.DTOs;

public sealed class AuthResponse
{
    public UserResponse User { get; init; } = new();
}

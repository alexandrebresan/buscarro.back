namespace Buscarro.Application.DTOs;

public sealed class UserResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

namespace Buscarro.Application.DTOs;

public sealed record JwtTokenResponse(
    string Token,
    string TokenType,
    DateTime ExpiresAtUtc);

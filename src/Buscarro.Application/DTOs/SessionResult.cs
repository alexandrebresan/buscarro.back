namespace Buscarro.Application.DTOs;
// Internal result: controllers must serialize only Response and put RefreshToken in HttpOnly cookie.
public sealed record SessionResult(AuthResponse Response, string RefreshToken);

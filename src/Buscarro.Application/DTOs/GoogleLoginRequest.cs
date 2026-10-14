namespace Buscarro.Application.DTOs;

public sealed record GoogleLoginRequest(string IdToken, Guid ChallengeId, string? Password = null, string? DisplayName = null);

namespace Buscarro.Application.DTOs;
public sealed record GoogleChallengeResponse(Guid ChallengeId, string Nonce, DateTime ExpiresAtUtc);

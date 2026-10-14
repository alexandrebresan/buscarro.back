namespace Buscarro.Application.DTOs;
public sealed record GoogleIdentity(string Subject, string? Email, bool EmailVerified, string? Name, string? HostedDomain, string? Nonce);

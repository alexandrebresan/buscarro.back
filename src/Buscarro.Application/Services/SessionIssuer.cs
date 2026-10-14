using Buscarro.Application.DTOs;
using Microsoft.Extensions.Options;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;
namespace Buscarro.Application.Services;
public sealed class SessionIssuer(IAuthSessionRepository sessions, IJwtTokenService jwt, IRefreshTokenGenerator generator, TimeProvider clock, IOptions<AuthSessionOptions>? options = null) : ISessionIssuer
{
    public async Task<SessionResult> CreateAsync(User user, string method, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var session = new UserSession { UserId = user.Id, CreatedAtUtc = now, AuthenticatedAtUtc = now, AuthenticationMethod = method, ExpiresAtUtc = now.AddDays((options?.Value ?? new AuthSessionOptions()).SessionDays) };
        var raw = generator.Generate();
        var token = NewToken(session, raw, now);
        await sessions.AddSessionAsync(session, token, ct);
        return Result(user, session, raw);
    }
    public async Task<SessionResult> RotateAsync(User user, UserSession session, RefreshToken previous, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var raw = generator.Generate();
        var token = NewToken(session, raw, now);
        previous.UsedAtUtc = now;
        previous.SuccessorId = token.Id;
        await sessions.AddRefreshAsync(token, ct);
        return Result(user, session, raw);
    }
    private RefreshToken NewToken(UserSession session, string raw, DateTime now) => new() { SessionId = session.Id, TokenHash = generator.Hash(raw), CreatedAtUtc = now, ExpiresAtUtc = session.ExpiresAtUtc };
    private SessionResult Result(User user, UserSession session, string raw)
    {
        var token = jwt.CreateToken(user, session);
        return new(new AuthResponse { User = new UserResponse { Id = user.Id, Name = user.Name, Email = user.Email }, Token = token.Token, TokenType = token.TokenType, ExpiresAtUtc = token.ExpiresAtUtc, SessionExpiresAtUtc = session.ExpiresAtUtc }, raw);
    }
}

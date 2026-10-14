using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Interfaces;
using MediatR;
namespace Buscarro.Application.Handlers;
public sealed class RefreshSessionHandler(IAuthSessionRepository sessions, IUnitOfWork uow, ISessionIssuer issuer, IRefreshTokenGenerator generator, TimeProvider clock) : IRequestHandler<RefreshSessionCommand, SessionResult>
{
    public async Task<SessionResult> Handle(RefreshSessionCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken) || request.RefreshToken.Length > 256)
            throw new AuthFlowException("session_invalid");
        var hash = generator.Hash(request.RefreshToken);
        SessionResult? result = null;
        string? failure = null;
        await uow.ExecuteInTransactionAsync(async token =>
        {
            result = null;
            failure = null;
            var refresh = await sessions.FindRefreshAsync(hash, token);
            var candidate = refresh is null ? null : await sessions.GetSessionAsync(refresh.SessionId, token);
            if (candidate is null)
            {
                failure = "session_invalid";
                return;
            }
            var user = await sessions.LockUserAsync(candidate.UserId, token);
            var session = await sessions.LockSessionAsync(candidate.Id, token);
            refresh = await sessions.FindRefreshAsync(hash, token);
            var now = clock.GetUtcNow().UtcDateTime;
            if (user is null || session is null || refresh is null)
            {
                failure = "session_invalid";
                return;
            }
            if (refresh.UsedAtUtc is not null)
            {
                session.Revoke(now, "refresh_reuse");
                await sessions.RevokeRefreshTokensAsync(session.Id, now, token);
                failure = "session_invalid";
                return;
            }
            if (!session.IsActive(now) || refresh.RevokedAtUtc is not null || refresh.ExpiresAtUtc <= now)
            {
                failure = "session_invalid";
                return;
            }
            result = await issuer.RotateAsync(user, session, refresh, token);
        }, ct);
        // Reuse revocation must commit before the rejection leaves the handler.
        if (failure is not null)
            throw new AuthFlowException(failure);
        return result!;
    }
}

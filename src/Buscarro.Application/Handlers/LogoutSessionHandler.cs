using Buscarro.Application.Commands;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Interfaces;
using MediatR;
namespace Buscarro.Application.Handlers;
public sealed class LogoutSessionHandler(IAuthSessionRepository sessions, IUnitOfWork uow, IRefreshTokenGenerator generator, TimeProvider clock) : IRequestHandler<LogoutSessionCommand, Unit>
{
    public async Task<Unit> Handle(LogoutSessionCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken) || request.RefreshToken.Length > 256)
            return Unit.Value;
        var hash = generator.Hash(request.RefreshToken);
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var refresh = await sessions.FindRefreshAsync(hash, token);
            var candidate = refresh is null ? null : await sessions.GetSessionAsync(refresh.SessionId, token);
            if (candidate is null)
                return;
            await sessions.LockUserAsync(candidate.UserId, token);
            var session = await sessions.LockSessionAsync(candidate.Id, token);
            if (session is null)
                return;
            var now = clock.GetUtcNow().UtcDateTime;
            session.Revoke(now, "logout");
            await sessions.RevokeRefreshTokensAsync(session.Id, now, token);
        }, ct);
        return Unit.Value;
    }
}

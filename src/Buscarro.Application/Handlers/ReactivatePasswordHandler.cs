using Buscarro.Application.Commands;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Interfaces;
using MediatR;
namespace Buscarro.Application.Handlers;
public sealed class ReactivatePasswordHandler(IAuthSessionRepository sessions, IUnitOfWork uow, IPasswordHasherService hasher, TimeProvider clock) : IRequestHandler<ReactivatePasswordCommand, Unit>
{
    public async Task<Unit> Handle(ReactivatePasswordCommand request, CancellationToken ct)
    {
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var user = await sessions.LockUserAsync(request.UserId, token);
            var session = await sessions.LockSessionAsync(request.SessionId, token);
            var now = clock.GetUtcNow().UtcDateTime;
            if (user is null || session is null || session.UserId != user.Id || !session.IsActive(now) || session.AuthenticationMethod != "google" || session.AuthenticatedAtUtc > now || session.AuthenticatedAtUtc < now.AddMinutes(-5) || user.PasswordHash is null || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 512 || !hasher.VerifyPassword(user, request.Password.Trim()))
                throw new AuthFlowException("password_reactivation_denied", 403);
            user.PasswordLoginEnabled = true;
            await sessions.RevokeSessionsAsync(user.Id, session.Id, now, "password_reactivated", token);
        }, ct);
        return Unit.Value;
    }
}

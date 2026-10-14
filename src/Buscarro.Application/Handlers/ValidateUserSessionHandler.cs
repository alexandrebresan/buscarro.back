using Buscarro.Application.Queries;
using Buscarro.Domain.Interfaces;
using MediatR;
namespace Buscarro.Application.Handlers;
public sealed class ValidateUserSessionHandler(IAuthSessionRepository sessions, TimeProvider clock) : IRequestHandler<ValidateUserSessionQuery, bool>
{
    public async Task<bool> Handle(ValidateUserSessionQuery request, CancellationToken ct)
    {
        var session = await sessions.GetSessionAsync(request.SessionId, ct);
        return session is not null && session.UserId == request.UserId && session.IsActive(clock.GetUtcNow().UtcDateTime);
    }
}

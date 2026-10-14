using Buscarro.Application.Commands;
using Buscarro.Domain.Interfaces;
using MediatR;
namespace Buscarro.Application.Handlers;
public sealed class CleanupAuthSessionsHandler(IAuthSessionRepository sessions, IUnitOfWork uow, TimeProvider clock) : IRequestHandler<CleanupAuthSessionsCommand, Unit>
{
    public async Task<Unit> Handle(CleanupAuthSessionsCommand request, CancellationToken ct)
    {
        await uow.ExecuteInTransactionAsync(token => sessions.CleanupAsync(clock.GetUtcNow().UtcDateTime, token), ct);
        return Unit.Value;
    }
}

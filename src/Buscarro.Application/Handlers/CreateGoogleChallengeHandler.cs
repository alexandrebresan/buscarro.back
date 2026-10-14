using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Services;
using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;
using MediatR;
namespace Buscarro.Application.Handlers;
public sealed class CreateGoogleChallengeHandler(IAuthSessionRepository sessions, IUnitOfWork uow, IRefreshTokenGenerator generator, TimeProvider clock) : IRequestHandler<CreateGoogleChallengeCommand, GoogleChallengeResponse>
{
    public async Task<GoogleChallengeResponse> Handle(CreateGoogleChallengeCommand request, CancellationToken ct)
    {
        var binding = AuthInput.Required(request.BrowserBinding, "Browser binding", 256);
        GoogleChallengeResponse? result = null;
        await uow.ExecuteInTransactionAsync(async token => { var nonce = generator.Generate(); var challenge = new GoogleAuthChallenge { NonceHash = generator.Hash(nonce), BrowserBindingHash = generator.Hash(binding), ExpiresAtUtc = clock.GetUtcNow().UtcDateTime.AddMinutes(5) }; await sessions.AddChallengeAsync(challenge, token); result = new(challenge.Id, nonce, challenge.ExpiresAtUtc); }, ct);
        return result!;
    }
}

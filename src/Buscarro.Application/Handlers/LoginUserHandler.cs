using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Services;
using Buscarro.Domain.Interfaces;
using MediatR;
namespace Buscarro.Application.Handlers;
public sealed class LoginUserHandler(IUserRepository users, IAuthSessionRepository sessions, IPasswordHasherService hasher, IUnitOfWork uow, ISessionIssuer issuer) : IRequestHandler<LoginUserCommand, SessionResult>
{
    public async Task<SessionResult> Handle(LoginUserCommand request, CancellationToken ct)
    {
        var email = AuthInput.Email(request.Email);
        var password = AuthInput.Required(request.Password, "Senha");
        SessionResult? result = null;
        await uow.ExecuteInTransactionAsync(async token =>
        {
            result = null;
            var candidate = await users.GetByEmailAsync(email, token);
            var user = candidate is null ? null : await sessions.LockUserAsync(candidate.Id, token);
            if (user is null || !user.PasswordLoginEnabled || user.PasswordHash is null || !hasher.VerifyPassword(user, password))
                throw new AuthenticationFailedException();
            result = await issuer.CreateAsync(user, "password", token);
        }, ct);
        return result!;
    }
}

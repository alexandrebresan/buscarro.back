using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Services;
using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;
using MediatR;
namespace Buscarro.Application.Handlers;
public sealed class RegisterUserHandler(IUserRepository users, IAuthSessionRepository sessions, IPasswordHasherService hasher, IUnitOfWork uow, ISessionIssuer issuer) : IRequestHandler<RegisterUserCommand, SessionResult>
{
    public async Task<SessionResult> Handle(RegisterUserCommand request, CancellationToken ct)
    {
        var name = AuthInput.Required(request.Name, "Nome", 120);
        var email = AuthInput.Email(request.Email);
        var password = AuthInput.Required(request.Password, "Senha");
        SessionResult? result = null;
        try
        {
            await uow.ExecuteInTransactionAsync(async token =>
            {
                result = null;
                await sessions.LockIdentityAsync("email:" + email, token);
                if (await users.ExistsByEmailAsync(email, token))
                    throw new ConflictException("Ja existe um usuario cadastrado com este e-mail.");
                var user = new User { Name = name, Email = email };
                user.PasswordHash = hasher.HashPassword(user, password);
                await users.AddAsync(user, token);
                result = await issuer.CreateAsync(user, "password", token);
            }, ct);
        }
        catch (UniqueConstraintException) { throw new ConflictException("Ja existe um usuario cadastrado com este e-mail."); }
        return result!;
    }
}

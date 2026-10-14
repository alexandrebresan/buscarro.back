using Buscarro.Application.Commands;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Handlers;
using Buscarro.Application.Interfaces;
using Buscarro.Application.DTOs;
using Buscarro.Application.Services;
using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Repositories;
using Buscarro.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
namespace Buscarro.Tests;
public sealed class RegisterUserHandlerTests
{
    [Fact]
    public async Task Login_and_registration_issue_persisted_sessions_with_hashed_refresh_tokens()
    {
        using var db = AuthSessionCoreTests.CreateDb();
        var users = new UserRepository(db);
        var auth = new AuthSessionRepository(db);
        var uow = new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(db);
        var generator = new RefreshTokenGenerator();
        var hasher = new PasswordHasherService();
        var issuer = new SessionIssuer(auth, new Jwt(), generator, TimeProvider.System);
        var register = new RegisterUserHandler(users, auth, hasher, uow, issuer);
        var registered = await register.Handle(new("Alex", "ALEX@EXAMPLE.COM", "secret"), default);
        Assert.Equal("alex@example.com", registered.Response.User.Email);
        Assert.NotEqual("secret", (await db.Users.SingleAsync()).PasswordHash);
        var login = new LoginUserHandler(users, auth, hasher, uow, issuer);
        var loggedIn = await login.Handle(new("ALEX@EXAMPLE.COM", "secret"), default);
        Assert.Equal(registered.Response.User.Id, loggedIn.Response.User.Id);
        Assert.Equal(2, await db.UserSessions.CountAsync());
        Assert.Contains(await db.RefreshTokens.ToListAsync(), t => t.TokenHash == generator.Hash(loggedIn.RefreshToken));
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => login.Handle(new("alex@example.com", "wrong"), default));
    }
    private sealed class Jwt : IJwtTokenService
    {
        public JwtTokenResponse CreateToken(User user, UserSession session) => new("jwt-token", "Bearer", DateTime.UtcNow.AddMinutes(15));
    }
}

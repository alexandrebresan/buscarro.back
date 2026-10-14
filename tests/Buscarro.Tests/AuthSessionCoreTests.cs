using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Handlers;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Services;
using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.Repositories;
using Buscarro.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
namespace Buscarro.Tests;
public sealed class AuthSessionCoreTests
{
    [Theory]
    [InlineData("invalid")]
    [InlineData("expired_token")]
    [InlineData("expired_session")]
    [InlineData("revoked_token")]
    [InlineData("revoked_session")]
    public async Task Invalid_expired_and_revoked_refresh_cannot_create_successor(string failure)
    {
        using var db = CreateDb();
        var auth = new AuthSessionRepository(db);
        var user = new User { Name = "Alex", Email = "alex@example.com" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var generator = new RefreshTokenGenerator();
        var issuer = new SessionIssuer(auth, new Jwt(), generator, TimeProvider.System);
        var issued = await issuer.CreateAsync(user, "password", default);
        await db.SaveChangesAsync();
        var token = await db.RefreshTokens.SingleAsync();
        var session = await db.UserSessions.SingleAsync();
        if (failure == "expired_token") token.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        if (failure == "expired_session") session.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        if (failure == "revoked_token") token.RevokedAtUtc = DateTime.UtcNow;
        if (failure == "revoked_session") session.Revoke(DateTime.UtcNow, "test");
        await db.SaveChangesAsync();
        var handler = new RefreshSessionHandler(auth, new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(db), issuer, generator, TimeProvider.System);
        await Assert.ThrowsAsync<AuthFlowException>(() => handler.Handle(new(failure == "invalid" ? "unknown-token" : issued.RefreshToken), default));
        Assert.Single(await db.RefreshTokens.ToListAsync());
        Assert.Null((await db.RefreshTokens.SingleAsync()).SuccessorId);
    }

    [Fact]
    public async Task Refresh_rotates_and_reuse_commits_revocation()
    {
        using var db = CreateDb();
        var users = new UserRepository(db);
        var auth = new AuthSessionRepository(db);
        var uow = new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(db);
        var user = new User { Name = "Alex", Email = "alex@example.com" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var generator = new RefreshTokenGenerator();
        var issuer = new SessionIssuer(auth, new Jwt(), generator, TimeProvider.System);
        var issued = await issuer.CreateAsync(user, "password", default);
        await db.SaveChangesAsync();
        var handler = new RefreshSessionHandler(auth, uow, issuer, generator, TimeProvider.System);
        var rotated = await handler.Handle(new(issued.RefreshToken), default);
        Assert.NotEqual(issued.RefreshToken, rotated.RefreshToken);
        Assert.Equal(issued.Response.SessionExpiresAtUtc, rotated.Response.SessionExpiresAtUtc);
        await Assert.ThrowsAsync<AuthFlowException>(() => handler.Handle(new(issued.RefreshToken), default));
        db.ChangeTracker.Clear();
        Assert.NotNull((await db.UserSessions.SingleAsync()).RevokedAtUtc);
        await Assert.ThrowsAsync<AuthFlowException>(() => handler.Handle(new(rotated.RefreshToken), default));
    }
    [Fact]
    public async Task Google_auto_link_disables_password_and_preserves_user()
    {
        using var db = CreateDb();
        var user = new User { Name = "Alex", Email = "alex@gmail.com", PasswordHash = "legacy" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var auth = new AuthSessionRepository(db);
        var uow = new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(db);
        var generator = new RefreshTokenGenerator();
        var challenge = await new CreateGoogleChallengeHandler(auth, uow, generator, TimeProvider.System).Handle(new("browser"), default);
        var validator = new Google(new("subject", "alex@gmail.com", true, "Alex", null, challenge.Nonce));
        var handler = new GoogleLoginHandler(new UserRepository(db), auth, uow, new SessionIssuer(auth, new Jwt(), generator, TimeProvider.System), validator, new PasswordHasherService(), generator, TimeProvider.System);
        var result = await handler.Handle(new("token", challenge.ChallengeId, "browser"), default);
        Assert.Equal(user.Id, result.Response.User.Id);
        Assert.False(user.PasswordLoginEnabled);
        Assert.Equal("legacy", user.PasswordHash);
        Assert.Single(await db.UserExternalLogins.ToListAsync());
        await Assert.ThrowsAsync<AuthFlowException>(() => handler.Handle(new("token", challenge.ChallengeId, "browser"), default));
    }
    internal static BuscarroDbContext CreateDb() => new(new DbContextOptionsBuilder<BuscarroDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed class Jwt : IJwtTokenService
    {
        public JwtTokenResponse CreateToken(User user, UserSession session) => new("jwt", "Bearer", DateTime.UtcNow.AddMinutes(15));
    }
    private sealed class Google(GoogleIdentity identity) : IGoogleTokenValidator
    {
        public Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken token) => Task.FromResult(identity);
    }
}

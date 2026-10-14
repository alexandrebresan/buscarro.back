using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Handlers;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using Buscarro.Application.Services;
using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Repositories;
using Buscarro.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
namespace Buscarro.Tests;
public sealed class AuthSecurityCoreTests
{
    [Fact]
    public async Task Google_unverified_email_cannot_create_or_link_user()
    {
        using var db = AuthSessionCoreTests.CreateDb();
        var auth = new AuthSessionRepository(db);
        var uow = new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(db);
        var gen = new RefreshTokenGenerator();
        var challenge = await new CreateGoogleChallengeHandler(auth, uow, gen, TimeProvider.System).Handle(new("browser"), default);
        var handler = new GoogleLoginHandler(new UserRepository(db), auth, uow, new SessionIssuer(auth, new Jwt(), gen, TimeProvider.System),
            new Google(new("subject", "alex@gmail.com", false, "Alex", null, challenge.Nonce)), new PasswordHasherService(), gen, TimeProvider.System);
        var error = await Assert.ThrowsAsync<AuthFlowException>(() => handler.Handle(new("token", challenge.ChallengeId, "browser"), default));
        Assert.Equal("google_email_unverified", error.Code);
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.UserExternalLogins.ToListAsync());
    }
    [Fact]
    public async Task Logout_revokes_session_and_all_refresh_tokens_and_is_idempotent()
    {
        using var db = AuthSessionCoreTests.CreateDb();
        var auth = new AuthSessionRepository(db);
        var uow = new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(db);
        var gen = new RefreshTokenGenerator();
        var user = new User { Name = "Alex", Email = "alex@example.com" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var result = await new SessionIssuer(auth, new Jwt(), gen, TimeProvider.System).CreateAsync(user, "password", default);
        await db.SaveChangesAsync();
        var session = await db.UserSessions.SingleAsync();
        var validate = new ValidateUserSessionHandler(auth, TimeProvider.System);
        Assert.True(await validate.Handle(new(user.Id, session.Id), default));
        Assert.False(await validate.Handle(new(Guid.NewGuid(), session.Id), default));
        var logout = new LogoutSessionHandler(auth, uow, gen, TimeProvider.System);
        await logout.Handle(new(result.RefreshToken), default);
        await logout.Handle(new(result.RefreshToken), default);
        await logout.Handle(new(null), default);
        Assert.False(await validate.Handle(new(user.Id, session.Id), default));
        Assert.NotNull((await db.RefreshTokens.SingleAsync()).RevokedAtUtc);
    }
    [Fact]
    public async Task Non_authoritative_email_collision_requires_local_password_and_subject_never_moves()
    {
        using var db = AuthSessionCoreTests.CreateDb();
        var hasher = new PasswordHasherService();
        var user = new User { Name = "Alex", Email = "alex@example.com" };
        user.PasswordHash = hasher.HashPassword(user, "secret");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var auth = new AuthSessionRepository(db);
        var uow = new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(db);
        var gen = new RefreshTokenGenerator();
        var challenge = await new CreateGoogleChallengeHandler(auth, uow, gen, TimeProvider.System).Handle(new("browser"), default);
        var google = new Google(new("subject", user.Email, true, "Alex", null, challenge.Nonce));
        var handler = new GoogleLoginHandler(new UserRepository(db), auth, uow, new SessionIssuer(auth, new Jwt(), gen, TimeProvider.System), google, hasher, gen, TimeProvider.System);
        var failure = await Assert.ThrowsAsync<AuthFlowException>(() => handler.Handle(new("token", challenge.ChallengeId, "browser"), default));
        Assert.Equal("account_link_required", failure.Code);
        Assert.Empty(await db.UserExternalLogins.ToListAsync());
        var result = await handler.Handle(new("token", challenge.ChallengeId, "browser", "secret"), default);
        Assert.Equal(user.Id, result.Response.User.Id);
        Assert.True((await db.Users.SingleAsync()).PasswordLoginEnabled);
        var second = await new CreateGoogleChallengeHandler(auth, uow, gen, TimeProvider.System).Handle(new("browser"), default);
        google.Identity = new("subject", "changed@elsewhere.com", false, "Changed", null, second.Nonce);
        var returning = await handler.Handle(new("token", second.ChallengeId, "browser"), default);
        Assert.Equal(user.Email, returning.Response.User.Email);
        Assert.Single(await db.Users.ToListAsync());
    }
    [Theory]
    [InlineData("other-browser", false)]
    [InlineData("browser", true)]
    public async Task Google_challenge_rejects_wrong_browser_or_nonce(string browser, bool wrongNonce)
    {
        using var db = AuthSessionCoreTests.CreateDb();
        var auth = new AuthSessionRepository(db);
        var uow = new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(db);
        var gen = new RefreshTokenGenerator();
        var challenge = await new CreateGoogleChallengeHandler(auth, uow, gen, TimeProvider.System).Handle(new("browser"), default);
        var handler = new GoogleLoginHandler(new UserRepository(db), auth, uow, new SessionIssuer(auth, new Jwt(), gen, TimeProvider.System), new Google(new("subject", "alex@gmail.com", true, "Alex", null, wrongNonce ? "wrong" : challenge.Nonce)), new PasswordHasherService(), gen, TimeProvider.System);
        var failure = await Assert.ThrowsAsync<AuthFlowException>(() => handler.Handle(new("token", challenge.ChallengeId, browser), default));
        Assert.Equal("google_challenge_invalid", failure.Code);
        Assert.Empty(await db.Users.ToListAsync());
    }
    [Fact]
    public async Task Reactivation_uses_original_google_authentication_time_and_revokes_other_sessions()
    {
        using var db = AuthSessionCoreTests.CreateDb();
        var auth = new AuthSessionRepository(db);
        var uow = new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(db);
        var hasher = new PasswordHasherService();
        var gen = new RefreshTokenGenerator();
        var user = new User { Name = "Alex", Email = "alex@gmail.com", PasswordLoginEnabled = false };
        user.PasswordHash = hasher.HashPassword(user, "old-secret");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var issuer = new SessionIssuer(auth, new Jwt(), gen, TimeProvider.System);
        await issuer.CreateAsync(user, "password", default);
        await issuer.CreateAsync(user, "google", default);
        await db.SaveChangesAsync();
        var current = await db.UserSessions.SingleAsync(x => x.AuthenticationMethod == "google");
        var handler = new ReactivatePasswordHandler(auth, uow, hasher, TimeProvider.System);
        await handler.Handle(new(user.Id, current.Id, "old-secret"), default);
        Assert.True((await db.Users.SingleAsync()).PasswordLoginEnabled);
        Assert.NotNull((await db.UserSessions.SingleAsync(x => x.AuthenticationMethod == "password")).RevokedAtUtc);
        current.AuthenticatedAtUtc = DateTime.UtcNow.AddMinutes(-6);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<AuthFlowException>(() => handler.Handle(new(user.Id, current.Id, "old-secret"), default));
    }
    [Fact]
    public void Refresh_generator_emits_32_random_bytes_and_only_hash_is_persistable()
    {
        var gen = new RefreshTokenGenerator();
        var one = gen.Generate();
        var two = gen.Generate();
        Assert.NotEqual(one, two);
        Assert.Equal(43, one.Length);
        Assert.Equal(64, gen.Hash(one).Length);
        Assert.NotEqual(one, gen.Hash(one));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(2)]
    public async Task Session_expiration_uses_configured_absolute_duration(int days)
    {
        using var db = AuthSessionCoreTests.CreateDb();
        var auth = new AuthSessionRepository(db);
        var user = new User { Name = "Alex", Email = "alex@example.com" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var now = DateTime.UtcNow;
        var issuer = new SessionIssuer(auth, new Jwt(), new RefreshTokenGenerator(), TimeProvider.System, Microsoft.Extensions.Options.Options.Create(new AuthSessionOptions { SessionDays = days }));
        var result = await issuer.CreateAsync(user, "password", default);
        Assert.InRange(result.Response.SessionExpiresAtUtc, now.AddDays(days), DateTime.UtcNow.AddDays(days));
    }

    [Fact]
    public async Task Cleanup_keeps_consumed_hashes_until_absolute_family_expiration()
    {
        using var db = AuthSessionCoreTests.CreateDb();
        var auth = new AuthSessionRepository(db);
        var uow = new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(db);
        var gen = new RefreshTokenGenerator();
        var clock = new Clock(DateTimeOffset.UtcNow);
        var user = new User { Name = "Alex", Email = "alex@example.com" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var issuer = new SessionIssuer(auth, new Jwt(), gen, clock);
        var issued = await issuer.CreateAsync(user, "password", default);
        await db.SaveChangesAsync();
        await new RefreshSessionHandler(auth, uow, issuer, gen, clock).Handle(new(issued.RefreshToken), default);
        await new CreateGoogleChallengeHandler(auth, uow, gen, clock).Handle(new("browser"), default);
        clock.Now = clock.Now.AddMinutes(6);
        var cleanup = new CleanupAuthSessionsHandler(auth, uow, clock);
        await cleanup.Handle(new(), default);
        Assert.Empty(await db.GoogleAuthChallenges.ToListAsync());
        Assert.Equal(2, await db.RefreshTokens.CountAsync());
        Assert.Single(await db.UserSessions.ToListAsync());
        clock.Now = clock.Now.AddDays(31);
        await cleanup.Handle(new(), default);
        Assert.Empty(await db.RefreshTokens.ToListAsync());
        Assert.Empty(await db.UserSessions.ToListAsync());
        Assert.Single(await db.Users.ToListAsync());
    }
    [Fact]
    public async Task Missing_google_name_requires_explicit_profile_completion_without_inventing_values()
    {
        using var db = AuthSessionCoreTests.CreateDb();
        var auth = new AuthSessionRepository(db);
        var uow = new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(db);
        var gen = new RefreshTokenGenerator();
        var challenge = await new CreateGoogleChallengeHandler(auth, uow, gen, TimeProvider.System).Handle(new("browser"), default);
        var handler = new GoogleLoginHandler(new UserRepository(db), auth, uow, new SessionIssuer(auth, new Jwt(), gen, TimeProvider.System), new Google(new("subject", "alex@gmail.com", true, null, null, challenge.Nonce)), new PasswordHasherService(), gen, TimeProvider.System);
        var failure = await Assert.ThrowsAsync<AuthFlowException>(() => handler.Handle(new("token", challenge.ChallengeId, "browser"), default));
        Assert.Equal("profile_name_required", failure.Code);
        Assert.Empty(await db.Users.ToListAsync());
        var result = await handler.Handle(new("token", challenge.ChallengeId, "browser", DisplayName: "Alex"), default);
        Assert.Equal("Alex", result.Response.User.Name);
    }
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now; public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Jwt : IJwtTokenService
    {
        public JwtTokenResponse CreateToken(User user, UserSession session) => new("jwt", "Bearer", DateTime.UtcNow.AddMinutes(15));
    }
    private sealed class Google(GoogleIdentity identity) : IGoogleTokenValidator
    {
        public GoogleIdentity Identity { get; set; } = identity; public Task<GoogleIdentity> ValidateAsync(string token, CancellationToken ct) => Task.FromResult(Identity);
    }
}

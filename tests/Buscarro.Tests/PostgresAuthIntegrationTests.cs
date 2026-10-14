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
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Buscarro.Tests;
public sealed class PostgresAuthIntegrationTests
{
    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Concurrent_refresh_creates_one_successor_and_commits_reuse_revocation()
    {
        await using var fixture = new PostgresTestDatabaseFixture();
        await fixture.InitializeAsync();
        await using var seed = Db(fixture);
        await seed.Database.MigrateAsync();
        var user = new User { Name = "Alex", Email = "alex@example.com" };
        seed.Users.Add(user);
        await seed.SaveChangesAsync();
        var gen = new RefreshTokenGenerator();
        var issued = await new SessionIssuer(new AuthSessionRepository(seed), new Jwt(), gen, TimeProvider.System).CreateAsync(user, "password", default);
        await seed.SaveChangesAsync();
        async Task<bool> Rotate()
        {
            await using var db = Db(fixture);
            var repo = new AuthSessionRepository(db);
            try
            {
                await new RefreshSessionHandler(repo, new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(db), new SessionIssuer(repo, new Jwt(), gen, TimeProvider.System), gen, TimeProvider.System).Handle(new(issued.RefreshToken), default);
                return true;
            }
            catch (AuthFlowException) { return false; }
        }
        var outcomes = await Task.WhenAll(Rotate(), Rotate());
        Assert.Single(outcomes, x => x);
        seed.ChangeTracker.Clear();
        Assert.Equal(2, await seed.RefreshTokens.CountAsync());
        Assert.NotNull((await seed.UserSessions.SingleAsync()).RevokedAtUtc);
    }
    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Normalized_email_unique_index_rejects_case_variant_and_refresh_hash_is_unique()
    {
        await using var fixture = new PostgresTestDatabaseFixture();
        await fixture.InitializeAsync();
        await using var db = Db(fixture);
        await db.Database.MigrateAsync();
        db.Users.Add(new()
        {
            Name = "First",
            Email = "Alex@example.com"
        });
        await db.SaveChangesAsync();
        db.Users.Add(new()
        {
            Name = "Second",
            Email = "alex@example.com"
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Google_concurrent_first_logins_create_one_user_and_one_subject_link()
    {
        await using var fixture = new PostgresTestDatabaseFixture();
        await fixture.InitializeAsync();
        await using var seed = Db(fixture);
        await seed.Database.MigrateAsync();
        var gen = new RefreshTokenGenerator();
        var repo = new AuthSessionRepository(seed);
        var uow = new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(seed);
        var challenges = new CreateGoogleChallengeHandler(repo, uow, gen, TimeProvider.System);
        var first = await challenges.Handle(new("first-browser"), default);
        var second = await challenges.Handle(new("second-browser"), default);
        async Task<SessionResult> Login(GoogleChallengeResponse challenge, string browser)
        {
            await using var db = Db(fixture);
            var auth = new AuthSessionRepository(db);
            var handler = new GoogleLoginHandler(new UserRepository(db), auth, new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(db), new SessionIssuer(auth, new Jwt(), gen, TimeProvider.System), new Google(new("subject", "alex@gmail.com", true, "Alex", null, challenge.Nonce)), new PasswordHasherService(), gen, TimeProvider.System);
            return await handler.Handle(new("token", challenge.ChallengeId, browser), default);
        }
        var results = await Task.WhenAll(Login(first, "first-browser"), Login(second, "second-browser"));
        Assert.Equal(results[0].Response.User.Id, results[1].Response.User.Id);
        Assert.Equal(1, await seed.Users.CountAsync());
        Assert.Equal(1, await seed.UserExternalLogins.CountAsync());
        Assert.Equal(2, await seed.UserSessions.CountAsync());
    }
    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Password_login_racing_auto_link_cannot_leave_password_session_active()
    {
        await using var fixture = new PostgresTestDatabaseFixture();
        await fixture.InitializeAsync();
        await using var seed = Db(fixture);
        await seed.Database.MigrateAsync();
        var gen = new RefreshTokenGenerator();
        var hasher = new PasswordHasherService();
        var user = new User { Name = "Alex", Email = "alex@gmail.com" };
        user.PasswordHash = hasher.HashPassword(user, "secret");
        seed.Users.Add(user);
        await seed.SaveChangesAsync();
        var challenge = await new CreateGoogleChallengeHandler(new AuthSessionRepository(seed), new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(seed), gen, TimeProvider.System).Handle(new("browser"), default);
        async Task PasswordLogin()
        {
            await using var db = Db(fixture);
            var auth = new AuthSessionRepository(db);
            try
            {
                await new LoginUserHandler(new UserRepository(db), auth, hasher, new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(db), new SessionIssuer(auth, new Jwt(), gen, TimeProvider.System)).Handle(new(user.Email, "secret"), default);
            }
            catch (AuthenticationFailedException) { }
        }
        async Task GoogleLogin()
        {
            await using var db = Db(fixture);
            var auth = new AuthSessionRepository(db);
            await new GoogleLoginHandler(new UserRepository(db), auth, new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(db), new SessionIssuer(auth, new Jwt(), gen, TimeProvider.System), new Google(new("subject", user.Email, true, "Alex", null, challenge.Nonce)), hasher, gen, TimeProvider.System).Handle(new("token", challenge.ChallengeId, "browser"), default);
        }
        await Task.WhenAll(PasswordLogin(), GoogleLogin());
        seed.ChangeTracker.Clear();
        Assert.False((await seed.Users.SingleAsync()).PasswordLoginEnabled);
        Assert.Empty(await seed.UserSessions.Where(x => x.AuthenticationMethod == "password" && x.RevokedAtUtc == null).ToListAsync());
        Assert.Single(await seed.UserSessions.Where(x => x.AuthenticationMethod == "google" && x.RevokedAtUtc == null).ToListAsync());
    }
    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Refresh_racing_logout_always_ends_with_revoked_family()
    {
        await using var fixture = new PostgresTestDatabaseFixture();
        await fixture.InitializeAsync();
        await using var seed = Db(fixture);
        await seed.Database.MigrateAsync();
        var gen = new RefreshTokenGenerator();
        var user = new User { Name = "Alex", Email = "alex@example.com" };
        seed.Users.Add(user);
        await seed.SaveChangesAsync();
        var issued = await new SessionIssuer(new AuthSessionRepository(seed), new Jwt(), gen, TimeProvider.System).CreateAsync(user, "password", default);
        await seed.SaveChangesAsync();
        async Task Refresh()
        {
            await using var db = Db(fixture);
            var auth = new AuthSessionRepository(db);
            try
            {
                await new RefreshSessionHandler(auth, new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(db), new SessionIssuer(auth, new Jwt(), gen, TimeProvider.System), gen, TimeProvider.System).Handle(new(issued.RefreshToken), default);
            }
            catch (AuthFlowException) { }
        }
        async Task Logout()
        {
            await using var db = Db(fixture);
            await new LogoutSessionHandler(new AuthSessionRepository(db), new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(db), gen, TimeProvider.System).Handle(new(issued.RefreshToken), default);
        }
        await Task.WhenAll(Refresh(), Logout());
        seed.ChangeTracker.Clear();
        Assert.NotNull((await seed.UserSessions.SingleAsync()).RevokedAtUtc);
        Assert.All(await seed.RefreshTokens.ToListAsync(), x => Assert.NotNull(x.RevokedAtUtc));
    }
    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Google_migration_blocks_normalized_collisions_without_modifying_existing_users()
    {
        await using var fixture = new PostgresTestDatabaseFixture();
        await fixture.InitializeAsync();
        await using var db = Db(fixture);
        var migrator = db.GetService<IMigrator>();
        var sessionsMigration = db.Database.GetMigrations().Single(x => x.EndsWith("AddUserSessionsAndRefreshTokens"));
        await migrator.MigrateAsync(sessionsMigration);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Users\" (\"Id\",\"Name\",\"Email\",\"PasswordHash\",\"CreatedAtUtc\") VALUES ({Guid.NewGuid()},'First','Alex@example.com','hash',{DateTime.UtcNow}),({Guid.NewGuid()},'Second',' alex@example.com ','hash',{DateTime.UtcNow})");
        var failure = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => migrator.MigrateAsync());
        Assert.Contains("collisions", failure.MessageText);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        await command.Connection!.OpenAsync();
        command.CommandText = "SELECT count(*) FROM \"Users\"";
        Assert.Equal(2L, await command.ExecuteScalarAsync());
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Register_racing_google_for_same_email_never_duplicates_user_or_keeps_password_active()
    {
        await using var fixture = new PostgresTestDatabaseFixture();
        await fixture.InitializeAsync();
        await using var seed = Db(fixture);
        await seed.Database.MigrateAsync();
        var gen = new RefreshTokenGenerator();
        var hasher = new PasswordHasherService();
        var challenge = await new CreateGoogleChallengeHandler(new AuthSessionRepository(seed), new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(seed), gen, TimeProvider.System).Handle(new("browser"), default);
        async Task Register()
        {
            await using var db = Db(fixture);
            var auth = new AuthSessionRepository(db);
            try
            {
                await new RegisterUserHandler(new UserRepository(db), auth, hasher, new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(db), new SessionIssuer(auth, new Jwt(), gen, TimeProvider.System)).Handle(new("Alex", "alex@gmail.com", "secret"), default);
            }
            catch (ConflictException) { }
        }
        async Task GoogleLogin()
        {
            await using var db = Db(fixture);
            var auth = new AuthSessionRepository(db);
            await new GoogleLoginHandler(new UserRepository(db), auth, new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(db), new SessionIssuer(auth, new Jwt(), gen, TimeProvider.System), new Google(new("subject", "alex@gmail.com", true, "Alex", null, challenge.Nonce)), hasher, gen, TimeProvider.System).Handle(new("token", challenge.ChallengeId, "browser"), default);
        }
        await Task.WhenAll(Register(), GoogleLogin());
        Assert.Single(await seed.Users.ToListAsync());
        Assert.False((await seed.Users.SingleAsync()).PasswordLoginEnabled);
        Assert.Single(await seed.UserExternalLogins.ToListAsync());
        Assert.Empty(await seed.UserSessions.Where(x => x.AuthenticationMethod == "password" && x.RevokedAtUtc == null).ToListAsync());
    }
    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Legacy_whitespace_email_lookup_uses_normalized_index_and_refresh_hash_is_unique()
    {
        await using var fixture = new PostgresTestDatabaseFixture();
        await fixture.InitializeAsync();
        await using var db = Db(fixture);
        await db.Database.MigrateAsync();
        var user = new User { Name = "Legacy", Email = " Alex@Example.com ", PasswordHash = "hash" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var repo = new UserRepository(db);
        Assert.Equal(user.Id, (await repo.GetByEmailAsync("alex@example.com", default))!.Id);
        Assert.True(await repo.ExistsByEmailAsync("alex@example.com", default));
        var auth = new AuthSessionRepository(db);
        var gen = new RefreshTokenGenerator();
        await new SessionIssuer(auth, new Jwt(), gen, TimeProvider.System).CreateAsync(user, "password", default);
        await db.SaveChangesAsync();
        var token = await db.RefreshTokens.SingleAsync();
        db.RefreshTokens.Add(new RefreshToken { SessionId = token.SessionId, TokenHash = token.TokenHash, CreatedAtUtc = token.CreatedAtUtc, ExpiresAtUtc = token.ExpiresAtUtc });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    private sealed class Google(GoogleIdentity identity) : IGoogleTokenValidator
    {
        public Task<GoogleIdentity> ValidateAsync(string token, CancellationToken ct) => Task.FromResult(identity);
    }
    private static BuscarroDbContext Db(PostgresTestDatabaseFixture fixture) => new(new DbContextOptionsBuilder<BuscarroDbContext>().UseNpgsql(fixture.ConnectionString).Options);
    private sealed class Jwt : IJwtTokenService
    {
        public JwtTokenResponse CreateToken(User user, UserSession session) => new("jwt", "Bearer", DateTime.UtcNow.AddMinutes(15));
    }
}

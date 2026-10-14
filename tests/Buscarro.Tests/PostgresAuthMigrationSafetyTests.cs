using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Buscarro.Tests;

public sealed class PostgresAuthMigrationSafetyTests
{
    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Downgrade_cannot_reenable_password_disabled_by_google_linking()
    {
        await using var fixture = new PostgresTestDatabaseFixture();
        await fixture.InitializeAsync();
        await using var db = new BuscarroDbContext(new DbContextOptionsBuilder<BuscarroDbContext>().UseNpgsql(fixture.ConnectionString).Options);
        await db.Database.MigrateAsync();
        db.Users.Add(new() { Name = "Alex", Email = "alex@gmail.com", PasswordHash = "legacy-hash", PasswordLoginEnabled = false });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261001175811_AddUserSessionsAndRefreshTokens"));
        db.ChangeTracker.Clear();
        Assert.False((await db.Users.SingleAsync()).PasswordLoginEnabled);
    }
}

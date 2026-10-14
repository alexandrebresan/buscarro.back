using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Buscarro.Tests;

public sealed class DesignTimeAuthMigrationTests
{
    [Theory]
    [InlineData("Host=127.0.0.1;Database=buscarro_migration_test;Username=migration")]
    [InlineData("postgresql://migration@127.0.0.1/buscarro_migration_test")]
    public void Explicit_migration_connection_is_used_without_starting_the_api(string connection)
    {
        using var context = new BuscarroDesignTimeDbContextFactory()
            .CreateDbContext(["--connection", connection]);

        var configured = new NpgsqlConnectionStringBuilder(context.Database.GetConnectionString());
        Assert.Equal("buscarro_migration_test", configured.Database);
        Assert.Equal("migration", configured.Username);
    }
}

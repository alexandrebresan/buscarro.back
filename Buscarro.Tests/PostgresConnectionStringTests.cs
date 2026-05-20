using Buscarro.Infrastructure.Persistence;

namespace Buscarro.Tests;

public sealed class PostgresConnectionStringTests
{
    [Fact]
    public void Normalize_returns_standard_connection_string_unchanged()
    {
        const string connectionString = "Host=localhost;Database=buscarro;Username=buscarro;Password=secret";

        var normalized = PostgresConnectionString.Normalize(connectionString);

        Assert.Equal(connectionString, normalized);
    }

    [Fact]
    public void Normalize_converts_render_database_url_to_npgsql_connection_string()
    {
        const string databaseUrl = "postgresql://buscarro_user:password%40123@dpg-example-a.ohio-postgres.render.com/buscarro_db";

        var normalized = PostgresConnectionString.Normalize(databaseUrl);

        Assert.Contains("Host=dpg-example-a.ohio-postgres.render.com", normalized);
        Assert.Contains("Database=buscarro_db", normalized);
        Assert.Contains("Username=buscarro_user", normalized);
        Assert.Contains("Password=password@123", normalized);
        Assert.Contains("SSL Mode=Require", normalized);
    }
}

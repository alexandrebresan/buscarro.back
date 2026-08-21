using Npgsql;

namespace Buscarro.Tests;

public sealed class PostgresIntegrationSqlSafetyTests
{
    [Fact]
    public void Migration_metadata_query_scopes_every_catalog_to_parameterized_fixture_schema()
    {
        const string schema="buscarro_test_0123456789abcdef0123456789abcdef";
        using var connection=new NpgsqlConnection();using var command=PostgresCatalogIntegrationTests.CreateMigrationMetadataCommand(connection,schema);

        Assert.Contains("JOIN pg_namespace n ON n.oid = c.connamespace",command.CommandText,StringComparison.Ordinal);
        Assert.Contains("n.nspname = @schema",command.CommandText,StringComparison.Ordinal);
        Assert.Contains("schemaname = @schema",command.CommandText,StringComparison.Ordinal);
        Assert.Contains("table_schema = @schema",command.CommandText,StringComparison.Ordinal);
        Assert.DoesNotContain(schema,command.CommandText,StringComparison.Ordinal);
        var parameter=Assert.Single(command.Parameters.Cast<NpgsqlParameter>());
        Assert.Equal("schema",parameter.ParameterName);Assert.Equal(schema,parameter.Value);
    }
}

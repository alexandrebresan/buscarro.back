using System.Text.RegularExpressions;
using Npgsql;

namespace Buscarro.Tests;

internal sealed class PostgresTestDatabaseFixture:IAsyncDisposable
{
    private static readonly Regex SafeSchema=new("^buscarro_test_[0-9a-f]{32}$",RegexOptions.CultureInvariant);
    private readonly string _adminConnectionString;
    public string Schema { get; }=$"buscarro_test_{Guid.NewGuid():N}";
    public string ConnectionString { get; private set; }=string.Empty;
    public PostgresTestDatabaseFixture()=>_adminConnectionString=Environment.GetEnvironmentVariable("TEST_DATABASE_URL")??string.Empty;
    public static bool CanRun(out string reason)=>Validate(Environment.GetEnvironmentVariable("TEST_DATABASE_URL"),Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION_TESTS"),Environment.GetEnvironmentVariable("ALLOW_REMOTE_TEST_DATABASE"),out reason);
    internal static bool Validate(string? connectionString,string? run,string? allowRemote,out string reason)
    {
        if(!string.Equals(run,"true",StringComparison.OrdinalIgnoreCase)){reason="RUN_POSTGRES_INTEGRATION_TESTS=true ausente";return false;}
        if(string.IsNullOrWhiteSpace(connectionString)){reason="TEST_DATABASE_URL ausente";return false;}
        try
        {
            var builder=new NpgsqlConnectionStringBuilder(connectionString);
            if(!(builder.Database??string.Empty).Contains("test",StringComparison.OrdinalIgnoreCase)){reason="database de teste deve conter 'test' no nome";return false;}
            var local=(builder.Host??string.Empty).Trim().ToLowerInvariant() is "localhost" or "127.0.0.1" or "::1";
            if(!local&&!string.Equals(allowRemote,"true",StringComparison.OrdinalIgnoreCase)){reason="banco remoto requer ALLOW_REMOTE_TEST_DATABASE=true";return false;}
            reason=string.Empty;return true;
        }
        catch{reason="TEST_DATABASE_URL invalida";return false;}
    }
    public async Task InitializeAsync()
    {
        if(!CanRun(out var reason))throw new InvalidOperationException(reason);
        await using var connection=new NpgsqlConnection(_adminConnectionString);await connection.OpenAsync();
        await using var command=new NpgsqlCommand($"CREATE SCHEMA \"{Schema}\"",connection);await command.ExecuteNonQueryAsync();
        var builder=new NpgsqlConnectionStringBuilder(_adminConnectionString){SearchPath=Schema};ConnectionString=builder.ConnectionString;
    }
    public async ValueTask DisposeAsync()
    {
        if(string.IsNullOrEmpty(ConnectionString))return;
        if(!SafeSchema.IsMatch(Schema))throw new InvalidOperationException("Unsafe test schema name; refusing cleanup.");
        await using var connection=new NpgsqlConnection(_adminConnectionString);await connection.OpenAsync();
        await using var command=new NpgsqlCommand($"DROP SCHEMA \"{Schema}\" CASCADE",connection);await command.ExecuteNonQueryAsync();
    }
}

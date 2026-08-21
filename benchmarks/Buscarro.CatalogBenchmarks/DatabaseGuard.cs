using System.Text.RegularExpressions;
using Npgsql;

namespace Buscarro.CatalogBenchmarks;

public static partial class DatabaseGuard
{
    [GeneratedRegex("^buscarro_benchmark_[0-9a-f]{32}$",RegexOptions.CultureInvariant)]
    private static partial Regex SafeSchema();

    [GeneratedRegex("^(?:buscarro_)?(?:benchmark|test)$",RegexOptions.CultureInvariant)]
    private static partial Regex SafeDatabase();

    public static bool Allowed(string? value,string? run,string? remote,out string reason)
    {
        if(!string.Equals(run,"true",StringComparison.OrdinalIgnoreCase)){reason="RUN_CATALOG_BENCHMARKS=true is required";return false;}
        if(string.IsNullOrWhiteSpace(value)){reason="BENCHMARK_DATABASE_URL is required";return false;}
        try
        {
            var builder=new NpgsqlConnectionStringBuilder(value);
            var database=builder.Database??string.Empty;
            if(!SafeDatabase().IsMatch(database)){reason="database name must be benchmark, test, buscarro_benchmark or buscarro_test";return false;}
            if(!SafeSchema().IsMatch(builder.SearchPath??string.Empty)){reason="Search Path must be an explicit buscarro_benchmark_<32 lowercase hex> schema";return false;}
            var local=(builder.Host??string.Empty).Trim().ToLowerInvariant() is "localhost" or "127.0.0.1" or "::1";
            if(!local&&!string.Equals(remote,"true",StringComparison.OrdinalIgnoreCase)){reason="remote benchmark database requires ALLOW_REMOTE_BENCHMARK_DATABASE=true";return false;}
            reason=string.Empty;return true;
        }
        catch{reason="invalid benchmark connection string";return false;}
    }
}

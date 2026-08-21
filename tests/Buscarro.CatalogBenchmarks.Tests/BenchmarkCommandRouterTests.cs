using Buscarro.CatalogBenchmarks;
using System.Text.Json;

namespace Buscarro.CatalogBenchmarks.Tests;

public sealed class BenchmarkCommandRouterTests
{
    [Theory]
    [InlineData("Host=localhost;Database=buscarro_benchmark;Username=test",false)]
    [InlineData("Host=localhost;Database=buscarro_benchmark;Username=test;Search Path=public",false)]
    [InlineData("Host=localhost;Database=buscarro_benchmark;Username=test;Search Path=buscarro_benchmark_bad",false)]
    [InlineData("Host=localhost;Database=buscarro_benchmark;Username=test;Search Path=buscarro_benchmark_0123456789abcdef0123456789abcdef",true)]
    public void Database_guard_requires_explicit_isolated_safe_schema(string connectionString,bool expected)
    {
        Assert.Equal(expected,DatabaseGuard.Allowed(connectionString,"true",null,out _));
    }

    [Fact]
    public void Database_guard_retains_run_database_name_and_remote_opt_in_requirements()
    {
        const string safe="Database=buscarro_benchmark;Search Path=buscarro_benchmark_0123456789abcdef0123456789abcdef;Username=test;";
        Assert.False(DatabaseGuard.Allowed("Host=localhost;"+safe,"false",null,out _));
        Assert.False(DatabaseGuard.Allowed("Host=localhost;Database=production;Search Path=buscarro_benchmark_0123456789abcdef0123456789abcdef","true",null,out _));
        Assert.False(DatabaseGuard.Allowed("Host=db.example;"+safe,"true",null,out _));
        Assert.True(DatabaseGuard.Allowed("Host=db.example;"+safe,"true","true",out _));
    }

    [Theory]
    [InlineData("contest-production")]
    [InlineData("latest-prod")]
    public void Database_guard_rejects_database_names_that_only_contain_test_as_a_substring(string database)
    {
        var connectionString = $"Host=localhost;Database={database};Search Path=buscarro_benchmark_0123456789abcdef0123456789abcdef;Username=test";

        Assert.False(DatabaseGuard.Allowed(connectionString, "true", null, out _));
    }

    [Fact]
    public void Parser_rejects_unknown_options_instead_of_using_a_default()
    {
        Assert.Throws<ArgumentException>(() => BenchmarkArguments.Parse(
            ["query", "--items", "10000", "--iteratons", "5"]));
    }

    [Fact]
    public void Parser_rejects_duplicate_options()
    {
        Assert.Throws<ArgumentException>(() => BenchmarkArguments.Parse(
            ["query", "--items", "10000", "--items", "100000"]));
    }

    [Theory]
    [InlineData("seed", "--iterations")]
    [InlineData("explain", "--warmup")]
    [InlineData("memory", "--concurrency")]
    public void Parser_rejects_options_that_are_irrelevant_to_the_command(string command, string option)
    {
        var items = command == "memory" ? "2000000" : "10000";

        Assert.Throws<ArgumentException>(() => BenchmarkArguments.Parse(
            [command, "--items", items, option, "1"]));
    }

    [Fact]
    public void Memory_measurement_uses_deterministic_string_ids_and_reports_process_and_managed_memory()
    {
        Assert.Equal("42",MemoryBenchmark.ItemId(42));
        var result=MemoryBenchmark.Measure(128);
        Assert.Equal(128,result.Items);Assert.Equal(128,result.Unique);
        Assert.True(result.TotalMemoryBefore>=0);Assert.True(result.TotalMemoryAfter>=0);
        Assert.True(result.WorkingSetBefore>=0);Assert.True(result.WorkingSetAfter>=0);
        Assert.True(result.BytesPerItem>=0);Assert.True(result.Elapsed.TotalMilliseconds>=0);
        using var json=JsonDocument.Parse(MemoryBenchmark.ToJson(result));
        Assert.Equal("memory",json.RootElement.GetProperty("command").GetString());
        Assert.Equal(128,json.RootElement.GetProperty("items").GetInt32());
        Assert.False(json.RootElement.GetProperty("databaseUsed").GetBoolean());
        foreach(var name in new[]{"totalMemoryBefore","totalMemoryAfter","workingSetBefore","workingSetAfter","bytesPerItem","elapsedMs"})
            Assert.True(json.RootElement.TryGetProperty(name,out _),name);
    }

    [Fact]
    public async Task Memory_is_exactly_two_million_and_bypasses_database_guard_and_dataset()
    {
        var guard=new Guard();var actions=new Actions();
        await BenchmarkCommandRouter.RunAsync(["memory","--items","2000000"],guard,actions);
        Assert.Equal(0,guard.Calls);Assert.Equal(0,actions.DatasetCalls);Assert.Equal(2_000_000,actions.MemoryItems);
        await Assert.ThrowsAsync<ArgumentException>(()=>BenchmarkCommandRouter.RunAsync(["memory","--items","1000000"],guard,actions));
    }

    [Fact]
    public async Task Database_commands_require_guard_and_only_queries_require_existing_dataset()
    {
        var guard=new Guard();var actions=new Actions();await BenchmarkCommandRouter.RunAsync(["seed","--items","10000"],guard,actions);
        Assert.Equal(1,guard.Calls);Assert.Equal(0,actions.DatasetCalls);
        await BenchmarkCommandRouter.RunAsync(["query","--items","10000"],guard,actions);
        Assert.Equal(2,guard.Calls);Assert.Equal(1,actions.DatasetCalls);
    }

    private sealed class Guard:IBenchmarkGuard { public int Calls;public void EnsureAllowed()=>Calls++; }
    private sealed class Actions:IBenchmarkActions
    {
        public int DatasetCalls;public int MemoryItems;
        public Task SeedAsync(int items)=>Task.CompletedTask;public Task EnsureDatasetAsync(int items){DatasetCalls++;return Task.CompletedTask;}
        public Task QueryAsync(BenchmarkArguments arguments)=>Task.CompletedTask;public Task ExplainAsync(int items)=>Task.CompletedTask;
        public Task MemoryAsync(int items){MemoryItems=items;return Task.CompletedTask;}
    }
}

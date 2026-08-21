using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Npgsql;
using Buscarro.CatalogBenchmarks;

NpgsqlDataSource? dataSource=null;
NpgsqlDataSource DataSource()=>dataSource??throw new InvalidOperationException("Database guard was not executed.");
var guard=new DelegateBenchmarkGuard(()=>
{
    var connectionString=Environment.GetEnvironmentVariable("BENCHMARK_DATABASE_URL");
    if(!DatabaseGuard.Allowed(connectionString,Environment.GetEnvironmentVariable("RUN_CATALOG_BENCHMARKS"),Environment.GetEnvironmentVariable("ALLOW_REMOTE_BENCHMARK_DATABASE"),out var reason))throw new InvalidOperationException(reason);
    dataSource=NpgsqlDataSource.Create(connectionString!);
});
var actions=new DelegateBenchmarkActions(Seed,EnsureDataset,a=>Measure(a.Command,a.Items,a.Iterations,a.Warmup,a.Concurrency),Explain,MeasureMemory);
try
{
    if(args is ["prepare-schema"]){guard.EnsureAllowed();await PrepareSchema();}
    else await BenchmarkCommandRouter.RunAsync(args,guard,actions);
}
finally{if(dataSource is not null)await dataSource.DisposeAsync();}

async Task PrepareSchema()
{
    var connectionString=Environment.GetEnvironmentVariable("BENCHMARK_DATABASE_URL")!;
    var schema=new NpgsqlConnectionStringBuilder(connectionString).SearchPath??throw new InvalidOperationException("Safe Search Path is required.");
    await using var connection=await DataSource().OpenConnectionAsync();
    var quotedSchema=new NpgsqlCommandBuilder().QuoteIdentifier(schema);
    await using var command=new NpgsqlCommand($"CREATE SCHEMA {quotedSchema}",connection);await command.ExecuteNonQueryAsync();
    Console.WriteLine(JsonSerializer.Serialize(new{command="prepare-schema",schema,status="created",dropped=false}));
}

async Task EnsureDataset(int expected)
{
    await using var connection=await DataSource().OpenConnectionAsync();await using var command=new NpgsqlCommand("SELECT count(*) FROM vehicle_catalog WHERE provider='benchmark'",connection);
    var actual=(long)(await command.ExecuteScalarAsync()??0L);if(actual!=expected)throw new InvalidOperationException($"benchmark dataset mismatch: expected {expected}, actual {actual}; run seed explicitly");
}

async Task Seed(int items)
{
    await using var connection=await DataSource().OpenConnectionAsync();await using var tx=await connection.BeginTransactionAsync();
    const string sql="""
      DELETE FROM provider_catalog_current WHERE provider='benchmark';
      DELETE FROM catalog_generation WHERE provider='benchmark';
      INSERT INTO catalog_generation(provider,status,created_at_utc,updated_at_utc,ready_at_utc,activated_at_utc,page_count,source_item_count,valid_item_count,invalid_item_count,duplicate_item_count,checksum_sha256)
      VALUES('benchmark',2,now(),now(),now(),now(),1,@items,@items,0,0,repeat('a',64)) RETURNING id;
      """;
    await using var create=new NpgsqlCommand(sql,connection,tx);create.Parameters.AddWithValue("items",items);var generation=(int)(await create.ExecuteScalarAsync()??throw new InvalidOperationException("generation not created"));
    await using var insert=new NpgsqlCommand("""
      INSERT INTO vehicle_catalog(generation_id,provider,external_id,title,title_normalized,brand,brand_normalized,model,model_normalized,version,version_normalized,year,mileage,fuel,fuel_normalized,transmission,transmission_normalized,color,color_normalized,price,city,city_normalized,state,state_normalized)
      SELECT @generation,'benchmark',n::text,'Benchmark vehicle '||n,'benchmark vehicle '||n,'Brand '||(n%40),'brand '||(n%40),'Model '||(n%400),'model '||(n%400),'Version '||(n%2000),'version '||(n%2000),2000+(n%26),(n%200000),'gasolina','gasolina','automatico','automatico','branco','branco',20000+(n%200000),'City '||(n%100),'city '||(n%100),'SP','sp' FROM generate_series(1,@items) n;
      """,connection,tx);insert.Parameters.AddWithValue("generation",generation);insert.Parameters.AddWithValue("items",items);await insert.ExecuteNonQueryAsync();
    await using var publication=new NpgsqlCommand("INSERT INTO catalog_publication(published_at_utc,total_catalog_count,provider_count) VALUES(now(),@items,1) RETURNING id",connection,tx);publication.Parameters.AddWithValue("items",items);var publicationId=(int)(await publication.ExecuteScalarAsync()??throw new InvalidOperationException("publication not created"));
    await using var pointer=new NpgsqlCommand("INSERT INTO provider_catalog_current(provider,current_generation_id,last_publication_id,updated_at_utc) VALUES('benchmark',@generation,@publication,now())",connection,tx);pointer.Parameters.AddWithValue("generation",generation);pointer.Parameters.AddWithValue("publication",publicationId);await pointer.ExecuteNonQueryAsync();
    await tx.CommitAsync();Console.WriteLine(JsonSerializer.Serialize(new{command="seed",items,status="completed"}));
}

async Task Explain(int items)
{
    await using var connection=await DataSource().OpenConnectionAsync();await using var command=new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) SELECT count(*) FROM vehicle_catalog WHERE provider='benchmark' AND price BETWEEN 50000 AND 100000",connection);
    var plan=(string)(await command.ExecuteScalarAsync()??"[]");Console.WriteLine(JsonSerializer.Serialize(new{command="explain",items,plan=JsonDocument.Parse(plan).RootElement}));
}

async Task Measure(string commandName,int items,int iterations,int warmup,int concurrency)
{
    async Task<double> Once()
    {
        var start=Stopwatch.GetTimestamp();await using var connection=await DataSource().OpenConnectionAsync();
        const string sql="SELECT count(*) FROM vehicle_catalog WHERE provider='benchmark' AND state_normalized='sp' AND price BETWEEN 50000 AND 100000";
        await using var cmd=new NpgsqlCommand(sql,connection);_=await cmd.ExecuteScalarAsync();
        return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }
    for(var i=0;i<warmup;i++)await Once();var before=GC.GetTotalAllocatedBytes(true);var values=new ConcurrentBag<double>();
    await Parallel.ForEachAsync(Enumerable.Range(0,iterations),new ParallelOptions{MaxDegreeOfParallelism=concurrency},async(_,_)=>values.Add(await Once()));
    var ordered=values.Order().ToArray();var allocated=GC.GetTotalAllocatedBytes(true)-before;
    double P(double percentile)=>ordered[(int)Math.Clamp(Math.Ceiling(percentile*ordered.Length)-1,0,ordered.Length-1)];
    Console.WriteLine(JsonSerializer.Serialize(new{command=commandName,items,iterations,warmup,concurrency,p50Ms=P(.50),p95Ms=P(.95),p99Ms=P(.99),allocatedBytes=allocated,coldDefinition="new process/L1 only; PostgreSQL OS/shared buffers are not flushed"}));
}

Task MeasureMemory(int items)
{
    var result=MemoryBenchmark.Measure(items);
    Console.WriteLine(MemoryBenchmark.ToJson(result));return Task.CompletedTask;
}

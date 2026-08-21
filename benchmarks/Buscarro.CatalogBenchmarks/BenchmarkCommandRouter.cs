namespace Buscarro.CatalogBenchmarks;

public interface IBenchmarkGuard { void EnsureAllowed(); }
public interface IBenchmarkActions
{
    Task SeedAsync(int items);
    Task EnsureDatasetAsync(int items);
    Task QueryAsync(BenchmarkArguments arguments);
    Task ExplainAsync(int items);
    Task MemoryAsync(int items);
}

public sealed record BenchmarkArguments(string Command,int Items,int Iterations,int Warmup,int Concurrency)
{
    public static BenchmarkArguments Parse(string[] args)
    {
        if(args.Length==0)throw new ArgumentException("command required: seed|query|explain|memory");var command=args[0];
        if(command is not("seed" or "query" or "explain" or "memory"))throw new ArgumentException("unknown command");
        var allowedOptions = command == "query"
            ? new HashSet<string>(["--items", "--iterations", "--warmup", "--concurrency"], StringComparer.Ordinal)
            : new HashSet<string>(["--items"], StringComparer.Ordinal);
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
        {
            var option = args[index];
            if (!allowedOptions.Contains(option)) throw new ArgumentException($"unknown option for {command}: {option}");
            if (!values.TryAdd(option, 0)) throw new ArgumentException($"duplicate option: {option}");
            if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out var value) || value <= 0) throw new ArgumentException($"invalid {option}");
            values[option] = value;
        }
        int Read(string name,int fallback)=>values.TryGetValue(name,out var value)?value:fallback;
        var items=Read("--items",0);if(items<=0)throw new ArgumentException("--items is required");
        if(command=="memory"&&items!=2_000_000)throw new ArgumentException("memory items must be 2000000");
        if(command!="memory"&&items is not(10_000 or 100_000 or 1_000_000))throw new ArgumentException("database items must be 10000, 100000 or 1000000");
        return new(command,items,Read("--iterations",200),Read("--warmup",20),Read("--concurrency",1));
    }
}

public static class BenchmarkCommandRouter
{
    public static async Task RunAsync(string[] args,IBenchmarkGuard guard,IBenchmarkActions actions)
    {
        var parsed=BenchmarkArguments.Parse(args);
        if(parsed.Command=="memory"){await actions.MemoryAsync(parsed.Items);return;}
        guard.EnsureAllowed();
        if(parsed.Command=="seed"){await actions.SeedAsync(parsed.Items);return;}
        await actions.EnsureDatasetAsync(parsed.Items);
        if(parsed.Command=="explain")await actions.ExplainAsync(parsed.Items);else await actions.QueryAsync(parsed);
    }
}

public sealed class DelegateBenchmarkGuard(Action action):IBenchmarkGuard { public void EnsureAllowed()=>action(); }
public sealed class DelegateBenchmarkActions(
    Func<int,Task> seed,Func<int,Task> ensureDataset,Func<BenchmarkArguments,Task> query,Func<int,Task> explain,Func<int,Task> memory):IBenchmarkActions
{
    public Task SeedAsync(int items)=>seed(items);public Task EnsureDatasetAsync(int items)=>ensureDataset(items);
    public Task QueryAsync(BenchmarkArguments arguments)=>query(arguments);public Task ExplainAsync(int items)=>explain(items);public Task MemoryAsync(int items)=>memory(items);
}

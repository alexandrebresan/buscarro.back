using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Buscarro.CatalogBenchmarks;

public sealed record MemoryBenchmarkResult(
    int Items,int Unique,long TotalMemoryBefore,long TotalMemoryAfter,long WorkingSetBefore,long WorkingSetAfter,
    double BytesPerItem,TimeSpan Elapsed);

public static class MemoryBenchmark
{
    public static string ItemId(int index)=>index.ToString(CultureInfo.InvariantCulture);

    public static MemoryBenchmarkResult Measure(int items)
    {
        if(items<=0)throw new ArgumentOutOfRangeException(nameof(items));
        Collect();
        using var process=Process.GetCurrentProcess();process.Refresh();
        var totalMemoryBefore=GC.GetTotalMemory(false);var workingSetBefore=process.WorkingSet64;
        var started=Stopwatch.GetTimestamp();var set=new HashSet<string>(items,StringComparer.Ordinal);
        for(var i=0;i<items;i++)set.Add(ItemId(i));
        var elapsed=Stopwatch.GetElapsedTime(started);
        Collect();process.Refresh();
        var totalMemoryAfter=GC.GetTotalMemory(false);var workingSetAfter=process.WorkingSet64;
        var bytesPerItem=Math.Max(0,totalMemoryAfter-totalMemoryBefore)/(double)items;
        var result=new MemoryBenchmarkResult(items,set.Count,totalMemoryBefore,totalMemoryAfter,workingSetBefore,workingSetAfter,bytesPerItem,elapsed);
        GC.KeepAlive(set);return result;
    }

    public static string ToJson(MemoryBenchmarkResult result)=>JsonSerializer.Serialize(new
    {
        command="memory",items=result.Items,unique=result.Unique,totalMemoryBefore=result.TotalMemoryBefore,totalMemoryAfter=result.TotalMemoryAfter,
        workingSetBefore=result.WorkingSetBefore,workingSetAfter=result.WorkingSetAfter,bytesPerItem=result.BytesPerItem,
        elapsedMs=result.Elapsed.TotalMilliseconds,databaseUsed=false
    });

    private static void Collect()
    {
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
    }
}

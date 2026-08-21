using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Buscarro.Application.Services;

public static class CatalogMetrics
{
    public static readonly string[] AllowedTagNames=["provider","outcome","operation","source","cache_kind","hit","reason_code"];
    public static readonly Meter Meter=new("Buscarro.Catalog","1.0.0");
    private static readonly Counter<long> SyncAttempts=Counter("catalog.sync.attempts"),SyncPages=Counter("catalog.sync.pages"),SourceItems=Counter("catalog.sync.items.source"),ValidItems=Counter("catalog.sync.items.valid"),InvalidItems=Counter("catalog.sync.items.invalid"),DuplicateItems=Counter("catalog.sync.items.duplicate"),Retries=Counter("catalog.sync.retries"),Timeouts=Counter("catalog.sync.timeouts"),Failures=Counter("catalog.sync.failures"),Publications=Counter("catalog.publications"),Rollbacks=Counter("catalog.rollbacks"),CleanupGenerations=Counter("catalog.cleanup.generations"),CleanupRows=Counter("catalog.cleanup.rows"),QueryRequests=Counter("catalog.query.requests"),CacheAccesses=Counter("catalog.cache.accesses"),LockAttempts=Counter("catalog.lock.attempts"),HealthChecks=Counter("catalog.health.checks");
    private static readonly Histogram<double> SyncDuration=Histogram("catalog.sync.duration","ms"),ProviderRequestDuration=Histogram("catalog.sync.provider_request.duration","ms"),ValidationDuration=Histogram("catalog.sync.validation.duration","ms"),WriteDuration=Histogram("catalog.sync.write.duration","ms"),ActivationDuration=Histogram("catalog.sync.activation.duration","ms"),CleanupDuration=Histogram("catalog.cleanup.duration","ms"),QueryDuration=Histogram("catalog.query.duration","ms"),MetadataDuration=Histogram("catalog.query.metadata.duration","ms"),CountDuration=Histogram("catalog.query.count.duration","ms"),PageDuration=Histogram("catalog.query.page.duration","ms"),LockWaitDuration=Histogram("catalog.lock.wait.duration","ms");
    private static readonly Histogram<long> GenerationItems=Meter.CreateHistogram<long>("catalog.generation.items"),QueryResults=Meter.CreateHistogram<long>("catalog.query.results"),FilterCategories=Meter.CreateHistogram<long>("catalog.query.filter_category_count");
    private static long _publicationId,_publicationAgeSeconds,_cacheUnits;

    static CatalogMetrics()
    {
        Meter.CreateObservableGauge("catalog.runtime.publication_id",()=>Interlocked.Read(ref _publicationId));
        Meter.CreateObservableGauge("catalog.runtime.age.seconds",()=>Interlocked.Read(ref _publicationAgeSeconds));
        Meter.CreateObservableGauge("catalog.cache.units",()=>Interlocked.Read(ref _cacheUnits));
    }

    public static void RecordQuery(string outcome,string source,long filterCategoryCount,long results,TimeSpan duration)
    {
        TagList tags=default;tags.Add("source",source);tags.Add("outcome",outcome);
        QueryRequests.Add(1,tags);QueryDuration.Record(duration.TotalMilliseconds,tags);FilterCategories.Record(filterCategoryCount,tags);QueryResults.Record(results,tags);
    }
    public static void RecordSync(string provider,string outcome,long pages,long source,long valid,long invalid,long duplicate,TimeSpan duration)
    {TagList tags=default;tags.Add("provider",provider);tags.Add("outcome",outcome);SyncAttempts.Add(1,tags);SyncPages.Add(pages,tags);SourceItems.Add(source,tags);ValidItems.Add(valid,tags);InvalidItems.Add(invalid,tags);DuplicateItems.Add(duplicate,tags);GenerationItems.Record(valid,tags);SyncDuration.Record(duration.TotalMilliseconds,tags);if(outcome!="success")Failures.Add(1,tags);}
    public static void RecordPublication(string provider,long publicationId,long total,TimeSpan age)
    {Publications.Add(1,new KeyValuePair<string,object?>("provider",provider));SetRuntimePublication(publicationId,age);GenerationItems.Record(total,new KeyValuePair<string,object?>("provider",provider));}
    public static void SetRuntimePublication(long publicationId,TimeSpan age){Interlocked.Exchange(ref _publicationId,publicationId);Interlocked.Exchange(ref _publicationAgeSeconds,Math.Max(0,(long)age.TotalSeconds));}
    public static void RecordRollback(string provider,string outcome)=>Rollbacks.Add(1,new KeyValuePair<string,object?>("provider",provider),new KeyValuePair<string,object?>("outcome",outcome));
    public static void RecordHealth(string reason)=>HealthChecks.Add(1,new KeyValuePair<string,object?>("reason_code",reason));
    public static void RecordCache(string kind,bool hit)=>CacheAccesses.Add(1,new KeyValuePair<string,object?>("cache_kind",kind),new KeyValuePair<string,object?>("hit",hit));
    public static void AdjustCacheUnits(long delta)=>Interlocked.Add(ref _cacheUnits,delta);
    public static void RecordLock(string provider,string outcome,TimeSpan wait){LockAttempts.Add(1,new KeyValuePair<string,object?>("provider",provider),new KeyValuePair<string,object?>("outcome",outcome));LockWaitDuration.Record(wait.TotalMilliseconds,new KeyValuePair<string,object?>("provider",provider));}
    public static void RecordCleanup(long generations,long rows,TimeSpan duration){CleanupGenerations.Add(generations);CleanupRows.Add(rows);CleanupDuration.Record(duration.TotalMilliseconds);}
    public static void RecordRetry(string provider)=>Retries.Add(1,new KeyValuePair<string,object?>("provider",provider));
    public static void RecordTimeout(string operation)=>Timeouts.Add(1,new KeyValuePair<string,object?>("operation",operation));
    public static void RecordProviderRequest(string provider,TimeSpan duration)=>ProviderRequestDuration.Record(duration.TotalMilliseconds,new KeyValuePair<string,object?>("provider",provider));
    public static void RecordValidation(string provider,TimeSpan duration)=>ValidationDuration.Record(duration.TotalMilliseconds,new KeyValuePair<string,object?>("provider",provider));
    public static void RecordWrite(string provider,TimeSpan duration)=>WriteDuration.Record(duration.TotalMilliseconds,new KeyValuePair<string,object?>("provider",provider));
    public static void RecordActivation(string provider,TimeSpan duration)=>ActivationDuration.Record(duration.TotalMilliseconds,new KeyValuePair<string,object?>("provider",provider));
    public static void RecordQueryStage(string operation,TimeSpan duration){var tag=new KeyValuePair<string,object?>("operation",operation);if(operation=="metadata")MetadataDuration.Record(duration.TotalMilliseconds,tag);else if(operation=="count")CountDuration.Record(duration.TotalMilliseconds,tag);else PageDuration.Record(duration.TotalMilliseconds,tag);}
    private static Counter<long> Counter(string name)=>Meter.CreateCounter<long>(name);
    private static Histogram<double> Histogram(string name,string? unit=null)=>Meter.CreateHistogram<double>(name,unit);
}

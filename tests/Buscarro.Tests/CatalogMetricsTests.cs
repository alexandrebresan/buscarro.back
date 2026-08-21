using System.Diagnostics.Metrics;
using System.Collections.Concurrent;
using Buscarro.Application.Services;
using Buscarro.Infrastructure.Services;
using Buscarro.Infrastructure.Providers;
using Microsoft.Extensions.Options;

namespace Buscarro.Tests;

public sealed class CatalogMetricsTests
{
    [Fact]
    public void Query_measurements_use_versioned_meter_and_only_bounded_allowed_tags()
    {
        var seen=new ConcurrentBag<(string Name,string[] Tags)>();using var listener=new MeterListener();
        listener.InstrumentPublished=(instrument,l)=>{if(instrument.Meter.Name=="Buscarro.Catalog"){seen.Add((instrument.Name,[]));l.EnableMeasurementEvents(instrument);}};
        listener.SetMeasurementEventCallback<long>((instrument,measurement,tags,state)=>
            seen.Add((instrument.Name,tags.ToArray().Select(x=>x.Key).ToArray())));listener.Start();
        CatalogMetrics.RecordQuery("success","postgres",3,25,TimeSpan.FromMilliseconds(4));
        Assert.Equal("1.0.0",CatalogMetrics.Meter.Version);
        Assert.Contains(seen,x=>x.Name=="catalog.query.requests"&&x.Tags.SequenceEqual(["source","outcome"]));
        Assert.All(seen.SelectMany(x=>x.Tags),tag=>Assert.Contains(tag,CatalogMetrics.AllowedTagNames));
    }

    [Fact]
    public void Cache_reports_real_units_and_hit_miss_measurements()
    {
        var measurements=new ConcurrentBag<(string Name,long Value,string[] Tags)>();using var listener=new MeterListener();
        listener.InstrumentPublished=(instrument,l)=>{if(instrument.Meter.Name=="Buscarro.Catalog")l.EnableMeasurementEvents(instrument);};
        listener.SetMeasurementEventCallback<long>((instrument,value,tags,state)=>measurements.Add((instrument.Name,value,tags.ToArray().Select(x=>$"{x.Key}={x.Value}").ToArray())));listener.Start();
        using var cache=new CatalogQueryCache(Options.Create(new CatalogQueryOptions { CacheSizeLimit=20 }));
        Assert.False(cache.TryGetCount("count",out _));cache.SetCount("count",4);Assert.True(cache.TryGetCount("count",out _));
        cache.SetPage("page",new CatalogCachedPage([new(),new()],new Dictionary<string,int>()));Assert.True(cache.TryGetPage("page",out _));
        listener.RecordObservableInstruments();
        Assert.Contains(measurements,x=>x.Name=="catalog.cache.units"&&x.Value==3);
        Assert.Contains(measurements,x=>x.Name=="catalog.cache.accesses"&&x.Tags.Contains("hit=False"));
        Assert.Contains(measurements,x=>x.Name=="catalog.cache.accesses"&&x.Tags.Contains("hit=True"));
    }
}

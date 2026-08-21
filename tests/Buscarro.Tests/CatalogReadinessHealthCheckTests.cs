using Buscarro.Domain.Entities;
using Buscarro.Api.Configurations;
using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Buscarro.Tests;

public sealed class CatalogReadinessHealthCheckTests
{
    private static readonly DateTimeOffset Now=new(2026,8,15,12,0,0,TimeSpan.Zero);

    [Fact]
    public async Task Healthy_requires_consistent_recent_global_publication_and_current_pointers()
    {
        await using var context=CreateContext();
        var generation=Ready("carzin",12);generation.MarkCurrent(Now.AddHours(-1));context.CatalogGenerations.Add(generation);
        await context.SaveChangesAsync();
        var publication=new CatalogPublication(Now.AddHours(-1),12,1);context.CatalogPublications.Add(publication);await context.SaveChangesAsync();
        var pointer=new ProviderCatalogCurrent("carzin",generation.Id);pointer.AssignPublication(publication.Id);context.ProviderCatalogCurrents.Add(pointer);await context.SaveChangesAsync();

        var result=await Create(context).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy,result.Status);Assert.Equal("ready",result.Description);
        Assert.Equal(publication.Id,result.Data["publicationId"]);Assert.Equal(3600L,result.Data["ageSeconds"]);Assert.Equal(1,result.Data["providerCount"]);
    }

    [Theory]
    [InlineData(13,"catalog_stale")]
    [InlineData(-1,"publication_from_future")]
    public async Task Unhealthy_uses_bounded_reason_for_age_failures(int publicationHoursAgo,string reason)
    {
        await using var context=CreateContext();
        var generation=Ready("carzin",1);generation.MarkCurrent(Now.AddHours(-1));context.CatalogGenerations.Add(generation);await context.SaveChangesAsync();
        var publication=new CatalogPublication(Now.AddHours(-publicationHoursAgo),1,1);context.CatalogPublications.Add(publication);await context.SaveChangesAsync();
        var pointer=new ProviderCatalogCurrent("carzin",generation.Id);pointer.AssignPublication(publication.Id);context.ProviderCatalogCurrents.Add(pointer);await context.SaveChangesAsync();
        var result=await Create(context).CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy,result.Status);Assert.Equal(reason,result.Description);
    }

    [Fact]
    public async Task Missing_publication_is_unhealthy_without_database_details()
    {
        await using var context=CreateContext();var result=await Create(context).CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy,result.Status);Assert.Equal("publication_missing",result.Description);Assert.Null(result.Exception);
    }

    [Fact]
    public async Task Latest_global_publication_does_not_require_unchanged_provider_pointer_to_reference_it()
    {
        await using var context=CreateContext();
        var carzin=Ready("carzin",12);carzin.MarkCurrent(Now.AddHours(-2));var olx=Ready("olx",8);olx.MarkCurrent(Now.AddHours(-2));
        context.AddRange(carzin,olx);await context.SaveChangesAsync();
        var oldPublication=new CatalogPublication(Now.AddHours(-2),8,1);context.Add(oldPublication);await context.SaveChangesAsync();
        var latest=new CatalogPublication(Now.AddMinutes(-5),20,2);context.Add(latest);await context.SaveChangesAsync();
        var carzinPointer=new ProviderCatalogCurrent("carzin",carzin.Id);carzinPointer.AssignPublication(latest.Id);
        var olxPointer=new ProviderCatalogCurrent("olx",olx.Id);olxPointer.AssignPublication(oldPublication.Id);
        context.AddRange(carzinPointer,olxPointer);await context.SaveChangesAsync();

        var result=await Create(context).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy,result.Status);Assert.Equal("ready",result.Description);
    }

    [Fact]
    public void Options_require_positive_timeout_and_age_and_smaller_nonnegative_skew()
    {
        Assert.True(new CatalogReadinessOptions().IsValid());
        Assert.False(new CatalogReadinessOptions { MaximumCatalogAge=TimeSpan.Zero }.IsValid());
        Assert.False(new CatalogReadinessOptions { DatabaseTimeout=TimeSpan.Zero }.IsValid());
        Assert.False(new CatalogReadinessOptions { AllowedClockSkew=TimeSpan.FromHours(12) }.IsValid());
    }

    private static CatalogReadinessHealthCheck Create(BuscarroDbContext context)=>new(
        new PostgresCatalogReadinessProbe(new TestFactory(context),Options.Create(new CatalogReadinessOptions()),new FixedTimeProvider()));
    private static BuscarroDbContext CreateContext()=>new(new DbContextOptionsBuilder<BuscarroDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static CatalogGeneration Ready(string provider,int count){var value=new CatalogGeneration(provider,Now.AddHours(-2));value.MarkReady(1,count,count,0,0,new string('a',64),null,Now.AddHours(-1));return value;}
    private sealed class FixedTimeProvider:TimeProvider { public override DateTimeOffset GetUtcNow()=>Now; }
    private sealed class TestFactory(BuscarroDbContext context):IDbContextFactory<BuscarroDbContext> { public BuscarroDbContext CreateDbContext()=>context;public Task<BuscarroDbContext> CreateDbContextAsync(CancellationToken cancellationToken=default)=>Task.FromResult(context); }
}

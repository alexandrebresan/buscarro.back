using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Handlers;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Domain.Enums;
using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Buscarro.Tests;

public sealed class CatalogRollbackTests
{
    [Fact]
    public async Task Publisher_swaps_exact_previous_once_and_creates_new_global_publication()
    {
        await using var context=CreateContext();var publisher=new PostgresCatalogPublisher(context);
        var first=await AddReady(context,"carzin",10);await publisher.PublishAsync(first.Id,DateTimeOffset.UtcNow,CancellationToken.None);
        var second=await AddReady(context,"carzin",20);await publisher.PublishAsync(second.Id,DateTimeOffset.UtcNow.AddMinutes(1),CancellationToken.None);
        var olx=await AddReady(context,"olx",7);await publisher.PublishAsync(olx.Id,DateTimeOffset.UtcNow.AddMinutes(2),CancellationToken.None);

        var result=await publisher.RollbackAsync("carzin",first.Id,DateTimeOffset.UtcNow.AddMinutes(3),CancellationToken.None);

        var pointer=context.ProviderCatalogCurrents.Single(x=>x.Provider=="carzin");
        Assert.Equal(first.Id,pointer.CurrentGenerationId);Assert.Equal(second.Id,pointer.PreviousGenerationId);
        Assert.Equal(CatalogGenerationStatus.Current,first.Status);Assert.Equal(CatalogGenerationStatus.Previous,second.Status);
        Assert.Equal(17,result.TotalCatalogCount);Assert.Equal(result.PublicationId,pointer.LastPublicationId);
        await Assert.ThrowsAsync<ConflictException>(()=>publisher.RollbackAsync("carzin",first.Id,DateTimeOffset.UtcNow.AddMinutes(4),CancellationToken.None));
    }

    [Fact]
    public async Task Handler_validates_and_requires_provider_lock()
    {
        var publisher=new FakePublisher();var handler=new RollbackCatalogHandler(new DeniedLock(),publisher,TimeProvider.System);
        await Assert.ThrowsAsync<ValidationException>(()=>handler.Handle(new RollbackCatalogCommand(" ",1),CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(()=>handler.Handle(new RollbackCatalogCommand("carzin",0),CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(()=>handler.Handle(new RollbackCatalogCommand("carzin",1),CancellationToken.None));
        Assert.Equal(0,publisher.Calls);
    }

    private static BuscarroDbContext CreateContext()=>new(new DbContextOptionsBuilder<BuscarroDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<CatalogGeneration> AddReady(BuscarroDbContext db,string provider,int count){var g=new CatalogGeneration(provider,DateTimeOffset.UtcNow.AddHours(-1));g.MarkReady(1,count,count,0,0,new string('b',64),null,DateTimeOffset.UtcNow);db.Add(g);await db.SaveChangesAsync();return g;}
    private sealed class DeniedLock:IProviderSynchronizationLock { public Task<IAsyncDisposable?> TryAcquireAsync(string provider,CancellationToken cancellationToken)=>Task.FromResult<IAsyncDisposable?>(null); }
    private sealed class FakePublisher:ICatalogPublisher { public int Calls;public Task<CatalogPublication> PublishAsync(int id,DateTimeOffset at,CancellationToken ct)=>throw new NotSupportedException();public Task<CatalogRollbackResult> RollbackAsync(string provider,int target,DateTimeOffset at,CancellationToken ct){Calls++;return Task.FromResult(new CatalogRollbackResult(provider,1,target,2,at,0));} }
}

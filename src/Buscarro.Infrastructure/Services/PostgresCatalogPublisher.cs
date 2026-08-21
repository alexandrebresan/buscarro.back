using Buscarro.Application.Interfaces;
using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Domain.Entities;
using Buscarro.Domain.Enums;
using Buscarro.Domain.Interfaces;
using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Buscarro.Application.Services;
using System.Diagnostics;

namespace Buscarro.Infrastructure.Services;

public sealed class PostgresCatalogPublisher : ICatalogPublisher
{
    private const long PublicationAdvisoryLock = 740_279_021;
    private readonly BuscarroDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public PostgresCatalogPublisher(BuscarroDbContext dbContext)
        : this(dbContext, new Buscarro.Infrastructure.UnitOfWork.UnitOfWork(dbContext))
    {
    }

    public PostgresCatalogPublisher(BuscarroDbContext dbContext, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<CatalogPublication> PublishAsync(
        int readyGenerationId,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken)
    {
        var activationStarted=Stopwatch.GetTimestamp();
        if (publishedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use UTC offset.", nameof(publishedAtUtc));
        }

        CatalogPublication? result = null;
        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (_dbContext.Database.IsNpgsql())
            {
                await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock({PublicationAdvisoryLock})",
                    token);
            }

            var generation = await LoadGenerationForUpdateAsync(readyGenerationId, token)
                ?? throw new InvalidOperationException($"Catalog generation {readyGenerationId} was not found.");
            var pointer = await LoadPointerForUpdateAsync(generation.Provider, token);

            if (generation.Status == CatalogGenerationStatus.Current)
            {
                if (pointer?.CurrentGenerationId != generation.Id || pointer.LastPublicationId is null)
                {
                    throw new InvalidOperationException("Current catalog generation has inconsistent publication pointers.");
                }

                result = await _dbContext.CatalogPublications.SingleOrDefaultAsync(
                        value => value.Id == pointer.LastPublicationId.Value,
                        token)
                    ?? throw new InvalidOperationException("The referenced catalog publication was not found.");
                return;
            }

            if (generation.Status != CatalogGenerationStatus.Ready)
            {
                throw new InvalidOperationException($"Only Ready generations can be published; generation is {generation.Status}.");
            }

            int? previousGenerationId = null;
            CatalogGeneration? oldPrevious = null;
            CatalogGeneration? oldCurrent = null;
            if (pointer is not null)
            {
                if (pointer.PreviousGenerationId is not null)
                {
                    oldPrevious = await LoadGenerationForUpdateAsync(pointer.PreviousGenerationId.Value, token)
                        ?? throw new InvalidOperationException("Previous generation pointer is inconsistent.");
                    if (oldPrevious.Status != CatalogGenerationStatus.Previous || oldPrevious.Provider != generation.Provider)
                    {
                        throw new InvalidOperationException("Previous generation pointer is inconsistent.");
                    }
                }

                oldCurrent = await LoadGenerationForUpdateAsync(pointer.CurrentGenerationId, token)
                    ?? throw new InvalidOperationException("Current generation pointer is inconsistent.");
                if (oldCurrent.Status != CatalogGenerationStatus.Current || oldCurrent.Provider != generation.Provider)
                {
                    throw new InvalidOperationException("Current generation pointer is inconsistent.");
                }

                previousGenerationId = oldCurrent.Id;
            }

            oldPrevious?.MarkInactiveReady(publishedAtUtc);
            oldCurrent?.MarkPrevious(publishedAtUtc);
            generation.MarkCurrent(publishedAtUtc);
            if (pointer is null)
            {
                pointer = new ProviderCatalogCurrent(generation.Provider, generation.Id);
                _dbContext.ProviderCatalogCurrents.Add(pointer);
            }
            pointer.Publish(generation.Id, previousGenerationId, publishedAtUtc);

            await _dbContext.SaveChangesAsync(token);

            var totals = await _dbContext.ProviderCatalogCurrents
                .Join(
                    _dbContext.CatalogGenerations,
                    current => current.CurrentGenerationId,
                    currentGeneration => currentGeneration.Id,
                    (current, currentGeneration) => new { current.Provider, currentGeneration.ValidItemCount })
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    Total = group.Sum(value => value.ValidItemCount),
                    Providers = group.Count()
                })
                .SingleAsync(token);

            result = new CatalogPublication(publishedAtUtc, totals.Total, totals.Providers);
            _dbContext.CatalogPublications.Add(result);
            await _dbContext.SaveChangesAsync(token);
            pointer.AssignPublication(result.Id);
        }, cancellationToken);

        var publication=result ?? throw new InvalidOperationException("Catalog publication did not produce a result.");
        CatalogMetrics.RecordPublication("all",publication.Id,publication.TotalCatalogCount,TimeSpan.Zero);
        CatalogMetrics.RecordActivation("all",Stopwatch.GetElapsedTime(activationStarted));
        return publication;
    }

    public async Task<CatalogRollbackResult> RollbackAsync(string provider,int targetGenerationId,DateTimeOffset publishedAtUtc,CancellationToken cancellationToken)
    {
        provider=provider?.Trim()??string.Empty;
        if(provider.Length==0)throw new ArgumentException("Provider is required.",nameof(provider));
        if(targetGenerationId<=0)throw new ArgumentOutOfRangeException(nameof(targetGenerationId));
        if(publishedAtUtc.Offset!=TimeSpan.Zero)throw new ArgumentException("Timestamp must use UTC offset.",nameof(publishedAtUtc));
        CatalogRollbackResult? result=null;
        await _unitOfWork.ExecuteInTransactionAsync(async token=>
        {
            if(_dbContext.Database.IsNpgsql())
                await _dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({PublicationAdvisoryLock})",token);
            var pointer=await LoadPointerForUpdateAsync(provider,token);
            if(pointer is null||pointer.PreviousGenerationId!=targetGenerationId)
                throw new ConflictException("A geracao solicitada nao e o rollback anterior disponivel.");
            var oldCurrent=await LoadGenerationForUpdateAsync(pointer.CurrentGenerationId,token);
            var target=await LoadGenerationForUpdateAsync(targetGenerationId,token);
            if(oldCurrent is null||target is null||oldCurrent.Provider!=provider||target.Provider!=provider||
                oldCurrent.Status!=CatalogGenerationStatus.Current||target.Status!=CatalogGenerationStatus.Previous)
                throw new ConflictException("Os ponteiros de rollback do catalogo estao inconsistentes.");
            oldCurrent.MarkPrevious(publishedAtUtc);target.RestoreAsCurrent(publishedAtUtc);
            pointer.Publish(target.Id,oldCurrent.Id,publishedAtUtc);
            await _dbContext.SaveChangesAsync(token);
            var totals=await _dbContext.ProviderCatalogCurrents.Join(_dbContext.CatalogGenerations,
                    p=>p.CurrentGenerationId,g=>g.Id,(p,g)=>new {g.ValidItemCount})
                .GroupBy(_=>1).Select(group=>new {Total=group.Sum(x=>x.ValidItemCount),Providers=group.Count()}).SingleAsync(token);
            var publication=new CatalogPublication(publishedAtUtc,totals.Total,totals.Providers);
            _dbContext.CatalogPublications.Add(publication);await _dbContext.SaveChangesAsync(token);
            pointer.AssignPublication(publication.Id);
            result=new CatalogRollbackResult(provider,publication.Id,target.Id,oldCurrent.Id,publishedAtUtc,totals.Total);
        },cancellationToken);
        var rollback=result??throw new InvalidOperationException("Catalog rollback did not produce a result.");
        CatalogMetrics.RecordRollback(provider,"success");CatalogMetrics.RecordPublication("all",rollback.PublicationId,rollback.TotalCatalogCount,TimeSpan.Zero);
        return rollback;
    }

    private Task<CatalogGeneration?> LoadGenerationForUpdateAsync(int id, CancellationToken cancellationToken)
        => _dbContext.Database.IsNpgsql()
            ? _dbContext.CatalogGenerations
                .FromSqlInterpolated($"SELECT * FROM catalog_generation WHERE id = {id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
            : _dbContext.CatalogGenerations.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);

    private Task<ProviderCatalogCurrent?> LoadPointerForUpdateAsync(string provider, CancellationToken cancellationToken)
        => _dbContext.Database.IsNpgsql()
            ? _dbContext.ProviderCatalogCurrents
                .FromSqlInterpolated($"SELECT * FROM provider_catalog_current WHERE provider = {provider} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
            : _dbContext.ProviderCatalogCurrents.SingleOrDefaultAsync(value => value.Provider == provider, cancellationToken);
}

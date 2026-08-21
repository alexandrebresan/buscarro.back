using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using MediatR;
using Buscarro.Application.Services;

namespace Buscarro.Application.Handlers;

public sealed class RollbackCatalogHandler(IProviderSynchronizationLock synchronizationLock,ICatalogPublisher publisher,TimeProvider timeProvider)
    :IRequestHandler<RollbackCatalogCommand,CatalogRollbackResult>
{
    public async Task<CatalogRollbackResult> Handle(RollbackCatalogCommand request,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var provider=request.Provider?.Trim()??string.Empty;
        if(provider.Length==0)throw new ValidationException("Provider e obrigatorio.");
        if(request.TargetGenerationId<=0)throw new ValidationException("Target generation deve ser positivo.");
        await using var lease=await synchronizationLock.TryAcquireAsync(provider,cancellationToken);
        if(lease is null){CatalogMetrics.RecordRollback(provider,"lock_unavailable");throw new ConflictException("O catalogo do provider esta sendo atualizado.");}
        try{return await publisher.RollbackAsync(provider,request.TargetGenerationId,timeProvider.GetUtcNow(),cancellationToken);}
        catch{CatalogMetrics.RecordRollback(provider,"failure");throw;}
    }
}

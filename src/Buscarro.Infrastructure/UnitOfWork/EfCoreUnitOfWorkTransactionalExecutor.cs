using Buscarro.Infrastructure.Persistence;
using Buscarro.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Buscarro.Infrastructure.UnitOfWork;

internal interface IUnitOfWorkTransactionalExecutor
{
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken);
}

internal interface IUnitOfWorkExecutionStrategy
{
    Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken);
}

internal interface IUnitOfWorkTransactionFactory
{
    ValueTask<IUnitOfWorkTransaction> BeginAsync(CancellationToken cancellationToken);
}

internal interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
    Task RollbackAsync(CancellationToken cancellationToken);
}

internal sealed class EfCoreUnitOfWorkTransactionalExecutor(
    BuscarroDbContext dbContext,
    IUnitOfWorkExecutionStrategy executionStrategy,
    IUnitOfWorkTransactionFactory transactionFactory) : IUnitOfWorkTransactionalExecutor
{
    public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return executionStrategy.ExecuteAsync(async strategyToken =>
        {
            IUnitOfWorkTransaction? transaction = null;
            try
            {
                transaction = await transactionFactory.BeginAsync(strategyToken);
            }
            catch
            {
                dbContext.ChangeTracker.Clear();
                throw;
            }

            try
            {
                try
                {
                    await operation(strategyToken);
                    await dbContext.SaveChangesAsync(strategyToken);
                }
                catch
                {
                    await RollbackBestEffortAsync(transaction);
                    dbContext.ChangeTracker.Clear();
                    throw;
                }

                try
                {
                    await transaction.CommitAsync(strategyToken);
                }
                catch (Exception commitException)
                {
                    await RollbackBestEffortAsync(transaction);
                    dbContext.ChangeTracker.Clear();
                    throw new TransactionCommitOutcomeUnknownException(commitException);
                }
            }
            finally
            {
                await transaction.DisposeAsync();
            }
        }, cancellationToken);
    }

    private static async Task RollbackBestEffortAsync(IUnitOfWorkTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch
        {
            // The original operation/save/commit exception is authoritative.
        }
    }
}

internal sealed class EfCoreExecutionStrategyAdapter(BuscarroDbContext dbContext) : IUnitOfWorkExecutionStrategy
{
    public Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(operation, cancellationToken);
    }
}

internal sealed class EfCoreTransactionFactory(BuscarroDbContext dbContext) : IUnitOfWorkTransactionFactory
{
    public async ValueTask<IUnitOfWorkTransaction> BeginAsync(CancellationToken cancellationToken)
        => new EfCoreTransaction(
            await dbContext.Database.BeginTransactionAsync(cancellationToken));
}

internal sealed class EfCoreTransaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken)
        => transaction.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken)
        => transaction.RollbackAsync(cancellationToken);

    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}

using Buscarro.Domain.Interfaces;
using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Buscarro.Infrastructure.UnitOfWork;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly BuscarroDbContext _dbContext;
    private readonly IUnitOfWorkTransactionalExecutor? _transactionalExecutor;

    public UnitOfWork(BuscarroDbContext dbContext)
    {
        _dbContext = dbContext;
        if (dbContext.Database.IsRelational())
        {
            _transactionalExecutor = new EfCoreUnitOfWorkTransactionalExecutor(
                dbContext,
                new EfCoreExecutionStrategyAdapter(dbContext),
                new EfCoreTransactionFactory(dbContext));
        }
    }

    internal UnitOfWork(
        BuscarroDbContext dbContext,
        IUnitOfWorkTransactionalExecutor transactionalExecutor)
    {
        _dbContext = dbContext;
        _transactionalExecutor = transactionalExecutor;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_transactionalExecutor is null)
        {
            await operation(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        await _transactionalExecutor.ExecuteAsync(operation, cancellationToken);
    }
}

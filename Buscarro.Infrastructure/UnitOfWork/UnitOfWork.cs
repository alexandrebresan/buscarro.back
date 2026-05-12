using Buscarro.Domain.Interfaces;
using Buscarro.Infrastructure.Persistence;

namespace Buscarro.Infrastructure.UnitOfWork;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly BuscarroDbContext _dbContext;

    public UnitOfWork(BuscarroDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}

namespace Buscarro.Domain.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Executes an operation transactionally. Infrastructure may retry failures that happen before commit,
    /// so the operation must reload its graph on every invocation. A commit failure has an unknown outcome
    /// and must not be retried automatically; callers must reconcile state using an idempotency key.
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
        => operation(cancellationToken);
}

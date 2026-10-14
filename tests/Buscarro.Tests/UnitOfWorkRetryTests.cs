using System.Diagnostics;
using Buscarro.Application.Exceptions;
using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Buscarro.Tests;

public sealed class UnitOfWorkRetryTests
{
    [Fact]
    public async Task Unique_constraint_failure_is_translated_only_after_rollback_and_tracker_clear()
    {
        await using var context = CreateContext();
        var transactions = new FakeTransactionFactory();
        var unitOfWork = new UnitOfWork(context, new EfCoreUnitOfWorkTransactionalExecutor(context, new RetryOnceExecutionStrategy(), transactions));
        await Assert.ThrowsAsync<UniqueConstraintException>(() => unitOfWork.ExecuteInTransactionAsync(token =>
        {
            context.Users.Add(new User { Name = "Conflict", Email = "conflict@test" });
            throw new DbUpdateException("Conflict", new PostgresException("duplicate", "ERROR", "ERROR", "23505"));
        }, default));
        Assert.Equal(1, transactions.RollbackCount);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Pre_commit_failure_clears_tracker_before_execution_strategy_retries()
    {
        await using var context = CreateContext();
        var user = new User { Name = "Before", Email = "retry@test", PasswordHash = "hash" };
        context.Users.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var strategy = new RetryOnceExecutionStrategy();
        var transactions = new FakeTransactionFactory();
        var executor = new EfCoreUnitOfWorkTransactionalExecutor(context, strategy, transactions);
        var unitOfWork = new UnitOfWork(context, executor);
        var loadedGraphs = new List<User>();

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var loaded = await context.Users.SingleAsync(token);
            loadedGraphs.Add(loaded);
            loaded.Name = $"Attempt {loadedGraphs.Count}";
            await context.SaveChangesAsync(token);
            if (loadedGraphs.Count == 1)
            {
                throw new RetryableOperationException();
            }
        }, TestContext.Current.CancellationToken);

        Assert.Equal(2, strategy.AttemptCount);
        Assert.Equal(2, loadedGraphs.Count);
        Assert.NotSame(loadedGraphs[0], loadedGraphs[1]);
        Assert.Equal(1, transactions.RollbackCount);
        Assert.Single(context.ChangeTracker.Entries<User>());
        Assert.Same(loadedGraphs[1], context.ChangeTracker.Entries<User>().Single().Entity);
    }

    [Fact]
    public async Task Unknown_commit_outcome_is_not_retried_and_rollback_failure_does_not_mask_commit_error()
    {
        await using var context = CreateContext();
        var strategy = new RetryOnceExecutionStrategy();
        var commitError = new RetryableOperationException("commit outcome unknown");
        var transactions = new FakeTransactionFactory(
            commitException: commitError,
            rollbackException: new InvalidOperationException("rollback also failed"));
        var executor = new EfCoreUnitOfWorkTransactionalExecutor(context, strategy, transactions);
        var unitOfWork = new UnitOfWork(context, executor);

        var exception = await Assert.ThrowsAsync<TransactionCommitOutcomeUnknownException>(() =>
            unitOfWork.ExecuteInTransactionAsync(token =>
            {
                context.Users.Add(new User { Name = "Unknown", Email = "unknown@test", PasswordHash = "hash" });
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken));

        Assert.Same(commitError, exception.InnerException);
        Assert.Equal(1, strategy.AttemptCount);
        Assert.Equal(1, transactions.RollbackCount);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Operation_exception_rolls_back_clears_tracker_and_propagates()
    {
        await using var context = CreateContext();
        var transactions = new FakeTransactionFactory();
        var executor = new EfCoreUnitOfWorkTransactionalExecutor(
            context,
            new RetryOnceExecutionStrategy(),
            transactions);
        var unitOfWork = new UnitOfWork(context, executor);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.ExecuteInTransactionAsync(token =>
            {
                context.Users.Add(new User { Name = "Transient graph", Email = "graph@test", PasswordHash = "hash" });
                throw new InvalidOperationException("operation failed");
            }, TestContext.Current.CancellationToken));

        Assert.Equal("operation failed", exception.Message);
        Assert.Equal(1, transactions.RollbackCount);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Cancellation_rolls_back_clears_tracker_and_propagates()
    {
        await using var context = CreateContext();
        var transactions = new FakeTransactionFactory();
        var executor = new EfCoreUnitOfWorkTransactionalExecutor(
            context,
            new RetryOnceExecutionStrategy(),
            transactions);
        var unitOfWork = new UnitOfWork(context, executor);
        using var source = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            unitOfWork.ExecuteInTransactionAsync(token =>
            {
                context.Users.Add(new User { Name = "Cancelled graph", Email = "cancel@test", PasswordHash = "hash" });
                source.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }, source.Token));

        Assert.Equal(1, transactions.RollbackCount);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Native_execution_strategy_observes_cancellation_during_retry_backoff()
    {
        var options = new DbContextOptionsBuilder<BuscarroDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=retry_test;Username=test;Password=test",
                npgsql => npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null))
            .Options;
        await using var context = new BuscarroDbContext(options);
        var strategy = new EfCoreExecutionStrategyAdapter(context);
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            strategy.ExecuteAsync(_ =>
            {
                return Task.FromException(new NpgsqlException("transient", new TimeoutException()));
            }, source.Token));

        stopwatch.Stop();
        Assert.True(source.IsCancellationRequested);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"Cancellation took {stopwatch.Elapsed}.");
    }

    private static BuscarroDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BuscarroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BuscarroDbContext(options);
    }

    private sealed class RetryOnceExecutionStrategy : IUnitOfWorkExecutionStrategy
    {
        public int AttemptCount { get; private set; }

        public async Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AttemptCount++;
                try
                {
                    await operation(cancellationToken);
                    return;
                }
                catch (RetryableOperationException) when (AttemptCount == 1)
                {
                }
            }
        }
    }

    private sealed class FakeTransactionFactory(
        Exception? commitException = null,
        Exception? rollbackException = null) : IUnitOfWorkTransactionFactory
    {
        public int RollbackCount { get; private set; }

        public ValueTask<IUnitOfWorkTransaction> BeginAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IUnitOfWorkTransaction>(
                new FakeTransaction(commitException, rollbackException, () => RollbackCount++));
        }
    }

    private sealed class FakeTransaction(
        Exception? commitException,
        Exception? rollbackException,
        Action rolledBack) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken)
            => commitException is null ? Task.CompletedTask : Task.FromException(commitException);

        public Task RollbackAsync(CancellationToken cancellationToken)
        {
            rolledBack();
            return rollbackException is null ? Task.CompletedTask : Task.FromException(rollbackException);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RetryableOperationException(string? message = null) : Exception(message);
}

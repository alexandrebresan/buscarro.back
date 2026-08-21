using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Buscarro.Tests;

public sealed class CatalogGenerationFailureRecorderTests
{
    [Fact]
    public async Task Recorder_failure_returns_false_and_logs_only_safe_structured_context()
    {
        var logger = new RecordingLogger();
        var recorder = new CatalogGenerationFailureRecorder(new ThrowingFactory(), logger);

        var recorded = await recorder.TryMarkLoadingAsFailedAsync(
            42, "safe_category", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.False(recorded);
        Assert.Null(logger.Exception);
        Assert.Contains("42", logger.Message);
        Assert.Contains(nameof(InvalidOperationException), logger.Message);
        Assert.DoesNotContain("sensitive-connection-details", logger.Message);
    }

    private sealed class ThrowingFactory : IDbContextFactory<BuscarroDbContext>
    {
        public BuscarroDbContext CreateDbContext() => throw new InvalidOperationException("sensitive-connection-details");
    }

    private sealed class RecordingLogger : ILogger<CatalogGenerationFailureRecorder>
    {
        public string Message { get; private set; } = string.Empty;
        public Exception? Exception { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        { Message = formatter(state, exception); Exception = exception; }
    }
}

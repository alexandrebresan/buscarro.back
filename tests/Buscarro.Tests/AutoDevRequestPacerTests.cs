using System.Diagnostics;
using Buscarro.Infrastructure.Providers;

namespace Buscarro.Tests;

public sealed class AutoDevRequestPacerTests
{
    [Fact]
    public async Task Concurrent_waiters_are_serialized_at_the_configured_five_per_second_limit()
    {
        var pacer = new AutoDevRequestPacer(TimeProvider.System, new AutoDevCatalogSourceOptions());
        var clock = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => pacer.WaitAsync(TestContext.Current.CancellationToken)));
        Assert.True(clock.Elapsed >= TimeSpan.FromMilliseconds(170));
    }

    [Fact]
    public async Task Cancellation_stops_a_waiting_request()
    {
        var pacer = new AutoDevRequestPacer(TimeProvider.System, new AutoDevCatalogSourceOptions());
        await pacer.WaitAsync(TestContext.Current.CancellationToken);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pacer.WaitAsync(cancelled.Token));
    }
}

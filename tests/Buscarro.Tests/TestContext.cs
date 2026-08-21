namespace Buscarro.Tests;

internal static class TestContext
{
    public static TestExecutionContext Current { get; } = new();

    internal sealed class TestExecutionContext
    {
        public CancellationToken CancellationToken => CancellationToken.None;
    }
}

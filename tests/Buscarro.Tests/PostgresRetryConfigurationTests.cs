namespace Buscarro.Tests;

public sealed class PostgresRetryConfigurationTests
{
    [Fact]
    public void Api_configures_npgsql_retry_on_failure()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Buscarro.Api",
            "Configurations",
            "ApiHostComposition.cs"));

        Assert.Contains("EnableRetryOnFailure", program);
    }

    [Fact]
    public void Seed_sync_configures_npgsql_retry_on_failure()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "Buscarro.CarzinSeedSync", "Program.cs"));

        Assert.Contains("EnableRetryOnFailure", program);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Buscarro.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root not found.");
    }
}

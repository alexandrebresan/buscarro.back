using System.Text.Json;
using System.Text.RegularExpressions;

namespace Buscarro.Tests;

public sealed partial class VersionedConfigurationSecurityTests
{
    [Fact]
    public void Versioned_appsettings_do_not_contain_deployable_secrets()
    {
        var repositoryRoot = FindRepositoryRoot();
        var appsettingsFiles = Directory
            .EnumerateFiles(repositoryRoot, "appsettings*.json", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(appsettingsFiles);

        foreach (var path in appsettingsFiles)
        {
            var contents = File.ReadAllText(path);
            Assert.DoesNotMatch(DeployableSecretPattern(), contents);
        }

        var apiSettingsPath = appsettingsFiles.Single(path =>
            path.EndsWith(Path.Combine("src", "Buscarro.Api", "appsettings.json"), StringComparison.OrdinalIgnoreCase));
        using var apiSettings = JsonDocument.Parse(File.ReadAllText(apiSettingsPath));

        Assert.True(apiSettings.RootElement.TryGetProperty("OpenAI", out var openAi));
        Assert.True(openAi.TryGetProperty("ApiKey", out var apiKey));
        Assert.True(string.IsNullOrWhiteSpace(apiKey.GetString()));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Buscarro.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    [GeneratedRegex("(?:sk-(?:proj-)?[A-Za-z0-9_-]{16,}|AKIA[0-9A-Z]{16}|Bearer\\s+[A-Za-z0-9._-]{16,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----)", RegexOptions.IgnoreCase)]
    private static partial Regex DeployableSecretPattern();
}

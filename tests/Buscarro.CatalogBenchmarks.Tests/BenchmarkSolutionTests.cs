using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Buscarro.CatalogBenchmarks.Tests;

public sealed class BenchmarkSolutionTests
{
    [Fact]
    public void Benchmark_projects_are_isolated_from_the_main_solution()
    {
        var repositoryRoot = FindRepositoryRoot();
        var mainSolution = File.ReadAllText(Path.Combine(repositoryRoot, "Buscarro.sln"));

        Assert.DoesNotContain("Buscarro.CatalogBenchmarks", mainSolution, StringComparison.Ordinal);
        AssertMainSolutionHasNoTransitiveBenchmarkReference(repositoryRoot, mainSolution);

        var benchmarkSolutionPath = Path.Combine(repositoryRoot, "Buscarro.Benchmarks.sln");
        Assert.True(File.Exists(benchmarkSolutionPath), "Buscarro.Benchmarks.sln must exist.");

        var benchmarkSolution = File.ReadAllText(benchmarkSolutionPath);
        Assert.Contains("benchmarks\\Buscarro.CatalogBenchmarks\\Buscarro.CatalogBenchmarks.csproj", benchmarkSolution, StringComparison.Ordinal);
        Assert.Contains("tests\\Buscarro.CatalogBenchmarks.Tests\\Buscarro.CatalogBenchmarks.Tests.csproj", benchmarkSolution, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Buscarro.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private static void AssertMainSolutionHasNoTransitiveBenchmarkReference(string repositoryRoot, string mainSolution)
    {
        var projectPaths = Regex.Matches(mainSolution, "\"([^\"]+\\.csproj)\"")
            .Select(match => Path.GetFullPath(Path.Combine(repositoryRoot, match.Groups[1].Value)))
            .ToArray();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var projectPath in projectPaths)
        {
            AssertNoTransitiveBenchmarkReference(projectPath, visited);
        }
    }

    private static void AssertNoTransitiveBenchmarkReference(string projectPath, HashSet<string> visited)
    {
        if (!visited.Add(projectPath))
        {
            return;
        }

        var project = XDocument.Load(projectPath);
        var references = project.Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!, include!)))
            .ToArray();

        Assert.DoesNotContain(references, reference =>
            reference.Contains($"{Path.DirectorySeparatorChar}benchmarks{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        foreach (var reference in references.Where(File.Exists))
        {
            AssertNoTransitiveBenchmarkReference(reference, visited);
        }
    }
}

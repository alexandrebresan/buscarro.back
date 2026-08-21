using System.Diagnostics;

namespace Buscarro.CatalogBenchmarks.Tests;

public sealed class BenchmarkScriptTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task Script_uses_repository_paths_and_stops_after_any_failed_dotnet_command(int failedCall)
    {
        var repositoryRoot = FindRepositoryRoot();
        var temporaryDirectory = Directory.CreateTempSubdirectory("buscarro-benchmark-script-");
        var invocationLog = Path.Combine(temporaryDirectory.FullName, "dotnet-invocations.txt");
        var callCount = Path.Combine(temporaryDirectory.FullName, "dotnet-call-count.txt");
        var fakeDotnet = Path.Combine(temporaryDirectory.FullName, "dotnet.cmd");
        await File.WriteAllTextAsync(fakeDotnet, "@echo off\r\nset count=0\r\nif exist \"%DOTNET_CALL_COUNT%\" set /p count=<\"%DOTNET_CALL_COUNT%\"\r\nset /a count+=1\r\n>\"%DOTNET_CALL_COUNT%\" echo %count%\r\necho %*>>\"%DOTNET_INVOCATIONS%\"\r\nif \"%count%\"==\"%DOTNET_FAIL_ON_CALL%\" exit /b 23\r\nexit /b 0\r\n");

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.System),
                        "WindowsPowerShell", "v1.0", "powershell.exe"),
                    WorkingDirectory = temporaryDirectory.FullName,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.StartInfo.ArgumentList.Add("-NoProfile");
            process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
            process.StartInfo.ArgumentList.Add("Bypass");
            process.StartInfo.ArgumentList.Add("-File");
            process.StartInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "scripts", "run-catalog-benchmarks.ps1"));
            process.StartInfo.Environment["RUN_CATALOG_BENCHMARKS"] = "true";
            process.StartInfo.Environment["BENCHMARK_ADMIN_DATABASE_URL"] = "Host=localhost;Database=buscarro_benchmark;Username=test";
            process.StartInfo.Environment["DOTNET_INVOCATIONS"] = invocationLog;
            process.StartInfo.Environment["DOTNET_CALL_COUNT"] = callCount;
            process.StartInfo.Environment["DOTNET_FAIL_ON_CALL"] = failedCall.ToString();
            process.StartInfo.Environment["PATH"] = temporaryDirectory.FullName + Path.PathSeparator + process.StartInfo.Environment["PATH"];

            process.Start();
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.NotEqual(0, process.ExitCode);
            Assert.True(
                File.Exists(invocationLog),
                $"The fake dotnet was not invoked.{Environment.NewLine}stdout: {await standardOutput}{Environment.NewLine}stderr: {await standardError}");
            var invocations = await File.ReadAllLinesAsync(invocationLog);
            Assert.Equal(failedCall, invocations.Length);
            Assert.Contains(Path.Combine(repositoryRoot, "Buscarro.Benchmarks.sln"), invocations[0], StringComparison.OrdinalIgnoreCase);
            foreach (var invocation in invocations.Skip(1))
            {
                Assert.Contains(
                    Path.Combine(repositoryRoot, "benchmarks", "Buscarro.CatalogBenchmarks", "Buscarro.CatalogBenchmarks.csproj"),
                    invocation,
                    StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            temporaryDirectory.Delete(recursive: true);
        }
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
}

using System.Diagnostics;
using Xunit;

namespace Autonocraft.Tests;

public class IntegrationTestFacts
{
    [Fact]
    public void FullIntegrationSuite()
    {
        string repoRoot = FindRepoRoot();
        string projectPath = Path.Combine(repoRoot, "src", "Autonocraft", "Autonocraft.csproj");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectPath}\" -- --test",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start integration test process.");

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit(TimeSpan.FromMinutes(5));

        Assert.True(process.ExitCode == 0, output + error);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Autonocraft.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate Autonocraft repository root.");
    }
}

namespace Autonocraft.AtlasBuild;

internal static class AtlasPaths
{
    public static (string LayoutPath, string OutputPath) Resolve(string? layoutOverride, string? outputOverride)
    {
        string repoRoot = FindRepoRoot();
        string layoutPath = layoutOverride
            ?? Path.Combine(repoRoot, "src", "Autonocraft", "atlas_layout.json");
        string outputPath = outputOverride
            ?? Path.Combine(repoRoot, "src", "Autonocraft", "atlas.png");
        return (layoutPath, outputPath);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "Autonocraft", "atlas_layout.json")))
            {
                return dir.FullName;
            }

            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate repository root (expected src/Autonocraft/atlas_layout.json).");
    }
}

using Autonocraft.Engine;
using Autonocraft.World;
using MgColor = Microsoft.Xna.Framework.Color;

namespace Autonocraft.AtlasBuild;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        bool checkOnly = args.Contains("--check", StringComparer.Ordinal);
        string? layoutOverride = ReadOption(args, "--layout");
        string? outputOverride = ReadOption(args, "--output");

        var (layoutPath, outputPath) = AtlasPaths.Resolve(layoutOverride, outputOverride);
        var layout = AtlasLayout.LoadFromFile(layoutPath);
        var builder = new ProceduralAtlasBuilder(layout);
        MgColor[] pixels = builder.BuildPixels();
        int width = layout.GridCols * layout.TileSize;
        int height = layout.GridRows * layout.TileSize;

        if (checkOnly)
        {
            Console.WriteLine(
                $"Atlas generation OK ({width}x{height}, {layout.GridCols}x{layout.GridRows} tiles @ {layout.TileSize}px)");

            string generatedHash = PngAtlasWriter.ComputePixelHash(pixels, width, height);
            if (File.Exists(outputPath))
            {
                string committedHash = PngAtlasWriter.ComputeFileHash(outputPath);
                if (!string.Equals(committedHash, generatedHash, StringComparison.Ordinal))
                {
                    Console.Error.WriteLine(
                        $"atlas.png is out of date (committed_pixels={committedHash[..12]}, " +
                        $"generated_pixels={generatedHash[..12]}). " +
                        "Run: dotnet run --project src/Autonocraft.AtlasBuild");
                    return 1;
                }

                Console.WriteLine($"Committed atlas matches generated output ({outputPath})");
            }
            else
            {
                Console.WriteLine("No committed atlas.png; layout and generation checks passed");
            }

            return 0;
        }

        PngAtlasWriter.Write(outputPath, pixels, width, height);
        Console.WriteLine(
            $"Wrote {outputPath} ({width}x{height}, {layout.GridCols}x{layout.GridRows} tiles @ {layout.TileSize}px)");
        return 0;
    }

    private static string? ReadOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}

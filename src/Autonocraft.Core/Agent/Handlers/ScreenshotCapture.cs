using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.Core.Agent.Handlers;

internal static class ScreenshotCapture
{
    private static string ScreenshotsRoot =>
        Path.Combine(AppContext.BaseDirectory, "screenshots");

    public static byte[] CapturePng(GraphicsDevice graphicsDevice)
    {
        int width = graphicsDevice.PresentationParameters.BackBufferWidth;
        int height = graphicsDevice.PresentationParameters.BackBufferHeight;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Graphics back buffer is not ready for capture.");
        }

        var backBuffer = new Color[width * height];
        graphicsDevice.GetBackBufferData(backBuffer);

        using var texture = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
        texture.SetData(backBuffer);

        using var stream = new MemoryStream();
        texture.SaveAsPng(stream, width, height);
        byte[] png = stream.ToArray();
        if (png.Length < 100)
        {
            throw new InvalidOperationException("Screenshot capture produced an empty image.");
        }

        return png;
    }

    public static void SavePng(GraphicsDevice graphicsDevice, string path)
    {
        SaveBytes(path, CapturePng(graphicsDevice));
    }

    public static void SaveBytes(string path, byte[] png)
    {
        string fullPath = ResolveSafePath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, png);
    }

    private static string ResolveSafePath(string path)
    {
        string root = Path.GetFullPath(ScreenshotsRoot);
        string fullRoot = root + Path.DirectorySeparatorChar;
        string requested = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(root, "screenshot.png")
            : Path.IsPathRooted(path)
                ? path
                : Path.Combine(root, path);
        string fullPath = Path.GetFullPath(requested);
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Screenshot path must stay under the screenshots directory.");
        }

        return fullPath;
    }
}

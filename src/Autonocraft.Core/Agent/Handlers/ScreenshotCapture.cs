using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.Core.Agent.Handlers;

internal static class ScreenshotCapture
{
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
        byte[] png = CapturePng(graphicsDevice);
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, png);
    }
}

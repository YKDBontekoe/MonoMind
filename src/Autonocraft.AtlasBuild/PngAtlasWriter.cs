using Microsoft.Xna.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using MgColor = Microsoft.Xna.Framework.Color;

namespace Autonocraft.AtlasBuild;

internal static class PngAtlasWriter
{
    public static void Write(string path, MgColor[] pixels, int width, int height)
    {
        if (pixels.Length != width * height)
        {
            throw new ArgumentException("Pixel buffer size does not match atlas dimensions.");
        }

        using var image = new Image<Rgba32>(width, height);
        CopyPixels(image, pixels, width, height);

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        image.SaveAsPng(path);
    }

    public static string ComputePixelHash(MgColor[] pixels, int width, int height)
    {
        var bytes = new byte[pixels.Length * 4];
        int offset = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            MgColor c = pixels[i];
            bytes[offset++] = c.R;
            bytes[offset++] = c.G;
            bytes[offset++] = c.B;
            bytes[offset++] = c.A;
        }

        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static string ComputeFileHash(string path)
    {
        using var image = Image.Load<Rgba32>(path);
        var bytes = new byte[image.Width * image.Height * 4];
        int offset = 0;
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                Rgba32 p = image[x, y];
                bytes[offset++] = p.R;
                bytes[offset++] = p.G;
                bytes[offset++] = p.B;
                bytes[offset++] = p.A;
            }
        }

        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static void CopyPixels(Image<Rgba32> image, MgColor[] pixels, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                MgColor c = pixels[y * width + x];
                image[x, y] = new Rgba32(c.R, c.G, c.B, c.A);
            }
        }
    }
}

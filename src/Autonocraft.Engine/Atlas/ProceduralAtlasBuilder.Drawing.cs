using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace Autonocraft.Engine
{

    public sealed partial class ProceduralAtlasBuilder
    {
        private static void DrawToolLine(Image image, int x0, int y0, int x1, int y1, Color color, int width)
        {
            DrawHorizontalLine(image, x0, y0, x1, y1, color, width);
        }

        private static void FillTriangle(Image image, int x0, int y0, int x1, int y1, int x2, int y2, Color color)
        {
            FillPolygon(image, new[] { (x0, y0), (x1, y1), (x2, y2) }, color);
        }

        private static void FillPolygon(Image image, (int x, int y)[] points, Color color)
        {
            int minY = points.Min(p => p.y);
            int maxY = points.Max(p => p.y);
            for (int y = minY; y <= maxY; y++)
            {
                var intersections = new List<int>();
                for (int i = 0; i < points.Length; i++)
                {
                    var a = points[i];
                    var b = points[(i + 1) % points.Length];
                    if (a.y == b.y)
                    {
                        continue;
                    }

                    if (y >= Math.Min(a.y, b.y) && y < Math.Max(a.y, b.y))
                    {
                        int x = a.x + (y - a.y) * (b.x - a.x) / (b.y - a.y);
                        intersections.Add(x);
                    }
                }

                intersections.Sort();
                for (int i = 0; i + 1 < intersections.Count; i += 2)
                {
                    DrawHorizontalLine(image, intersections[i], y, intersections[i + 1], y, color, 1);
                }
            }
        }

        private static void FillEllipse(Image image, int x0, int y0, int x1, int y1, Color color)
        {
            int cx = (x0 + x1) / 2;
            int cy = (y0 + y1) / 2;
            int rx = Math.Max(1, (x1 - x0) / 2);
            int ry = Math.Max(1, (y1 - y0) / 2);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x - cx) / (float)rx;
                    float dy = (y - cy) / (float)ry;
                    if (dx * dx + dy * dy <= 1f)
                    {
                        SetPixel(image, x, y, color);
                    }
                }
            }
        }

        private int ApplyPaletteSeed(string name)
        {
            if (_paletteSeed == 0)
            {
                return 0;
            }

            return NoiseValue("palette", _paletteSeed, name.GetHashCode(), 0) % 17 - 8;
        }

        private static string SeedName(string name, int seedShift)
        {
            return seedShift == 0 ? name : $"{name}#{seedShift}";
        }

        private Color ShiftColor(Color color, int amount)
        {
            return new Color(
                Clamp(color.R + amount),
                Clamp(color.G + amount / 2),
                Clamp(color.B + amount / 3));
        }

        private static byte Clamp(int value) => (byte)Math.Clamp(value, 0, 255);

        private static Color Shade(Color color, int amount)
        {
            return new Color(Clamp(color.R + amount), Clamp(color.G + amount), Clamp(color.B + amount));
        }

        private static int NoiseValue(string name, int x, int y, int salt = 0)
        {
            int seed = 0;
            for (int i = 0; i < name.Length; i++)
            {
                seed += (i + 1) * name[i];
            }

            seed += salt * 131;
            unchecked
            {
                uint value = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                value = (value ^ (value >> 13)) * 1274126177u;
                return (int)((value ^ (value >> 16)) & 255u);
            }
        }

        private static void SetPixel(Image image, int x, int y, Color color)
        {
            if (x >= 0 && x < image.Size && y >= 0 && y < image.Size)
            {
                image.Pixels[y * image.Size + x] = color;
            }
        }

        private static void FillRect(Image image, int x, int y, int width, int height, Color color)
        {
            for (int py = y; py < y + height && py < image.Size; py++)
            {
                for (int px = x; px < x + width && px < image.Size; px++)
                {
                    if (px >= 0 && py >= 0)
                    {
                        image.Pixels[py * image.Size + px] = color;
                    }
                }
            }
        }

        private static void DrawRectOutline(Image image, int x0, int y0, int x1, int y1, Color color, int width)
        {
            for (int t = 0; t < width; t++)
            {
                DrawHorizontalLine(image, x0, y0 + t, x1, y0 + t, color, 1);
                DrawHorizontalLine(image, x0, y1 - t, x1, y1 - t, color, 1);
                DrawVerticalLine(image, x0 + t, color, y1 - y0);
                DrawVerticalLine(image, x1 - t, color, y1 - y0);
            }
        }

        private static void DrawVerticalLine(Image image, int x, Color color, int height)
        {
            for (int y = 0; y < height && y < image.Size; y++)
            {
                SetPixel(image, x, y, color);
            }
        }

        private static void DrawHorizontalLine(Image image, int x0, int y0, int x1, int y1, Color color, int width)
        {
            int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                int x = x0 + (x1 - x0) * i / Math.Max(1, steps);
                int y = y0 + (y1 - y0) * i / Math.Max(1, steps);
                FillRect(image, x, y, width, width, color);
            }
        }

        private sealed class Image
        {
            public Image(Color[] pixels)
            {
                Pixels = pixels;
                Size = (int)Math.Sqrt(pixels.Length);
            }

            public Color[] Pixels { get; }
            public int Size { get; }
            public int Height => Size;
            public int Width => Size;

            public Image Clone()
            {
                var copy = new Color[Pixels.Length];
                Array.Copy(Pixels, copy, Pixels.Length);
                return new Image(copy);
            }
        }
    }
}

using Microsoft.Xna.Framework;
using System;
using System.Linq;

namespace Autonocraft.Engine
{
    /// <summary>
    /// High-level procedural tile generators shared by <see cref="ProceduralAtlasBuilder"/>.
    /// Each method returns a flat RGBA pixel buffer (tileSize × tileSize).
    /// </summary>
    internal static partial class ProceduralTextureSynth
    {
        private const int CellOrganic = 3;
        private const int CellEarth = 3;
        private const int CellWood = 2;



        private static void FillTriangle(TileImage image, int x0, int y0, int x1, int y1, int x2, int y2, Color color)
        {
            FillPolygon(image, new[] { (x0, y0), (x1, y1), (x2, y2) }, color);
        }

        private static void FillPolygon(TileImage image, (int x, int y)[] points, Color color)
        {
            int minY = points.Min(p => p.y);
            int maxY = points.Max(p => p.y);
            for (int y = minY; y <= maxY; y++)
            {
                var intersections = new System.Collections.Generic.List<int>();
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
                    DrawLine(image, intersections[i], y, intersections[i + 1], y, color, 1);
                }
            }
        }

        private static Color[] PixelCluster(int tileSize, string name, Color baseColor, Color[] palette, int cellSize, int variation)
        {
            var pixels = new Color[tileSize * tileSize];
            var colors = palette.Length > 0 ? palette : new[] { baseColor };

            for (int cy = 0; cy < tileSize; cy += cellSize)
            {
                for (int cx = 0; cx < tileSize; cx += cellSize)
                {
                    int pick = Noise(name, cx / cellSize, cy / cellSize) % colors.Length;
                    Color cell = colors[pick];
                    int accent = Noise(name, cx / cellSize, cy / cellSize, 11) % 6;
                    if (accent == 0) cell = Lighten(cell, variation);
                    else if (accent == 1) cell = Darken(cell, variation / 2);

                    int maxX = Math.Min(tileSize, cx + cellSize);
                    int maxY = Math.Min(tileSize, cy + cellSize);
                    for (int y = cy; y < maxY; y++)
                    {
                        for (int x = cx; x < maxX; x++)
                        {
                            int fine = Noise(name, x, y, 19) % 9;
                            pixels[y * tileSize + x] = fine switch
                            {
                                0 => Lighten(cell, 10),
                                1 => Darken(cell, 8),
                                _ => cell
                            };
                        }
                    }
                }
            }

            return pixels;
        }

        private static Color[] Voronoi(int tileSize, string name, Color[] cellColors, Color grout, int seedCount, float groutWidth)
        {
            var seeds = new (float x, float y, Color color)[seedCount];
            for (int i = 0; i < seedCount; i++)
            {
                seeds[i] = (
                    Noise(name, i, 1, 3) % tileSize,
                    Noise(name, i, 2, 5) % tileSize,
                    cellColors[Noise(name, i, 3, 7) % cellColors.Length]);
            }

            var pixels = new Color[tileSize * tileSize];
            for (int y = 0; y < tileSize; y++)
            {
                for (int x = 0; x < tileSize; x++)
                {
                    float d1 = float.MaxValue;
                    float d2 = float.MaxValue;
                    Color nearest = cellColors[0];
                    int nearestIdx = 0;
                    for (int i = 0; i < seedCount; i++)
                    {
                        float dx = x - seeds[i].x;
                        float dy = y - seeds[i].y;
                        float d = dx * dx + dy * dy;
                        if (d < d1)
                        {
                            d2 = d1;
                            d1 = d;
                            nearest = seeds[i].color;
                            nearestIdx = i;
                        }
                        else if (d < d2)
                        {
                            d2 = d;
                        }
                    }

                    float edge = MathF.Sqrt(d2) - MathF.Sqrt(d1);
                    if (edge < groutWidth)
                    {
                        int g = Noise(name, x, y, 23) % 5;
                        pixels[y * tileSize + x] = g switch
                        {
                            0 => Lighten(grout, 6),
                            1 => Darken(grout, 6),
                            _ => grout
                        };
                    }
                    else
                    {
                        float shade = MathF.Min(1f, MathF.Sqrt(d1) / (tileSize * 0.42f));
                        Color c = Lerp(Lighten(nearest, 12), Darken(nearest, 16), shade);

                        float angleX = (x - seeds[nearestIdx].x);
                        float angleY = (y - seeds[nearestIdx].y);
                        if (angleX < -1f && angleY < -1f)
                        {
                            c = Lighten(c, 16);
                        }
                        else if (angleX > 1f && angleY > 1f)
                        {
                            c = Darken(c, 18);
                        }
                        pixels[y * tileSize + x] = c;
                    }
                }
            }

            return pixels;
        }

        private static void PaintBlobs(TileImage image, string name, Color[] colors, int count, int minR, int maxR)
        {
            for (int i = 0; i < count; i++)
            {
                int cx = Noise(name, i, 3, 7) % image.Size;
                int cy = Noise(name, i, 5, 9) % image.Size;
                int rx = minR + Noise(name, i, 11, 13) % (maxR - minR + 1);
                int ry = minR + Noise(name, i, 15, 17) % (maxR - minR + 1);
                Color color = colors[Noise(name, i, 19, 21) % colors.Length];
                FillEllipse(image, cx - rx, cy - ry, cx + rx, cy + ry, color);
                FillEllipse(image, cx - rx / 2, cy - ry / 2, cx + rx / 3, cy + ry / 3, Lighten(color, 18));
            }
        }

        private static void ApplyCellRims(TileImage image, int cellSize, int darkAmount, int lightAmount)
        {
            for (int cy = 0; cy < image.Size; cy += cellSize)
            {
                for (int cx = 0; cx < image.Size; cx += cellSize)
                {
                    int maxX = Math.Min(image.Size - 1, cx + cellSize - 1);
                    int maxY = Math.Min(image.Size - 1, cy + cellSize - 1);
                    for (int x = cx; x <= maxX; x++)
                    {
                        var p = image.Pixels[cy * image.Size + x];
                        image.Pixels[cy * image.Size + x] = Darken(p, darkAmount);
                        image.Pixels[maxY * image.Size + x] = Lighten(image.Pixels[maxY * image.Size + x], lightAmount / 2);
                    }

                    for (int y = cy; y <= maxY; y++)
                    {
                        image.Pixels[y * image.Size + cx] = Darken(image.Pixels[y * image.Size + cx], darkAmount);
                        image.Pixels[y * image.Size + maxX] = Lighten(image.Pixels[y * image.Size + maxX], lightAmount);
                    }
                }
            }
        }

        private static Color[] FillSolid(int tileSize, Color color)
        {
            var pixels = new Color[tileSize * tileSize];
            Array.Fill(pixels, color);
            return pixels;
        }

        private static Color[] ExpandPalette(Color a, Color b, Color c) => new[] { Darken(a, 12), a, Lighten(a, 8), b, c };

        private static void ScatterRects(TileImage image, string name, Color[] colors, int count, int size)
        {
            for (int i = 0; i < count; i++)
            {
                int x = Noise(name, i, 17, 3) % image.Size;
                int y = Noise(name, i, 31, 5) % image.Size;
                Color color = colors[Noise(name, i, 47, 9) % colors.Length];
                FillRect(image, x, y, size, size, color);
            }
        }

        private static void DrawBlade(TileImage image, int x0, int y0, int x1, int y1, Color color, int width)
        {
            DrawLine(image, x0, y0, x1, y1, color, width);
        }

        private static void DrawVerticalBand(TileImage image, int x, Color color, int width)
        {
            for (int y = 0; y < image.Size; y++)
            {
                FillRect(image, x, y, width, 1, color);
            }
        }

        private static void DrawLine(TileImage image, int x0, int y0, int x1, int y1, Color color, int width)
        {
            int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                int x = x0 + (x1 - x0) * i / Math.Max(1, steps);
                int y = y0 + (y1 - y0) * i / Math.Max(1, steps);
                FillRect(image, x, y, width, width, color);
            }
        }

        private static void DrawRectOutline(TileImage image, int x0, int y0, int x1, int y1, Color color, int thickness)
        {
            for (int t = 0; t < thickness; t++)
            {
                DrawLine(image, x0, y0 + t, x1, y0 + t, color, 1);
                DrawLine(image, x0, y1 - t, x1, y1 - t, color, 1);
                DrawLine(image, x0 + t, y0, x0 + t, y1, color, 1);
                DrawLine(image, x1 - t, y0, x1 - t, y1, color, 1);
            }
        }

        private static void FillRect(TileImage image, int x, int y, int w, int h, Color color)
        {
            for (int py = y; py < y + h && py < image.Size; py++)
            {
                for (int px = x; px < x + w && px < image.Size; px++)
                {
                    if (px >= 0 && py >= 0)
                    {
                        image.Pixels[py * image.Size + px] = color;
                    }
                }
            }
        }

        private static void FillEllipse(TileImage image, int x0, int y0, int x1, int y1, Color color)
        {
            int cx = (x0 + x1) / 2;
            int cy = (y0 + y1) / 2;
            int rx = Math.Max(1, (x1 - x0) / 2);
            int ry = Math.Max(1, (y1 - y0) / 2);
            for (int y = Math.Max(0, y0); y <= Math.Min(image.Size - 1, y1); y++)
            {
                for (int x = Math.Max(0, x0); x <= Math.Min(image.Size - 1, x1); x++)
                {
                    float dx = (x - cx) / (float)rx;
                    float dy = (y - cy) / (float)ry;
                    if (dx * dx + dy * dy <= 1f)
                    {
                        image.Pixels[y * image.Size + x] = color;
                    }
                }
            }
        }

        private static void SetPixel(TileImage image, int x, int y, Color color)
        {
            if (x >= 0 && x < image.Size && y >= 0 && y < image.Size)
            {
                image.Pixels[y * image.Size + x] = color;
            }
        }

        private static Color Lighten(Color c, int amount) => new(
            (byte)Math.Clamp(c.R + amount, 0, 255),
            (byte)Math.Clamp(c.G + amount, 0, 255),
            (byte)Math.Clamp(c.B + amount, 0, 255),
            c.A);

        private static Color Darken(Color c, int amount) => Lighten(c, -amount);

        private static Color Lerp(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return new Color(
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t),
                (byte)(a.A + (b.A - a.A) * t));
        }

        private static int Noise(string name, int x, int y, int salt = 0)
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

        private sealed class TileImage
        {
            public TileImage(Color[] pixels, int size)
            {
                Pixels = pixels;
                Size = size;
            }

            public Color[] Pixels { get; }
            public int Size { get; }
        }

        private static void SetPixelWrapped(TileImage image, int x, int y, Color color)
        {
            int wx = (x % image.Size + image.Size) % image.Size;
            int wy = (y % image.Size + image.Size) % image.Size;
            image.Pixels[wy * image.Size + wx] = color;
        }

        private static void FillRectWrapped(TileImage image, int x, int y, int w, int h, Color color)
        {
            for (int py = y; py < y + h; py++)
            {
                for (int px = x; px < x + w; px++)
                {
                    SetPixelWrapped(image, px, py, color);
                }
            }
        }
    }
}

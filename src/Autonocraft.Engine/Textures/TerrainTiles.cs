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

        public static Color[] Surface(int tileSize, string name, Color[] palette, int scatterCount = 18)
        {
            var image = new TileImage(PixelCluster(tileSize, name, palette[Math.Min(1, palette.Length - 1)], palette, CellEarth, 18), tileSize);
            ScatterRects(image, name, palette, scatterCount, 2);
            ApplyCellRims(image, CellEarth, -12, 6);
            return image.Pixels;
        }

        public static Color[] GrassTop(int tileSize, string name, Color[] palette)
        {
            var grout = new Color(24, 68, 22);
            var image = new TileImage(Voronoi(tileSize, name, palette, grout, 18, 2.4f), tileSize);

            // Scatter lush grass blade clumps
            for (int i = 0; i < 35; i++)
            {
                int cx = Noise(name, i, 3, 7) % tileSize;
                int cy = Noise(name, i, 5, 9) % tileSize;
                Color bladeColor = palette[Noise(name, i, 11, 13) % palette.Length];
                SetPixelWrapped(image, cx, cy, Lighten(bladeColor, 12));
                SetPixelWrapped(image, cx - 1, cy - 1, bladeColor);
                SetPixelWrapped(image, cx + 1, cy - 1, bladeColor);
                SetPixelWrapped(image, cx, cy + 1, Darken(bladeColor, 8));
            }

            // Scatter small colorful flowers
            Color[] flowerColors = { new Color(240, 90, 120), new Color(240, 210, 60), new Color(90, 180, 240) };
            for (int i = 0; i < 8; i++)
            {
                int cx = Noise(name, i, 23, 31) % tileSize;
                int cy = Noise(name, i, 27, 37) % tileSize;
                Color petal = flowerColors[Noise(name, i, 41, 43) % flowerColors.Length];
                Color center = new Color(250, 240, 160);

                SetPixelWrapped(image, cx, cy, center);
                SetPixelWrapped(image, cx - 1, cy, petal);
                SetPixelWrapped(image, cx + 1, cy, petal);
                SetPixelWrapped(image, cx, cy - 1, petal);
                SetPixelWrapped(image, cx, cy + 1, petal);
            }

            return image.Pixels;
        }

        public static Color[] GrassFringe(int tileSize, string name, Color[] palette)
        {
            var image = new TileImage(PixelCluster(tileSize, name, palette[0], palette, CellOrganic, 18), tileSize);
            int fringeRows = Math.Max(8, tileSize * 55 / 100);

            for (int i = 0; i < 64; i++)
            {
                int x = Noise(name, i, 3, 5) % tileSize;
                int len = 6 + Noise(name, i, 7, 9) % fringeRows;
                Color blade = palette[Noise(name, i, 11, 13) % palette.Length];
                for (int d = 0; d < len; d++)
                {
                    int y = d;
                    int sway = (Noise(name, i, 17, 19) % 3) - 1;
                    int wx = x + sway;
                    if (wx >= 0 && wx < tileSize && y >= 0 && y < tileSize)
                    {
                        image.Pixels[y * tileSize + wx] = d == 0 ? Lighten(blade, 8) : blade;
                    }
                }
            }

            return image.Pixels;
        }

        public static Color[] SnowFringe(int tileSize, string name, Color[] palette)
        {
            var image = new TileImage(PixelCluster(tileSize, name, palette[0], palette, CellOrganic, 18), tileSize);
            int fringeRows = Math.Max(8, tileSize * 55 / 100);

            for (int i = 0; i < 64; i++)
            {
                int x = Noise(name, i, 3, 5) % tileSize;
                int len = 6 + Noise(name, i, 7, 9) % fringeRows;
                Color blade = palette[Noise(name, i, 11, 13) % palette.Length];
                for (int d = 0; d < len; d++)
                {
                    int y = d;
                    int sway = (Noise(name, i, 17, 19) % 3) - 1;
                    int wx = x + sway;
                    if (wx >= 0 && wx < tileSize && y >= 0 && y < tileSize)
                    {
                        image.Pixels[y * tileSize + wx] = d == 0 ? Lighten(blade, 8) : blade;
                    }
                }
            }

            return image.Pixels;
        }

        public static Color[] Dirt(int tileSize, string name, Color[] palette)
        {
            Color grout = Darken(palette[0], 20);
            var image = new TileImage(Voronoi(tileSize, name, palette, grout, 18, 2.4f), tileSize);

            // Draw organic dirt clods
            for (int i = 0; i < 12; i++)
            {
                int cx = Noise(name, i, 31, 33) % tileSize;
                int cy = Noise(name, i, 35, 37) % tileSize;
                int rx = 4 + Noise(name, i, 39, 41) % 5;
                int ry = 3 + Noise(name, i, 43, 47) % 4;
                Color clodColor = Darken(palette[0], 14 + i % 6);
                FillEllipse(image, cx - rx, cy - ry, cx + rx, cy + ry, clodColor);
                // Highlight on top of clod
                for (int hx = cx - rx + 1; hx < cx + rx; hx++)
                {
                    SetPixel(image, hx, cy - ry + 1, Lighten(clodColor, 8));
                }
            }

            // Scatter some small gray/beige pebbles
            Color[] pebbleColors = { new Color(140, 135, 130), new Color(165, 155, 145) };
            for (int i = 0; i < 15; i++)
            {
                int px = Noise(name, i, 53, 59) % (tileSize - 4);
                int py = Noise(name, i, 61, 67) % (tileSize - 4);
                Color pebble = pebbleColors[i % 2];
                // 2x2 pebble with a tiny shadow and highlight
                SetPixel(image, px, py, pebble);
                SetPixel(image, px + 1, py, Lighten(pebble, 14));
                SetPixel(image, px, py + 1, Darken(pebble, 10));
                SetPixel(image, px + 1, py + 1, Darken(pebble, 16));
            }

            return image.Pixels;
        }

        public static Color[] MossStone(int tileSize, string name, Color[] stonePalette, Color grout, Color[] mossPalette)
        {
            var image = new TileImage(Cobble(tileSize, name, stonePalette, grout), tileSize);
            PaintBlobs(image, name + "_moss", mossPalette, 18, 4, 10);
            return image.Pixels;
        }

        public static Color[] GlassTile(int tileSize, string name)
        {
            var baseColor = new Color(180, 220, 240, 32); // semi-transparent
            var pixels = new Color[tileSize * tileSize];
            Array.Fill(pixels, baseColor);
            var image = new TileImage(pixels, tileSize);

            Color frameLight = new Color(240, 250, 255, 180);
            Color frameDark = new Color(110, 150, 180, 140);
            Color reflection = new Color(255, 255, 255, 150);

            // Draw frame outline
            DrawRectOutline(image, 0, 0, tileSize - 1, tileSize - 1, frameDark, 2);
            DrawLine(image, 0, 0, tileSize - 1, 0, frameLight, 2);
            DrawLine(image, 0, 0, 0, tileSize - 1, frameLight, 2);

            // Draw diagonal glints/glare highlights in the center
            int cx = tileSize / 2;
            int cy = tileSize / 2;
            DrawLine(image, cx - 16, cy - 8, cx - 4, cy + 4, reflection, 2);
            DrawLine(image, cx + 4, cy - 8, cx + 16, cy + 4, reflection, 2);

            return image.Pixels;
        }

        public static Color[] IceTile(int tileSize, string name)
        {
            var baseColor = new Color(168, 212, 238);
            var palette = new[] { baseColor, Lighten(baseColor, 12), Darken(baseColor, 10) };
            var image = new TileImage(PixelCluster(tileSize, name, baseColor, palette, CellOrganic, 8), tileSize);

            // Draw frosty highlights and fractures
            Color iceCrack = new Color(220, 240, 255, 220);
            Color iceShadow = new Color(130, 180, 215, 180);

            // Jagged crack 1
            DrawLine(image, 10, tileSize / 4, tileSize / 2, tileSize / 4 + 8, iceCrack, 2);
            DrawLine(image, tileSize / 2, tileSize / 4 + 8, tileSize - 15, tileSize / 4 - 2, iceCrack, 1);
            DrawLine(image, tileSize / 2, tileSize / 4 + 8, tileSize / 2 - 4, tileSize * 3 / 4, iceShadow, 1);

            // Jagged crack 2
            DrawLine(image, tileSize * 3 / 4, 15, tileSize * 2 / 3, tileSize * 2 / 3, iceCrack, 1);
            DrawLine(image, tileSize * 2 / 3, tileSize * 2 / 3, tileSize / 3, tileSize - 10, iceShadow, 2);

            ApplyCellRims(image, CellOrganic, -6, 10);
            return image.Pixels;
        }

        public static Color[] HayBale(int tileSize, string name, Color[] palette, Color seam, Color border)
        {
            var image = new TileImage(PixelCluster(tileSize, name, palette[1], palette, CellEarth, 14), tileSize);

            // Draw straw grain lines
            for (int y = 4; y < tileSize; y += 6)
            {
                int len = tileSize / 2 + Noise(name, y, 11) % (tileSize / 2);
                DrawLine(image, 0, y, len, y, Lighten(palette[0], 12), 1);
                DrawLine(image, len + 2, y, tileSize - 1, y, Darken(palette[2], 12), 1);
            }

            // Draw ropes wrapped vertically around the bale
            Color ropeColor = new Color(158, 88, 48); // red/brown twine
            Color ropeHi = new Color(208, 128, 68);
            Color ropeShadow = new Color(108, 58, 28);

            int rope1 = tileSize / 4;
            int rope2 = tileSize * 3 / 4;

            // Draw rope 1
            FillRect(image, rope1 - 3, 0, 6, tileSize, ropeShadow);
            FillRect(image, rope1 - 2, 0, 4, tileSize, ropeColor);
            FillRect(image, rope1 - 1, 0, 2, tileSize, ropeHi);

            // Draw rope 2
            FillRect(image, rope2 - 3, 0, 6, tileSize, ropeShadow);
            FillRect(image, rope2 - 2, 0, 4, tileSize, ropeColor);
            FillRect(image, rope2 - 1, 0, 2, tileSize, ropeHi);

            ApplyCellRims(image, CellEarth, -10, 5);
            return image.Pixels;
        }

        public static Color[] Stone(int tileSize, string name, Color[] palette, Color grout)
        {
            var image = new TileImage(Voronoi(tileSize, name, palette, grout, 18, 2.4f), tileSize);

            for (int i = 0; i < 8; i++)
            {
                int x0 = Noise(name, i, 3, 17) % (tileSize - 30) + 15;
                int y0 = Noise(name, i, 7, 19) % (tileSize - 30) + 15;
                int len = 12 + Noise(name, i, 11, 23) % 16;
                int dx = Noise(name, i, 13, 29) % 2 == 0 ? len : -len;
                int dy = 4 + Noise(name, i, 17, 31) % 8;

                Color crackColor = Darken(grout, 36);
                Color highlightColor = Lighten(palette[2], 24);

                DrawLine(image, x0, y0, x0 + dx, y0 + dy, crackColor, 1);
                DrawLine(image, x0, y0 - 1, x0 + dx, y0 + dy - 1, highlightColor, 1);
            }

            return image.Pixels;
        }

        public static Color[] WoodLog(int tileSize, string name, Color baseColor, Color ringDark, Color ringLight, int spacing)
        {
            var image = new TileImage(PixelCluster(tileSize, name, baseColor, ExpandPalette(baseColor, ringDark, ringLight), CellWood, 12), tileSize);

            for (int x = 2; x < tileSize; x += spacing)
            {
                int wobble = (Noise(name, x, 0, 31) % 5) - 2;
                int rx = x + wobble;
                DrawVerticalBand(image, rx, ringDark, 4);
                DrawVerticalBand(image, rx + 1, ringLight, 1);
            }

            for (int y = 10; y < tileSize; y += 22)
            {
                int offset = Noise(name, y, 0, 37) % 8;
                DrawLine(image, offset, y, tileSize - 1, y, Darken(baseColor, 22), 2);
            }

            for (int i = 0; i < 4; i++)
            {
                int kx = Noise(name, i, 41, 43) % (tileSize - 20) + 10;
                int ky = Noise(name, i, 47, 49) % (tileSize - 20) + 10;
                int kr = 6 + Noise(name, i, 51, 53) % 6;
                FillEllipse(image, kx - kr, ky - kr, kx + kr, ky + kr, Darken(ringDark, 8));
                FillEllipse(image, kx - kr / 2, ky - kr / 2, kx + kr / 2, ky + kr / 2, Lighten(ringLight, 6));
            }

            return image.Pixels;
        }

        public static Color[] WoodPlank(int tileSize, string name, Color baseColor, Color seam, Color grain)
        {
            var image = new TileImage(PixelCluster(tileSize, name, baseColor, ExpandPalette(baseColor, seam, grain), CellWood, 10), tileSize);
            int plankH = tileSize / 4;

            for (int row = 0; row < 4; row++)
            {
                int y0 = row * plankH;
                int y1 = Math.Min(tileSize - 1, y0 + plankH - 1);
                DrawLine(image, 0, y0, tileSize - 1, y0, Lighten(baseColor, 14), 1);
                DrawLine(image, 0, y1, tileSize - 1, y1, Darken(seam, 10), 2);

                for (int g = 0; g < 5; g++)
                {
                    int gy = y0 + 4 + g * (plankH / 5);
                    int gx0 = Noise(name, row, g, 11) % 10;
                    DrawLine(image, gx0, gy, tileSize - 1 - Noise(name, row, g, 13) % 8, gy + 1, grain, 1);
                }

                if (Noise(name, row, 0, 17) % 3 == 0)
                {
                    int kx = Noise(name, row, 1, 19) % (tileSize - 16) + 8;
                    int ky = y0 + plankH / 2;
                    FillEllipse(image, kx - 4, ky - 3, kx + 4, ky + 3, Darken(grain, 16));
                }
            }

            return image.Pixels;
        }

        public static Color[] Leaves(int tileSize, string name, Color[] palette)
        {
            Color grout = Darken(palette[0], 24);
            var image = new TileImage(Voronoi(tileSize, name, palette, grout, 18, 2.4f), tileSize);
            ScatterRects(image, name, palette, 90, 5);
            ScatterRects(image, name + "_hi", palette.Select(c => Lighten(c, 32)).ToArray(), 38, 3);
            ScatterRects(image, name + "_dk", palette.Select(c => Darken(c, 28)).ToArray(), 22, 4);
            return image.Pixels;
        }

        public static Color[] Sand(int tileSize, string name, Color[] palette)
        {
            Color grout = Darken(palette[0], 14);
            var image = new TileImage(Voronoi(tileSize, name, palette, grout, 22, 1.8f), tileSize);
            ScatterRects(image, name, palette, 30, 2);
            for (int i = 0; i < 18; i++)
            {
                int x = Noise(name, i, 3, 5) % tileSize;
                int y = Noise(name, i, 7, 9) % tileSize;
                SetPixel(image, x, y, Lighten(palette[2], 20));
            }

            return image.Pixels;
        }

        public static Color[] Gravel(int tileSize, string name, Color[] palette, Color grout)
        {
            var image = new TileImage(Voronoi(tileSize, name, palette, grout, 28, 1.8f), tileSize);
            ScatterRects(image, name + "_chip", palette, 40, 2);
            return image.Pixels;
        }

        public static Color[] Ore(int tileSize, string name, Color stone, Color ore, Color oreHi)
        {
            var stonePalette = ExpandPalette(stone, Darken(stone, 18), Lighten(stone, 14));
            var image = new TileImage(Stone(tileSize, name, stonePalette, Darken(stone, 24)), tileSize);

            for (int cluster = 0; cluster < 6; cluster++)
            {
                int cx = Noise(name, cluster, 3, 7) % (tileSize - 24) + 12;
                int cy = Noise(name, cluster, 5, 9) % (tileSize - 24) + 12;
                int pieces = 4 + Noise(name, cluster, 11, 13) % 4;

                for (int p = 0; p < pieces; p++)
                {
                    int x = cx + (Noise(name, cluster, p, 17) % 14) - 7;
                    int y = cy + (Noise(name, cluster, p, 19) % 14) - 7;
                    int r = 3 + Noise(name, cluster, p, 21) % 4;

                    Color darkOutline = Darken(ore, 30);
                    FillEllipse(image, x - r - 1, y - r - 1, x + r + 1, y + r + 1, darkOutline);
                    FillEllipse(image, x - r, y - r, x + r, y + r, ore);
                    FillEllipse(image, x - r / 2, y - r / 2, x + r / 4, y + r / 4, oreHi);
                }
            }

            return image.Pixels;
        }

        public static Color[] Water(int tileSize, string name, int frame = 0)
        {
            var deep = new Color(26, 68, 142);
            var mid = new Color(40, 104, 186);
            var shallow = new Color(56, 136, 212);
            var highlight = new Color(148, 208, 248);
            var pixels = new Color[tileSize * tileSize];

            for (int y = 0; y < tileSize; y++)
            {
                for (int x = 0; x < tileSize; x++)
                {
                    int n0 = Noise(name, x / 3, y / 3, frame + 1);
                    int n1 = Noise(name, x / 5 + frame, y / 5, frame + 7);
                    int n2 = Noise(name, x / 2, y / 2, frame + 13);
                    int n3 = Noise(name, x / 7, y / 4, frame + 3);

                    float blend = ((n0 & 255) / 255f * 0.55f) + ((n3 & 255) / 255f * 0.45f);
                    Color basePx = Lerp(Lerp(deep, mid, blend), shallow, blend * blend);
                    basePx = Lerp(basePx, Lighten(basePx, 14), (n2 & 15) / 15f * 0.28f);
                    basePx = Lerp(basePx, Darken(basePx, 8), ((n1 & 7) / 7f) * 0.22f);

                    float caustic = MathF.Sin((x * 0.45f + frame * 0.6f) + n0 * 0.05f)
                        * MathF.Sin((y * 0.4f - frame * 0.5f) + n1 * 0.04f);
                    if (caustic > 0.68f)
                    {
                        basePx = Lerp(basePx, highlight, (caustic - 0.68f) * 1.5f);
                    }

                    pixels[y * tileSize + x] = basePx;
                }
            }

            return pixels;
        }

        public static Color[] WaterSide(int tileSize, string name)
        {
            var deep = new Color(16, 48, 104);
            var mid = new Color(24, 72, 138);
            var shallow = new Color(34, 88, 154);
            var pixels = new Color[tileSize * tileSize];

            for (int y = 0; y < tileSize; y++)
            {
                for (int x = 0; x < tileSize; x++)
                {
                    int n0 = Noise(name, x / 4, y / 5, 11);
                    int n1 = Noise(name, x / 6, y / 3, 19);
                    float blend = ((n0 & 255) / 255f * 0.6f) + ((n1 & 255) / 255f * 0.4f);
                    Color basePx = Lerp(Lerp(deep, mid, blend), shallow, blend * 0.35f);
                    pixels[y * tileSize + x] = (n0 & 7) switch
                    {
                        0 => Lighten(basePx, 5),
                        1 => Darken(basePx, 6),
                        _ => basePx
                    };
                }
            }

            return pixels;
        }

        public static Color[] Cobble(int tileSize, string name, Color[] stones, Color mortar)
        {
            return Voronoi(tileSize, name, stones, mortar, 20, 3.2f);
        }

        public static Color[] Brick(int tileSize, string name, Color brick, Color mortar, Color brickHi, Color brickLo)
        {
            var image = new TileImage(FillSolid(tileSize, mortar), tileSize);
            int rowH = tileSize / 5;

            for (int row = 0; row < 5; row++)
            {
                int offset = (row % 2) * (tileSize / 8);
                int y = row * rowH;
                for (int col = 0; col < 4; col++)
                {
                    int x = offset + col * (tileSize / 4);
                    Color baseColor = (row + col) % 2 == 0 ? brickHi : brickLo;
                    FillRectWrapped(image, x + 2, y + 2, tileSize / 4 - 3, rowH - 3, baseColor);
                    for (int bx = x + 2; bx < x + tileSize / 4 - 1; bx++)
                    {
                        SetPixelWrapped(image, bx, y + 2, Lighten(baseColor, 18));
                        SetPixelWrapped(image, bx, y + rowH - 2, Darken(baseColor, 18));
                    }
                    for (int by = y + 2; by < y + rowH - 1; by++)
                    {
                        SetPixelWrapped(image, x + 2, by, Lighten(baseColor, 14));
                        SetPixelWrapped(image, x + tileSize / 4 - 2, by, Darken(baseColor, 22));
                    }
                }
            }

            return image.Pixels;
        }

        public static Color[] Snow(int tileSize, string name)
        {
            var palette = new[]
            {
                new Color(228, 236, 244),
                new Color(240, 246, 252),
                new Color(252, 254, 255)
            };
            var image = new TileImage(PixelCluster(tileSize, name, palette[1], palette, CellEarth, 8), tileSize);
            DrawLine(image, 0, tileSize / 3, tileSize - 1, tileSize / 3 - 6, new Color(208, 220, 232), 2);
            DrawLine(image, 0, tileSize * 2 / 3, tileSize - 1, tileSize * 2 / 3 + 4, new Color(255, 255, 255), 2);
            ApplyCellRims(image, CellEarth, -6, 8);
            return image.Pixels;
        }

        public static Color[] MetalBlock(int tileSize, string name, Color baseColor, Color edge)
        {
            var image = new TileImage(PixelCluster(tileSize, name, baseColor, ExpandPalette(baseColor, edge, Lighten(baseColor, 18)), CellEarth, 10), tileSize);
            int inset = tileSize / 5;
            DrawRectOutline(image, inset, inset, tileSize - inset, tileSize - inset, edge, 3);
            DrawLine(image, inset, inset, tileSize - inset, inset, Lighten(baseColor, 22), 2);
            DrawLine(image, inset, inset, inset, tileSize - inset, Lighten(baseColor, 12), 2);
            ApplyCellRims(image, CellEarth, -8, 6);
            return image.Pixels;
        }

        public static Color[] WoodLogTop(int tileSize, string name, Color barkBase, Color barkDark, Color woodBase, Color woodRing)
        {
            var pixels = new Color[tileSize * tileSize];
            float cx = tileSize / 2f;
            float cy = tileSize / 2f;
            for (int y = 0; y < tileSize; y++)
            {
                for (int x = 0; x < tileSize; x++)
                {
                    float dx = x - cx + 0.5f;
                    float dy = y - cy + 0.5f;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    float theta = MathF.Atan2(dy, dx);

                    int angleIdx = (int)MathF.Round((theta + MathF.PI) / (2f * MathF.PI) * 32f) % 32;
                    int wobble = (Noise(name, angleIdx, 0, 45) % 6) - 3;

                    float adjustedDist = dist + wobble;
                    Color c;

                    if (adjustedDist > (tileSize / 2f) - 5f)
                    {
                        int barkNoise = Noise(name, x, y, 77) % 3;
                        if (barkNoise == 0)
                        {
                            c = barkDark;
                        }
                        else if (barkNoise == 1)
                        {
                            c = barkBase;
                        }
                        else
                        {
                            c = Darken(barkBase, 12);
                        }
                    }
                    else
                    {
                        float woodNoise = (Noise(name, x, y, 99) % 3 - 1) * 0.5f;
                        float adjustedDistWood = adjustedDist + woodNoise;

                        float t;
                        if (adjustedDistWood < 5f)
                        {
                            t = 0.8f;
                        }
                        else
                        {
                            float ringPhase = adjustedDistWood % 10f;
                            if (ringPhase < 2.5f)
                            {
                                t = 0.7f + 0.3f * (1f - ringPhase / 2.5f);
                            }
                            else
                            {
                                t = 0.2f * (ringPhase - 2.5f) / 7.5f;
                            }
                        }

                        c = Lerp(woodBase, woodRing, t);
                        int grainNoise = (Noise(name, x, y, 123) % 15) - 7;
                        c = Lighten(c, grainNoise);
                    }

                    pixels[y * tileSize + x] = c;
                }
            }
            return pixels;
        }

    }
}

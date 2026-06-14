using Microsoft.Xna.Framework;
using System.Linq;

namespace Autonocraft.Engine
{
    /// <summary>
    /// High-level procedural tile generators shared by <see cref="ProceduralAtlasBuilder"/>.
    /// Each method returns a flat RGBA pixel buffer (tileSize × tileSize).
    /// </summary>
    internal static class ProceduralTextureSynth
    {
        private const int CellOrganic = 6;
        private const int CellEarth = 8;
        private const int CellWood = 4;

        /// <summary>Standard grounded surface — pixel clusters, scatter, rim shading.</summary>
        public static Color[] Surface(int tileSize, string name, Color[] palette, int scatterCount = 18)
        {
            var image = new TileImage(PixelCluster(tileSize, name, palette[Math.Min(1, palette.Length - 1)], palette, CellEarth, 18), tileSize);
            ScatterRects(image, name, palette, scatterCount, 2);
            ApplyCellRims(image, CellEarth, -12, 6);
            return image.Pixels;
        }

        public static Color[] GrassTop(int tileSize, string name, Color[] palette)
        {
            var pixels = PixelCluster(tileSize, name, palette[1], palette, CellOrganic, 14);
            var image = new TileImage(pixels, tileSize);

            for (int i = 0; i < 48; i++)
            {
                int x = Noise(name, i, 3, 7) % tileSize;
                int yBase = tileSize - 2 - Noise(name, i, 5, 9) % 6;
                int tipX = x + (Noise(name, i, 11, 13) % 15) - 7;
                int tipY = Noise(name, i, 17, 19) % (tileSize / 2);
                Color blade = palette[Noise(name, i, 23, 29) % palette.Length];
                DrawBlade(image, x, yBase, tipX, tipY, blade, 2);
                SetPixel(image, tipX, tipY, Lighten(blade, 10));
            }

            ScatterRects(image, name + "_tuft", palette, 18, 2);
            for (int i = 0; i < 3; i++)
            {
                int x = Noise(name, i, 41, 43) % tileSize;
                int y = Noise(name, i, 47, 49) % tileSize;
                SetPixel(image, x, y, new Color(88, 108, 52));
            }

            ApplyCellRims(image, CellOrganic, -16, 4);
            return image.Pixels;
        }

        public static Color[] GrassFringe(int tileSize, string name, Color[] palette)
        {
            var pixels = PixelCluster(tileSize, name, palette[0], palette, CellOrganic, 18);
            var image = new TileImage(pixels, tileSize);
            int fringeRows = Math.Max(8, tileSize * 42 / 100);

            for (int i = 0; i < 64; i++)
            {
                int x = Noise(name, i, 3, 5) % tileSize;
                int len = 6 + Noise(name, i, 7, 9) % fringeRows;
                Color blade = palette[Noise(name, i, 11, 13) % palette.Length];
                for (int d = 0; d < len; d++)
                {
                    int y = d;
                    int sway = (Noise(name, i, 17, 19) % 3) - 1;
                    SetPixel(image, x + sway, y, d == 0 ? Lighten(blade, 8) : blade);
                }
            }

            return image.Pixels;
        }

        public static Color[] Dirt(int tileSize, string name, Color[] palette)
        {
            var image = new TileImage(PixelCluster(tileSize, name, palette[1], palette, CellEarth, 22), tileSize);
            ScatterRects(image, name, palette, 22, 3);

            for (int i = 0; i < 10; i++)
            {
                int x = Noise(name, i, 31, 33) % tileSize;
                int y = Noise(name, i, 35, 37) % tileSize;
                int r = 3 + Noise(name, i, 39, 41) % 5;
                FillEllipse(image, x - r, y - r, x + r, y + r, Darken(palette[0], 18));
            }

            ApplyCellRims(image, CellEarth, -12, 6);
            return image.Pixels;
        }

        public static Color[] MossStone(int tileSize, string name, Color[] stonePalette, Color grout, Color[] mossPalette)
        {
            var image = new TileImage(Stone(tileSize, name, stonePalette, grout), tileSize);
            PaintBlobs(image, name + "_moss", mossPalette, 22, 4, 10);
            return image.Pixels;
        }

        public static Color[] CactusSprite(int tileSize, string name, Color body, Color ribDark, Color ribLight, Color spine)
        {
            var image = new TileImage(FillSolid(tileSize, Color.Transparent), tileSize);
            int cx = tileSize / 2;
            int bodyW = Math.Max(6, tileSize / 5);
            int top = tileSize / 10;
            int bottom = tileSize - 6;
            FillRect(image, cx - bodyW / 2, top + bodyW / 2, bodyW, bottom - top - bodyW / 2, body);
            FillEllipse(image, cx - bodyW / 2, top, cx + bodyW / 2, top + bodyW, body);
            for (int x = cx - bodyW / 2; x <= cx + bodyW / 2; x += Math.Max(2, bodyW / 3))
            {
                DrawLine(image, x, top + 4, x, bottom - 4, ribDark, 1);
            }

            DrawLine(image, cx - bodyW / 2 - 1, top + bodyW / 2, cx - bodyW / 2 - 1, bottom - 4, ribLight, 1);
            DrawLine(image, cx + bodyW / 2 + 1, top + bodyW / 2, cx + bodyW / 2 + 1, bottom - 4, ribLight, 1);
            ScatterRects(image, name + "_spine", new[] { spine }, 16, 1);
            return image.Pixels;
        }

        public static Color[] FlowerPatch(int tileSize, string name, Color[] grassPalette, Color[] petalColors)
        {
            var image = new TileImage(Surface(tileSize, name, grassPalette, 14), tileSize);
            for (int i = 0; i < 11; i++)
            {
                int x = 10 + Noise(name, i, 5, 25) % (tileSize - 20);
                int y = 16 + Noise(name, i, 7, 27) % (tileSize - 30);
                Color petal = petalColors[Noise(name, i, 9, 31) % petalColors.Length];
                FillRect(image, x - 2, y - 2, 5, 5, petal);
                SetPixel(image, x, y, new Color(246, 236, 180));
            }

            return image.Pixels;
        }

        public static Color[] FloraSprite(int tileSize, string name, Color[] palette, int bladeCount, bool addHeads = false)
        {
            var image = new TileImage(FillSolid(tileSize, Color.Transparent), tileSize);
            for (int i = 0; i < bladeCount; i++)
            {
                int x = Noise(name, i, 11, 17) % tileSize;
                int y1 = tileSize / 5 + Noise(name, i, 19, 21) % (tileSize / 2);
                Color blade = palette[Noise(name, i, 23, 29) % palette.Length];
                int tipX = Math.Clamp(x + (Noise(name, i, 31, 33) % 13) - 6, 0, tileSize - 1);
                DrawBlade(image, x, tileSize - 2, tipX, y1, blade, 2);
                SetPixel(image, tipX, y1, Lighten(blade, 24));
                if (addHeads && i % 4 == 0)
                {
                    SetPixel(image, tipX, y1 - 1, new Color(72, 142, 58));
                }
            }

            return image.Pixels;
        }

        public static Color[] SunflowerSprite(int tileSize, string name, Color stem, Color petal, Color center)
        {
            var image = new TileImage(FillSolid(tileSize, Color.Transparent), tileSize);
            int cx = tileSize / 2;
            int cy = tileSize / 3;
            DrawBlade(image, cx, tileSize - 3, cx, cy + 18, stem, 3);
            for (int angle = 0; angle < 360; angle += 30)
            {
                double rad = angle * Math.PI / 180.0;
                int x2 = (int)(cx + Math.Cos(rad) * 22);
                int y2 = (int)(cy + Math.Sin(rad) * 16);
                FillEllipse(image, x2 - 6, y2 - 6, x2 + 6, y2 + 6, petal);
            }

            FillEllipse(image, cx - 12, cy - 10, cx + 12, cy + 10, center);
            return image.Pixels;
        }

        public static Color[] PalmLeaves(int tileSize, string name, Color[] palette)
        {
            var image = new TileImage(Leaves(tileSize, name, palette), tileSize);
            int cx = tileSize / 2;
            int cy = tileSize / 3;
            Color frond = Darken(palette[0], 12);
            for (int angle = 0; angle < 360; angle += 30)
            {
                double rad = angle * Math.PI / 180.0;
                int x2 = (int)(cx + Math.Cos(rad) * tileSize * 0.38);
                int y2 = (int)(cy + Math.Sin(rad) * tileSize * 0.28);
                DrawLine(image, cx, cy, x2, y2, frond, 4);
            }

            FillEllipse(image, cx - 8, cy - 6, cx + 8, cy + 6, Lighten(palette[1], 10));
            return image.Pixels;
        }

        public static Color[] GlassTile(int tileSize, string name)
        {
            var palette = new[]
            {
                new Color(168, 208, 228),
                new Color(180, 220, 240),
                new Color(196, 232, 248)
            };
            var image = new TileImage(PixelCluster(tileSize, name, palette[1], palette, CellOrganic, 8), tileSize);
            DrawLine(image, 0, 0, tileSize - 1, tileSize - 1, new Color(220, 240, 255, 180), 2);
            DrawLine(image, tileSize - 1, 0, 0, tileSize - 1, new Color(140, 190, 220, 140), 2);
            ApplyCellRims(image, CellOrganic, -8, 10);
            return image.Pixels;
        }

        public static Color[] IceTile(int tileSize, string name)
        {
            var palette = new[]
            {
                new Color(156, 200, 228),
                new Color(168, 212, 238),
                new Color(188, 228, 248)
            };
            var image = new TileImage(PixelCluster(tileSize, name, palette[1], palette, CellOrganic, 8), tileSize);
            DrawLine(image, 0, tileSize / 4, tileSize - 1, tileSize / 4 + 6, new Color(200, 232, 248, 200), 2);
            DrawLine(image, tileSize / 3, 0, tileSize / 3 + 8, tileSize - 1, new Color(140, 190, 228, 160), 2);
            ApplyCellRims(image, CellOrganic, -6, 12);
            return image.Pixels;
        }

        public static Color[] HayBale(int tileSize, string name, Color[] palette, Color seam, Color border)
        {
            var image = new TileImage(PixelCluster(tileSize, name, palette[1], palette, CellEarth, 14), tileSize);
            for (int y = tileSize / 8; y < tileSize; y += tileSize / 6)
            {
                DrawLine(image, 0, y, tileSize - 1, y, seam, 2);
            }

            int inset = tileSize / 6;
            DrawRectOutline(image, inset, inset, tileSize - inset, tileSize - inset, border, 3);
            ApplyCellRims(image, CellEarth, -10, 5);
            return image.Pixels;
        }

        public static Color[] ForgeStation(int tileSize, string name, Color[] palette, Color frame, Color ember)
        {
            var image = new TileImage(Surface(tileSize, name, palette, 10), tileSize);
            for (int i = 0; i < 6; i++)
            {
                int x = tileSize / 4 + i * 8;
                FillRect(image, x, tileSize / 3, 6, tileSize / 3, new Color(42, 42, 44));
            }

            DrawRectOutline(image, tileSize / 4, tileSize / 4, tileSize * 3 / 4, tileSize * 3 / 4, ember, 4);
            DrawLine(image, tileSize / 4, tileSize / 4, tileSize * 3 / 4, tileSize / 4, frame, 2);
            return image.Pixels;
        }

        public static Color[] CrucibleStation(int tileSize, string name, Color[] palette, Color rim, Color liquid)
        {
            var image = new TileImage(Surface(tileSize, name, palette, 8), tileSize);
            int inset = tileSize / 5;
            DrawRectOutline(image, inset, inset, tileSize - inset, tileSize - inset, rim, 4);
            FillEllipse(image, tileSize / 3, tileSize / 3, tileSize * 2 / 3, tileSize * 2 / 3, liquid);
            return image.Pixels;
        }

        public static Color[] AnimalHide(int tileSize, string name, Color baseColor, Color accent)
        {
            var palette = ExpandPalette(baseColor, Darken(baseColor, 16), Lighten(baseColor, 14));
            var image = new TileImage(PixelCluster(tileSize, name, baseColor, palette, CellOrganic, 10), tileSize);
            int margin = tileSize / 6;
            DrawRectOutline(image, margin, margin, tileSize - margin, tileSize - margin, Darken(accent, 24), 3);
            ScatterRects(image, name + "_f", new[] { accent, Lighten(accent, 16) }, 10, 2);
            ApplyCellRims(image, CellOrganic, -8, 6);
            return image.Pixels;
        }

        public static Color[] Stone(int tileSize, string name, Color[] palette, Color grout)
        {
            var image = new TileImage(Voronoi(tileSize, name, palette, grout, 16, 2.8f), tileSize);

            for (int i = 0; i < 10; i++)
            {
                int x0 = Noise(name, i, 3, 17) % (tileSize - 20);
                int y0 = Noise(name, i, 7, 19) % (tileSize - 20);
                DrawLine(image, x0, y0, x0 + 14 + Noise(name, i, 11, 21) % 12, y0 + Noise(name, i, 13, 23) % 4, Darken(grout, 28), 1);
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
            var image = new TileImage(PixelCluster(tileSize, name, Darken(palette[1], 10), palette, CellOrganic, 14), tileSize);
            PaintBlobs(image, name, palette, 28, 6, 18);
            PaintBlobs(image, name + "_hi", palette.Select(c => Lighten(c, 26)).ToArray(), 14, 4, 12);
            PaintBlobs(image, name + "_dk", palette.Select(c => Darken(c, 24)).ToArray(), 10, 8, 20);
            return image.Pixels;
        }

        public static Color[] Sand(int tileSize, string name, Color[] palette)
        {
            var image = new TileImage(PixelCluster(tileSize, name, palette[1], palette, CellOrganic, 16), tileSize);
            ScatterRects(image, name, palette, 30, 2);
            for (int i = 0; i < 18; i++)
            {
                int x = Noise(name, i, 3, 5) % tileSize;
                int y = Noise(name, i, 7, 9) % tileSize;
                SetPixel(image, x, y, Lighten(palette[2], 20));
            }

            ApplyCellRims(image, CellOrganic, -10, 6);
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
            var image = new TileImage(PixelCluster(tileSize, name, stone, stonePalette, CellEarth, 18), tileSize);

            for (int cluster = 0; cluster < 6; cluster++)
            {
                int cx = Noise(name, cluster, 3, 7) % (tileSize - 16) + 8;
                int cy = Noise(name, cluster, 5, 9) % (tileSize - 16) + 8;
                int pieces = 3 + Noise(name, cluster, 11, 13) % 4;
                for (int p = 0; p < pieces; p++)
                {
                    int x = cx + (Noise(name, cluster, p, 17) % 10) - 5;
                    int y = cy + (Noise(name, cluster, p, 19) % 10) - 5;
                    int s = 4 + Noise(name, cluster, p, 21) % 4;
                    FillRect(image, x, y, s, s, ore);
                    SetPixel(image, x + 1, y + 1, oreHi);
                    SetPixel(image, x + s - 1, y, Darken(ore, 20));
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
                int offset = (row % 2) * (tileSize / 10);
                int y = row * rowH;
                for (int col = 0; col < 4; col++)
                {
                    int x = offset + col * (tileSize / 4);
                    Color tone = (row + col) % 2 == 0 ? brickHi : brickLo;
                    FillRect(image, x + 3, y + 3, tileSize / 4 - 5, rowH - 5, tone);
                    DrawRectOutline(image, x + 2, y + 2, x + tileSize / 4 - 2, y + rowH - 2, Darken(brick, 24), 1);
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
                        pixels[y * tileSize + x] = Lerp(Lighten(nearest, 12), Darken(nearest, 16), shade);
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
    }
}

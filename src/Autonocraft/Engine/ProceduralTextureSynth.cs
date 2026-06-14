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
        private const int CellOrganic = 3;
        private const int CellEarth = 3;
        private const int CellWood = 2;

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
            var pixels = PixelCluster(tileSize, name, palette[1], palette, CellOrganic, 12);
            var image = new TileImage(pixels, tileSize);

            // Scatter lush grass blade clumps
            for (int i = 0; i < 35; i++)
            {
                int cx = Noise(name, i, 3, 7) % tileSize;
                int cy = Noise(name, i, 5, 9) % tileSize;
                Color bladeColor = palette[Noise(name, i, 11, 13) % palette.Length];
                // Y-shape tuft
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

            ApplyCellRims(image, CellOrganic, -10, 4);
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
            var image = new TileImage(PixelCluster(tileSize, name, palette[1], palette, CellEarth, 16), tileSize);

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

            ApplyCellRims(image, CellEarth, -10, 4);
            return image.Pixels;
        }

        public static Color[] MossStone(int tileSize, string name, Color[] stonePalette, Color grout, Color[] mossPalette)
        {
            var image = new TileImage(Cobble(tileSize, name, stonePalette, grout), tileSize);
            PaintBlobs(image, name + "_moss", mossPalette, 18, 4, 10);
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

        public static Color[] ForgeStation(int tileSize, string name, Color[] palette, Color frame, Color ember)
        {
            // Background is a nice brick/stone pattern
            var basePalette = new[] { new Color(100, 100, 105), new Color(120, 120, 125), new Color(80, 80, 84) };
            var image = new TileImage(Brick(tileSize, name, basePalette[0], Darken(basePalette[2], 16), basePalette[1], basePalette[2]), tileSize);

            // Center furnace opening
            int cx = tileSize / 2;
            int cy = tileSize / 2;
            int size = tileSize / 2;
            int x = cx - size / 2;
            int y = cy - size / 2;

            // Black firebox
            FillRect(image, x, y, size, size, new Color(24, 24, 26));
            
            // Stone arch/frame
            DrawRectOutline(image, x - 2, y - 2, x + size + 2, y + size + 2, frame, 3);
            DrawRectOutline(image, x - 1, y - 1, x + size + 1, y + size + 1, Lighten(frame, 16), 1);

            // Glowing hot embers and fire inside the box
            for (int i = 0; i < 15; i++)
            {
                int ex = x + 3 + Noise(name, i, 3, 11) % (size - 6);
                int ey = y + size - 8 - Noise(name, i, 7, 13) % 10;
                int r = 2 + Noise(name, i, 11, 17) % 3;
                
                Color col = i % 3 == 0 ? new Color(255, 230, 60) : (i % 2 == 0 ? new Color(255, 110, 30) : new Color(220, 40, 20));
                FillEllipse(image, ex - r, ey - r, ex + r, ey + r, col);
            }

            return image.Pixels;
        }

        public static Color[] CrucibleStation(int tileSize, string name, Color[] palette, Color rim, Color liquid)
        {
            var basePalette = new[] { new Color(110, 110, 114), new Color(130, 130, 135), new Color(90, 90, 94) };
            var image = new TileImage(Voronoi(tileSize, name, basePalette, Darken(basePalette[2], 12), 14, 2.5f), tileSize);

            int cx = tileSize / 2;
            int cy = tileSize / 2;
            int r = tileSize / 3;

            // Outer dark metal rim
            FillEllipse(image, cx - r - 4, cy - r - 4, cx + r + 4, cy + r + 4, new Color(48, 48, 52));
            FillEllipse(image, cx - r, cy - r, cx + r, cy + r, rim);

            // Glowing liquid core
            FillEllipse(image, cx - r + 5, cy - r + 5, cx + r - 5, cy + r - 5, liquid);

            // Add magic energy rings / runic glints inside the pool
            Color runeColor = Lighten(liquid, 40);
            FillEllipse(image, cx - r + 9, cy - r + 9, cx + r - 9, cy + r - 9, Darken(liquid, 16));
            FillEllipse(image, cx - r + 13, cy - r + 13, cx + r - 13, cy + r - 13, runeColor);
            FillEllipse(image, cx - r + 16, cy - r + 16, cx + r - 16, cy + r - 16, liquid);

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

        public static Color[] SheepBody(int tileSize, string name, Color baseColor, Color accent)
        {
            var image = new TileImage(FillSolid(tileSize, new Color(210, 210, 210)), tileSize);

            for (int i = 0; i < 28; i++)
            {
                int cx = Noise(name, i, 3, 7) % tileSize;
                int cy = Noise(name, i, 5, 9) % tileSize;
                int r = 10 + Noise(name, i, 11, 13) % 8;

                Color woolColor = i % 2 == 0 ? new Color(245, 245, 245) : new Color(230, 230, 230);
                
                FillEllipse(image, cx - r, cy - r, cx + r, cy + r, Darken(woolColor, 20));
                FillEllipse(image, cx - r + 1, cy - r + 1, cx + r - 1, cy + r - 1, woolColor);
                FillEllipse(image, cx - r + 2, cy - r + 2, cx + r / 4, cy + r / 4, new Color(255, 255, 255));
            }

            return image.Pixels;
        }

        public static Color[] ChickenBody(int tileSize, string name, Color baseColor, Color accent)
        {
            var image = new TileImage(FillSolid(tileSize, baseColor), tileSize);

            int rowSpacing = tileSize / 8;
            for (int y = 4; y < tileSize; y += rowSpacing)
            {
                int offset = (y / rowSpacing % 2) * (tileSize / 16);
                for (int x = -10; x < tileSize + 10; x += tileSize / 6)
                {
                    int cx = x + offset;
                    int cy = y;
                    int r = tileSize / 10;
                    
                    Color featherColor = Lighten(baseColor, (y / rowSpacing) * 3 - 8);
                    
                    FillEllipse(image, cx - r, cy - r, cx + r, cy + r, Darken(featherColor, 18));
                    FillEllipse(image, cx - r + 1, cy - r + 1, cx + r - 1, cy + r - 1, featherColor);
                    for (int tx = cx - r + 2; tx <= cx + r - 2; tx++)
                    {
                        SetPixel(image, tx, cy + r - 1, Lighten(featherColor, 22));
                    }
                }
            }

            return image.Pixels;
        }

        public static Color[] PigBody(int tileSize, string name, Color baseColor, Color accent)
        {
            var palette = ExpandPalette(baseColor, Darken(baseColor, 12), Lighten(baseColor, 12));
            var image = new TileImage(PixelCluster(tileSize, name, baseColor, palette, CellOrganic, 8), tileSize);

            Color spotColor = new Color(110, 80, 65);
            Color spotPink = Darken(baseColor, 26);
            for (int i = 0; i < 4; i++)
            {
                int cx = Noise(name, i, 11, 13) % tileSize;
                int cy = Noise(name, i, 17, 19) % tileSize;
                int r = 6 + Noise(name, i, 23, 29) % 8;
                
                Color color = i % 2 == 0 ? spotColor : spotPink;
                FillEllipse(image, cx - r, cy - r, cx + r, cy + r, color);
                FillEllipse(image, cx - r + 2, cy - r + 1, cx + r - 1, cy + r - 1, Lighten(color, 8));
            }

            ApplyCellRims(image, CellOrganic, -8, 6);
            return image.Pixels;
        }

        public static Color[] AnimalHead(int tileSize, string name, Color baseColor, Color accent, string animalType)
        {
            var palette = ExpandPalette(baseColor, Darken(baseColor, 14), Lighten(baseColor, 12));
            var image = new TileImage(PixelCluster(tileSize, name, baseColor, palette, CellOrganic, 8), tileSize);
            int margin = tileSize / 8;
            DrawRectOutline(image, margin, margin, tileSize - margin, tileSize - margin, Darken(accent, 20), 2);

            int cx = tileSize / 2;
            int cy = tileSize / 2;

            if (animalType == "sheep")
            {
                int eyeY = cy - tileSize / 16;
                int eyeOffset = tileSize / 4;
                FillRect(image, cx - eyeOffset - 4, eyeY - 2, 8, 5, new Color(255, 255, 255));
                FillRect(image, cx - eyeOffset - 2, eyeY - 2, 4, 5, new Color(0, 0, 0));
                FillRect(image, cx + eyeOffset - 4, eyeY - 2, 8, 5, new Color(255, 255, 255));
                FillRect(image, cx + eyeOffset - 2, eyeY - 2, 4, 5, new Color(0, 0, 0));

                FillRect(image, cx - 6, cy + tileSize / 8, 13, 7, new Color(240, 150, 160));
                SetPixel(image, cx - 2, cy + tileSize / 8 + 2, new Color(80, 30, 40));
                SetPixel(image, cx + 2, cy + tileSize / 8 + 2, new Color(80, 30, 40));

                for (int x = margin; x < tileSize - margin; x += 6)
                {
                    int r = 5 + Noise(name, x, 73) % 4;
                    FillEllipse(image, x - r, margin - r + 3, x + r, margin + r, new Color(245, 245, 245));
                }
            }
            else if (animalType == "pig")
            {
                int eyeY = cy - tileSize / 12;
                int eyeOffset = tileSize / 5;
                FillRect(image, cx - eyeOffset - 3, eyeY - 2, 6, 5, new Color(255, 255, 255));
                FillRect(image, cx - eyeOffset - 1, eyeY - 2, 3, 5, new Color(0, 0, 0));
                FillRect(image, cx + eyeOffset - 3, eyeY - 2, 6, 5, new Color(255, 255, 255));
                FillRect(image, cx + eyeOffset - 1, eyeY - 2, 3, 5, new Color(0, 0, 0));

                int snoutW = tileSize / 3;
                int snoutH = tileSize / 5;
                Color snoutColor = Darken(baseColor, 22);
                FillRect(image, cx - snoutW / 2, cy + 2, snoutW, snoutH, snoutColor);
                DrawRectOutline(image, cx - snoutW / 2, cy + 2, cx + snoutW / 2, cy + 2 + snoutH, Darken(snoutColor, 20), 1);
                
                FillRect(image, cx - snoutW / 4 - 1, cy + 2 + snoutH / 2 - 1, 3, 3, new Color(60, 20, 20));
                FillRect(image, cx + snoutW / 4 - 2, cy + 2 + snoutH / 2 - 1, 3, 3, new Color(60, 20, 20));
            }
            else if (animalType == "chicken")
            {
                int eyeY = cy - tileSize / 8;
                int eyeOffset = tileSize / 4;
                FillRect(image, cx - eyeOffset - 3, eyeY - 2, 6, 5, new Color(255, 255, 255));
                FillRect(image, cx - eyeOffset - 1, eyeY - 2, 3, 5, new Color(0, 0, 0));
                FillRect(image, cx + eyeOffset - 3, eyeY - 2, 6, 5, new Color(255, 255, 255));
                FillRect(image, cx + eyeOffset - 1, eyeY - 2, 3, 5, new Color(0, 0, 0));

                int beakSize = tileSize / 6;
                Color beakColor = new Color(255, 170, 30);
                FillEllipse(image, cx - beakSize, cy - 2, cx + beakSize, cy + beakSize + 2, beakColor);
                DrawLine(image, cx - beakSize + 1, cy + 2, cx + beakSize - 1, cy + 2, Darken(beakColor, 24), 1);

                FillRect(image, cx - 4, margin - 6, 9, 8, new Color(220, 30, 30));
                FillRect(image, cx - 3, cy + beakSize + 1, 6, 7, new Color(220, 30, 30));
            }

            ApplyCellRims(image, CellOrganic, -8, 6);
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

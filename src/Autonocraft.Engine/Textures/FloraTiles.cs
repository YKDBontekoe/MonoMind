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

        /// <summary>Side-view flower sprite for vertical billboards (stem + petal cluster).</summary>

        public static Color[] FlowerStemSprite(int tileSize, string name, Color stem, Color[] petalColors, Color center)
        {
            var image = new TileImage(FillSolid(tileSize, Color.Transparent), tileSize);
            int cx = tileSize / 2 + (Noise(name, 0, 3, 5) % 7) - 3;
            int bloomY = tileSize / 4 + Noise(name, 1, 7, 11) % (tileSize / 6);
            DrawBlade(image, cx, tileSize - 2, cx + (Noise(name, 2, 13, 17) % 5) - 2, bloomY + 10, stem, 2);
            Color petal = petalColors[Noise(name, 3, 19, 23) % petalColors.Length];
            int petalCount = 5 + Noise(name, 4, 29, 31) % 2;
            for (int i = 0; i < petalCount; i++)
            {
                double rad = i * (Math.PI * 2.0 / petalCount) + Noise(name, i, 37, 41) * 0.08;
                int px = (int)(cx + Math.Cos(rad) * (tileSize / 7));
                int py = (int)(bloomY + Math.Sin(rad) * (tileSize / 9));
                FillEllipse(image, px - 4, py - 3, px + 4, py + 3, petal);
            }

            FillEllipse(image, cx - 4, bloomY - 3, cx + 4, bloomY + 3, center);
            DrawBlade(image, cx - 6, tileSize / 2, cx - 10, tileSize / 2 + 6, Darken(stem, 8), 1);
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

        public static Color[] TallGrassClump(int tileSize, string name, Color[] palette)
        {
            return FloraSprite(tileSize, name, palette, 34);
        }

        public static Color[] ShortGrassSprite(int tileSize, string name, Color[] palette)
        {
            var image = new TileImage(FillSolid(tileSize, Color.Transparent), tileSize);
            for (int i = 0; i < 22; i++)
            {
                int x = Noise(name, i, 11, 17) % tileSize;
                int y1 = tileSize * 2 / 3 + Noise(name, i, 19, 21) % (tileSize / 4);
                Color blade = palette[Noise(name, i, 23, 29) % palette.Length];
                int tipX = Math.Clamp(x + (Noise(name, i, 31, 33) % 9) - 4, 0, tileSize - 1);
                DrawBlade(image, x, tileSize - 2, tipX, y1, blade, 2);
                SetPixel(image, tipX, y1, Lighten(blade, 18));
            }

            return image.Pixels;
        }

        public static Color[] ReedSprite(int tileSize, string name, Color[] palette, Color head)
        {
            var image = new TileImage(FillSolid(tileSize, Color.Transparent), tileSize);
            for (int i = 0; i < 10; i++)
            {
                int x = tileSize / 4 + Noise(name, i, 11, 17) % (tileSize / 2);
                int top = tileSize / 6 + Noise(name, i, 19, 21) % (tileSize / 3);
                Color stalk = palette[Noise(name, i, 23, 29) % palette.Length];
                DrawBlade(image, x, tileSize - 2, x + (Noise(name, i, 31, 33) % 5) - 2, top, stalk, 2);
                for (int seg = top; seg < tileSize - 4; seg += 8)
                {
                    SetPixel(image, x, seg, Darken(stalk, 10));
                }

                if (i % 2 == 0)
                {
                    FillEllipse(image, x - 3, top - 4, x + 3, top + 2, head);
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
            DrawBlade(image, cx - 5, tileSize / 2, cx - 9, tileSize / 2 + 8, Darken(stem, 10), 2);
            for (int ring = 0; ring < 2; ring++)
            {
                int radiusX = 20 - ring * 4;
                int radiusY = 14 - ring * 3;
                Color ringPetal = ring == 0 ? petal : Darken(petal, 12);
                for (int angle = 0; angle < 360; angle += 30)
                {
                    double rad = angle * Math.PI / 180.0;
                    int x2 = (int)(cx + Math.Cos(rad) * radiusX);
                    int y2 = (int)(cy + Math.Sin(rad) * radiusY);
                    FillEllipse(image, x2 - 6, y2 - 5, x2 + 6, y2 + 5, ringPetal);
                }
            }

            FillEllipse(image, cx - 12, cy - 10, cx + 12, cy + 10, center);
            FillEllipse(image, cx - 6, cy - 5, cx + 2, cy + 1, Lighten(center, 16));
            return image.Pixels;
        }

        public static Color[] WheatCropSprite(int tileSize, string name, Color stem, Color head)
        {
            var image = new TileImage(FillSolid(tileSize, Color.Transparent), tileSize);
            int cx = tileSize / 2;
            DrawBlade(image, cx, tileSize - 2, cx, tileSize / 3, stem, 3);
            for (int i = -2; i <= 2; i++)
            {
                int top = tileSize / 3 - i * 4;
                DrawBlade(image, cx, top + 10, cx + i * 5, top, Darken(head, i * 4), 2);
            }

            FillEllipse(image, cx - 8, tileSize / 4 - 6, cx + 8, tileSize / 4 + 8, head);
            return image.Pixels;
        }

        public static Color[] CarrotCropSprite(int tileSize, string name, Color stem, Color root)
        {
            var image = new TileImage(FillSolid(tileSize, Color.Transparent), tileSize);
            int cx = tileSize / 2;
            DrawBlade(image, cx, tileSize - 2, cx, tileSize / 2, stem, 3);
            DrawBlade(image, cx - 4, tileSize / 2, cx - 10, tileSize / 2 + 6, Darken(stem, 8), 2);
            DrawBlade(image, cx + 4, tileSize / 2, cx + 10, tileSize / 2 + 6, Darken(stem, 8), 2);
            FillEllipse(image, cx - 7, tileSize / 2 + 4, cx + 7, tileSize - 4, root);
            FillEllipse(image, cx - 4, tileSize / 2 + 8, cx + 4, tileSize - 6, Lighten(root, 12));
            return image.Pixels;
        }

        public static Color[] FernSprite(int tileSize, string name, Color[] palette)
        {
            var image = new TileImage(FillSolid(tileSize, Color.Transparent), tileSize);
            int cx = tileSize / 2;
            DrawBlade(image, cx, tileSize - 2, cx, tileSize / 3, palette[0], 2);
            for (int i = 0; i < 12; i++)
            {
                int y = tileSize - 4 - i * (tileSize / 16);
                bool left = i % 2 == 0;
                int length = Math.Max(4, tileSize / 3 - i * (tileSize / 30));
                Color frondColor = palette[Noise(name, i, 5, 7) % palette.Length];
                int dx = left ? -length : length;
                int dy = -length / 2;
                DrawBlade(image, cx, y, cx + dx, y + dy, frondColor, 2);
                SetPixel(image, cx + dx, y + dy, Lighten(frondColor, 15));
            }
            return image.Pixels;
        }

        public static Color[] MushroomSprite(int tileSize, string name, Color capColor, Color? spotColor)
        {
            var image = new TileImage(FillSolid(tileSize, Color.Transparent), tileSize);
            int cx = tileSize / 2 + (Noise(name, 0, 1, 0) % 5) - 2;
            Color stemColor = new Color(230, 225, 220);
            for (int y = tileSize - tileSize / 3; y < tileSize - 1; y++)
            {
                for (int x = cx - 2; x <= cx + 2; x++)
                {
                    SetPixel(image, x, y, stemColor);
                }
            }
            int capY = tileSize - tileSize / 3;
            int capR = tileSize / 4 + 1;
            FillEllipse(image, cx - capR, capY - capR / 2, cx + capR, capY + capR / 2, capColor);

            if (spotColor.HasValue)
            {
                for (int i = 0; i < 5; i++)
                {
                    int sx = cx - capR + 2 + Noise(name, i, 9, 0) % (capR * 2 - 3);
                    int sy = capY - capR / 2 + 1 + Noise(name, i, 13, 0) % (capR - 2);
                    SetPixel(image, sx, sy, spotColor.Value);
                }
            }
            return image.Pixels;
        }

        public static Color[] DeadBushSprite(int tileSize, string name, Color[] palette)
        {
            var image = new TileImage(FillSolid(tileSize, Color.Transparent), tileSize);
            for (int i = 0; i < 10; i++)
            {
                int x1 = tileSize / 2;
                int y1 = tileSize - 2;
                Color branchColor = palette[Noise(name, i, 1, 0) % palette.Length];
                int x2 = Noise(name, i, 3, 0) % tileSize;
                int y2 = tileSize / 3 + Noise(name, i, 5, 0) % (tileSize / 3);
                DrawBlade(image, x1, y1, x2, y2, branchColor, 1);
                if (i % 2 == 0)
                {
                    int bx = (x1 + x2) / 2;
                    int by = (y1 + y2) / 2;
                    int bx2 = bx + (Noise(name, i, 7, 0) % (tileSize / 3)) - tileSize / 6;
                    int by2 = by - (Noise(name, i, 9, 0) % (tileSize / 4));
                    DrawBlade(image, bx, by, bx2, by2, Darken(branchColor, 10), 1);
                }
            }
            return image.Pixels;
        }

        public static Color[] LilyPadSprite(int tileSize, string name)
        {
            var image = new TileImage(FillSolid(tileSize, Color.Transparent), tileSize);
            int cx = tileSize / 2;
            int cy = tileSize / 2;
            int r = tileSize / 2 - 6;
            Color color = new Color(58, 128, 48);
            FillEllipse(image, cx - r, cy - r, cx + r, cy + r, color);

            Color veinColor = new Color(78, 158, 58);
            for (int i = 0; i < 8; i++)
            {
                if (i == 0) continue;
                double rad = i * (6.28318 / 8);
                int tx = (int)(cx + Math.Cos(rad) * r);
                int ty = (int)(cy + Math.Sin(rad) * r);
                DrawBlade(image, cx, cy, tx, ty, veinColor, 1);
            }

            for (int y = 0; y < tileSize; y++)
            {
                for (int x = cx; x < tileSize; x++)
                {
                    int dy = y - cy;
                    int dx = x - cx;
                    if (dx > 0 && Math.Abs(dy) <= dx * 0.58)
                    {
                        SetPixel(image, x, y, Color.Transparent);
                    }
                }
            }

            return image.Pixels;
        }

        public static Color[] VineSprite(int tileSize, string name, Color[] palette)
        {
            var image = new TileImage(FillSolid(tileSize, Color.Transparent), tileSize);
            for (int i = 0; i < 3; i++)
            {
                int cx = tileSize / 4 + i * (tileSize / 4) + (Noise(name, i, 3, 0) % 7) - 3;
                Color color = palette[Noise(name, i, 5, 0) % palette.Length];
                int length = tileSize - 2 - (Noise(name, i, 7, 0) % (tileSize / 3));
                DrawBlade(image, cx, 0, cx, length, Darken(color, 10), 2);
                for (int y = 2; y < length; y += 4)
                {
                    Color leafColor = palette[Noise(name, i, y, 0) % palette.Length];
                    bool left = Noise(name, i, y, 1) % 2 == 0;
                    int lx = left ? cx - 2 : cx + 2;
                    SetPixel(image, lx, y, leafColor);
                    SetPixel(image, lx - 1, y, leafColor);
                    SetPixel(image, lx + 1, y, leafColor);
                    SetPixel(image, lx, y - 1, leafColor);
                    SetPixel(image, lx, y + 1, leafColor);
                }
            }
            return image.Pixels;
        }

        public static Color[] BerryBushSprite(int tileSize, string name, Color[] palette)
        {
            var image = new TileImage(FillSolid(tileSize, Color.Transparent), tileSize);
            int cx = tileSize / 2;
            int cy = tileSize / 2 + 4;
            int r = tileSize / 3;
            FillEllipse(image, cx - r, cy - r, cx + r, cy + r, palette[1]);
            for (int i = 0; i < 6; i++)
            {
                int offsetX = (Noise(name, i, 3, 0) % (r * 2)) - r;
                int offsetY = (Noise(name, i, 5, 0) % (r * 2)) - r;
                int cr = r / 2 + Noise(name, i, 7, 0) % (r / 3);
                Color leafColor = palette[Noise(name, i, 9, 0) % palette.Length];
                FillEllipse(image, cx + offsetX - cr, cy + offsetY - cr, cx + offsetX + cr, cy + offsetY + cr, leafColor);
            }
            Color berryColor = new Color(220, 40, 40);
            for (int i = 0; i < 8; i++)
            {
                int bx = cx - r + 3 + Noise(name, i, 11, 0) % (r * 2 - 6);
                int by = cy - r + 3 + Noise(name, i, 13, 0) % (r * 2 - 6);
                FillEllipse(image, bx - 1, by - 1, bx + 1, by + 1, berryColor);
            }
            return image.Pixels;
        }

        public static Color[] SeagrassSprite(int tileSize, string name, Color[] palette)
        {
            var image = new TileImage(FillSolid(tileSize, Color.Transparent), tileSize);
            for (int i = 0; i < 6; i++)
            {
                int x = tileSize / 4 + i * (tileSize / 8) + (Noise(name, i, 3, 0) % 5) - 2;
                Color color = palette[Noise(name, i, 5, 0) % palette.Length];
                int prevX = x;
                int height = tileSize / 2 + Noise(name, i, 7, 0) % (tileSize / 3);
                for (int step = 0; step < height; step++)
                {
                    int currY = tileSize - 2 - step;
                    int wave = (int)(Math.Sin(step * 0.3 + i) * 3);
                    int currX = x + wave;
                    DrawBlade(image, prevX, currY + 1, currX, currY, color, 2);
                    prevX = currX;
                }
            }
            return image.Pixels;
        }

        /// <summary>Packs four variant sprites into the quadrants of a full atlas tile.</summary>

        public static Color[] PackFloraVariants(int tileSize, Func<int, int, Color[]> generateVariant)
        {
            int half = tileSize / 2;
            var result = new Color[tileSize * tileSize];
            Array.Fill(result, Color.Transparent);
            for (int variant = 0; variant < 4; variant++)
            {
                var pixels = generateVariant(half, variant);
                int ox = (variant % 2) * half;
                int oy = (variant / 2) * half;
                for (int y = 0; y < half; y++)
                {
                    Array.Copy(pixels, y * half, result, (oy + y) * tileSize + ox, half);
                }
            }

            return result;
        }

        public static Color[] PalmLeaves(int tileSize, string name, Color[] palette)
        {
            var image = new TileImage(PixelCluster(tileSize, name, Darken(palette[0], 8), palette, 5, 10), tileSize);
            int cx = tileSize / 2;
            int cy = tileSize / 2 - tileSize / 10;
            Color rib = Darken(palette[0], 24);
            Color frondDark = Darken(palette[0], 10);
            Color frondLight = Lighten(palette[1], 8);
            for (int angle = 0; angle < 360; angle += 24)
            {
                double rad = angle * Math.PI / 180.0;
                int length = tileSize / 3 + Noise(name, angle, 3, 5) % (tileSize / 8);
                int x2 = (int)(cx + Math.Cos(rad) * length);
                int y2 = (int)(cy + Math.Sin(rad) * length * 0.72);
                DrawLine(image, cx, cy, x2, y2, rib, 2);

                for (int step = 2; step <= 8; step++)
                {
                    float t = step / 8f;
                    int sx = (int)Math.Round(cx + (x2 - cx) * t);
                    int sy = (int)Math.Round(cy + (y2 - cy) * t);
                    int blade = Math.Max(3, (int)(tileSize * (0.10f - t * 0.006f)));
                    double side = rad + Math.PI / 2.0;
                    Color bladeColor = step % 2 == 0 ? frondLight : frondDark;
                    int ax = (int)(sx + Math.Cos(side) * blade);
                    int ay = (int)(sy + Math.Sin(side) * blade * 0.45);
                    int bx = (int)(sx - Math.Cos(side) * blade);
                    int by = (int)(sy - Math.Sin(side) * blade * 0.45);
                    DrawLine(image, sx, sy, ax, ay, bladeColor, 2);
                    DrawLine(image, sx, sy, bx, by, Darken(bladeColor, 8), 2);
                }
            }

            for (int i = 0; i < 20; i++)
            {
                int x = Noise(name, i, 11, 13) % tileSize;
                int y = Noise(name, i, 17, 19) % tileSize;
                SetPixelWrapped(image, x, y, Lighten(palette[1], 10));
            }

            FillEllipse(image, cx - 8, cy - 6, cx + 8, cy + 6, Lighten(palette[1], 5));
            FillEllipse(image, cx - 3, cy - 2, cx + 4, cy + 3, Darken(palette[0], 18));
            ApplySoftBlockLighting(image, 3, 10);
            return image.Pixels;
        }

        public static Color[] SaplingSprite(int tileSize, string name, Color stemColor, Color leafColor)
        {
            var image = new TileImage(FillSolid(tileSize, Color.Transparent), tileSize);
            int cx = tileSize / 2;
            
            // Draw a stem
            int stemTop = tileSize / 2;
            DrawBlade(image, cx, tileSize - 2, cx, stemTop, stemColor, 2);
            DrawBlade(image, cx, (tileSize - 2 + stemTop) / 2, cx - tileSize / 6, stemTop + tileSize / 8, stemColor, 1);
            DrawBlade(image, cx, (tileSize - 2 + stemTop) / 2 + 2, cx + tileSize / 6, stemTop + tileSize / 8, stemColor, 1);

            // Draw leaves on top
            FillEllipse(image, cx - tileSize / 4, stemTop - tileSize / 4, cx + tileSize / 4, stemTop + tileSize / 8, leafColor);
            FillEllipse(image, cx - tileSize / 6, stemTop - tileSize / 3, cx + tileSize / 6, stemTop - tileSize / 8, Lighten(leafColor, 15));
            FillEllipse(image, cx - tileSize / 5, stemTop - tileSize / 6, cx - tileSize / 8, stemTop, Darken(leafColor, 15));
            FillEllipse(image, cx + tileSize / 8, stemTop - tileSize / 6, cx + tileSize / 5, stemTop, Darken(leafColor, 15));

            ApplySoftBlockLighting(image, 3, 10);
            return image.Pixels;
        }
    }
}

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
            else if (animalType == "cow")
            {
                int eyeY = cy - tileSize / 16;
                int eyeOffset = tileSize / 4;
                FillRect(image, cx - eyeOffset - 4, eyeY - 2, 8, 5, new Color(255, 255, 255));
                FillRect(image, cx - eyeOffset - 2, eyeY - 2, 4, 5, new Color(0, 0, 0));
                FillRect(image, cx + eyeOffset - 4, eyeY - 2, 8, 5, new Color(255, 255, 255));
                FillRect(image, cx + eyeOffset - 2, eyeY - 2, 4, 5, new Color(0, 0, 0));

                FillRect(image, margin + 2, margin + 2, 12, 12, accent);
                FillRect(image, tileSize - margin - 14, margin + 2, 12, 12, accent);

                FillRect(image, cx - 16, cy + tileSize / 8, 33, 9, new Color(245, 170, 180));
                SetPixel(image, cx - 6, cy + tileSize / 8 + 3, new Color(100, 50, 60));
                SetPixel(image, cx + 6, cy + tileSize / 8 + 3, new Color(100, 50, 60));
            }
            else if (animalType == "bear")
            {
                int eyeY = cy - tileSize / 12;
                int eyeOffset = tileSize / 4;
                FillRect(image, cx - eyeOffset - 2, eyeY - 2, 4, 4, new Color(0, 0, 0));
                FillRect(image, cx + eyeOffset - 2, eyeY - 2, 4, 4, new Color(0, 0, 0));

                FillRect(image, cx - 10, cy + 4, 21, 11, accent);
                FillRect(image, cx - 4, cy + 4, 9, 5, new Color(10, 10, 10));

                FillRect(image, margin, margin - 4, 13, 9, baseColor);
                FillRect(image, tileSize - margin - 12, margin - 4, 13, 9, baseColor);
            }
            else if (animalType == "fox")
            {
                int eyeY = cy - tileSize / 16;
                int eyeOffset = tileSize / 4;
                FillRect(image, cx - eyeOffset - 2, eyeY - 2, 4, 4, new Color(0, 0, 0));
                FillRect(image, cx + eyeOffset - 2, eyeY - 2, 4, 4, new Color(0, 0, 0));

                FillRect(image, margin + 2, cy + 2, 15, tileSize - margin - cy - 4, accent);
                FillRect(image, tileSize - margin - 16, cy + 2, 15, tileSize - margin - cy - 4, accent);

                FillRect(image, cx - 4, tileSize - margin - 6, 9, 5, new Color(10, 10, 10));

                FillTriangle(image, margin, margin + 8, margin, margin - 6, margin + 12, margin + 8, baseColor);
                FillTriangle(image, tileSize - margin, margin + 8, tileSize - margin, margin - 6, tileSize - margin - 12, margin + 8, baseColor);
            }
            else if (animalType == "deer")
            {
                int eyeY = cy - tileSize / 12;
                int eyeOffset = tileSize / 4;
                FillRect(image, cx - eyeOffset - 3, eyeY - 3, 6, 6, new Color(0, 0, 0));
                FillRect(image, cx + eyeOffset - 3, eyeY - 3, 6, 6, new Color(0, 0, 0));

                SetPixel(image, cx - eyeOffset + 1, eyeY - 2, new Color(255, 255, 255));
                SetPixel(image, cx + eyeOffset - 2, eyeY - 2, new Color(255, 255, 255));

                DrawLine(image, cx - 8, margin, cx - 12, margin - 10, new Color(140, 100, 70), 2);
                DrawLine(image, cx - 12, margin - 10, cx - 18, margin - 14, new Color(140, 100, 70), 2);
                DrawLine(image, cx + 8, margin, cx + 12, margin - 10, new Color(140, 100, 70), 2);
                DrawLine(image, cx + 12, margin - 10, cx + 18, margin - 14, new Color(140, 100, 70), 2);
            }
            else if (animalType == "wolf")
            {
                int eyeY = cy - tileSize / 14;
                int eyeOffset = tileSize / 4;
                FillRect(image, cx - eyeOffset - 3, eyeY - 2, 6, 5, new Color(180, 180, 185));
                FillRect(image, cx - eyeOffset - 1, eyeY - 2, 3, 5, new Color(0, 0, 0));
                FillRect(image, cx + eyeOffset - 3, eyeY - 2, 6, 5, new Color(180, 180, 185));
                FillRect(image, cx + eyeOffset - 1, eyeY - 2, 3, 5, new Color(0, 0, 0));

                FillRect(image, cx - 8, cy + tileSize / 10, 17, 8, new Color(50, 50, 55));
                SetPixel(image, cx - 3, cy + tileSize / 10 + 3, new Color(20, 20, 25));
                SetPixel(image, cx + 3, cy + tileSize / 10 + 3, new Color(20, 20, 25));

                FillTriangle(image, margin + 2, margin + 10, margin + 2, margin - 4, margin + 14, margin + 10, baseColor);
                FillTriangle(image, tileSize - margin - 2, margin + 10, tileSize - margin - 2, margin - 4, tileSize - margin - 14, margin + 10, baseColor);
            }

            ApplyCellRims(image, CellOrganic, -8, 6);
            return image.Pixels;
        }

        public static Color[] CowBody(int tileSize, string name)
        {
            var baseColor = new Color(230, 230, 230);
            var palette = ExpandPalette(baseColor, Darken(baseColor, 10), Lighten(baseColor, 10));
            var image = new TileImage(PixelCluster(tileSize, name, baseColor, palette, CellOrganic, 5), tileSize);
            for (int i = 0; i < 6; i++)
            {
                int cx = Noise(name, i, 11, 13) % tileSize;
                int cy = Noise(name, i, 17, 19) % tileSize;
                int r = 14 + Noise(name, i, 23, 29) % 12;
                FillEllipse(image, cx - r, cy - r, cx + r, cy + r, new Color(40, 40, 42));
                FillEllipse(image, cx - r + 2, cy - r + 2, cx + r - 2, cy + r - 2, new Color(55, 55, 58));
            }
            return image.Pixels;
        }

        public static Color[] BearBody(int tileSize, string name)
        {
            var baseColor = new Color(75, 45, 30);
            var palette = ExpandPalette(baseColor, Darken(baseColor, 12), Lighten(baseColor, 12));
            var image = new TileImage(PixelCluster(tileSize, name, baseColor, palette, CellOrganic, 8), tileSize);
            for (int i = 0; i < 4; i++)
            {
                int cx = Noise(name, i, 15, 17) % tileSize;
                int cy = Noise(name, i, 19, 21) % tileSize;
                int r = 16 + Noise(name, i, 25, 31) % 10;
                FillEllipse(image, cx - r, cy - r, cx + r, cy + r, new Color(55, 30, 20));
            }
            return image.Pixels;
        }

        public static Color[] FoxBody(int tileSize, string name, Color? baseColor = null, Color? bellyColor = null)
        {
            var bodyColor = baseColor ?? new Color(220, 95, 30);
            var belly = bellyColor ?? new Color(240, 235, 225);
            var palette = ExpandPalette(bodyColor, Darken(bodyColor, 12), Lighten(bodyColor, 12));
            var image = new TileImage(PixelCluster(tileSize, name, bodyColor, palette, CellOrganic, 8), tileSize);
            for (int i = 0; i < 3; i++)
            {
                int cx = Noise(name, i, 5, 11) % tileSize;
                int cy = tileSize - 10 - Noise(name, i, 13, 17) % 15;
                int r = 10 + Noise(name, i, 19, 23) % 8;
                FillEllipse(image, cx - r, cy - r, cx + r, cy + r, belly);
            }
            return image.Pixels;
        }

        public static Color[] DeerBody(int tileSize, string name)
        {
            var baseColor = new Color(175, 115, 75);
            var palette = ExpandPalette(baseColor, Darken(baseColor, 10), Lighten(baseColor, 10));
            var image = new TileImage(PixelCluster(tileSize, name, baseColor, palette, CellOrganic, 6), tileSize);
            for (int i = 0; i < 16; i++)
            {
                int cx = Noise(name, i, 3, 11) % tileSize;
                int cy = Noise(name, i, 7, 13) % tileSize;
                FillRect(image, cx, cy, 3, 3, new Color(245, 245, 240));
            }
            return image.Pixels;
        }

        public static Color[] VillagerBody(int tileSize, string name)
        {
            var tunic = new Color(70, 100, 150);
            var palette = ExpandPalette(tunic, Darken(tunic, 16), Lighten(tunic, 14));
            var image = new TileImage(PixelCluster(tileSize, name, tunic, palette, CellOrganic, 6), tileSize);
            int margin = tileSize / 10;
            DrawRectOutline(image, margin, margin, tileSize - margin, tileSize - margin, Darken(tunic, 30), 2);
            DrawLine(image, tileSize / 2, margin, tileSize / 2, tileSize - margin, Darken(tunic, 15), 2);

            int beltY = tileSize * 3 / 5;
            int beltH = tileSize / 8;
            var belt = new Color(80, 50, 20);
            FillRect(image, margin + 2, beltY, tileSize - margin * 2 - 2, beltH, belt);
            FillRect(image, margin + 2, beltY, tileSize - margin * 2 - 2, 2, Lighten(belt, 20));

            int buckle = tileSize / 12;
            FillRect(image, tileSize / 2 - buckle, beltY, buckle * 2, beltH, new Color(200, 180, 50));
            FillRect(image, tileSize / 2 - buckle / 2, beltY + 2, buckle, beltH - 2, belt);

            var collar = Lighten(tunic, 30);
            FillTriangle(image, tileSize / 2, margin + tileSize / 4, tileSize / 2 - tileSize / 6, margin, tileSize / 2 + tileSize / 6, margin, collar);
            ApplyCellRims(image, CellOrganic, -6, 4);
            return image.Pixels;
        }

        public static Color[] VillagerHead(int tileSize, string name)
        {
            var skin = new Color(210, 160, 130);
            var palette = ExpandPalette(skin, Darken(skin, 12), Lighten(skin, 10));
            var image = new TileImage(PixelCluster(tileSize, name, skin, palette, CellOrganic, 5), tileSize);
            int cx = tileSize / 2;
            int cy = tileSize / 2;
            var hair = new Color(60, 40, 20);

            FillRect(image, 0, 0, tileSize, cy - tileSize / 6, hair);
            for (int x = 0; x < tileSize; x += 4)
            {
                DrawLine(image, x, 0, x, cy - tileSize / 6, Lighten(hair, 15), 1);
            }

            int eyeY = cy - tileSize / 8;
            int eyeSpacing = tileSize / 5;
            int eyeSize = Math.Max(2, tileSize / 16);
            FillRect(image, cx - eyeSpacing - eyeSize, eyeY - eyeSize, eyeSize * 2, eyeSize * 2, new Color(240, 240, 240));
            FillRect(image, cx + eyeSpacing - eyeSize, eyeY - eyeSize, eyeSize * 2, eyeSize * 2, new Color(240, 240, 240));
            FillRect(image, cx - eyeSpacing, eyeY - eyeSize / 2, eyeSize, eyeSize + eyeSize / 2, new Color(40, 100, 150));
            FillRect(image, cx + eyeSpacing - eyeSize, eyeY - eyeSize / 2, eyeSize, eyeSize + eyeSize / 2, new Color(40, 100, 150));

            DrawLine(image, cx - eyeSpacing - eyeSize * 2, eyeY - eyeSize * 2, cx - eyeSpacing + eyeSize, eyeY - eyeSize * 2, hair, 2);
            DrawLine(image, cx + eyeSpacing - eyeSize, eyeY - eyeSize * 2, cx + eyeSpacing + eyeSize * 2, eyeY - eyeSize * 2, hair, 2);

            int noseW = tileSize / 10;
            int noseH = tileSize / 6;
            FillRect(image, cx - noseW / 2, cy, noseW, noseH, Darken(skin, 20));
            DrawLine(image, cx - noseW / 2, cy + noseH, cx + noseW / 2, cy + noseH, Darken(skin, 40), 2);

            int mouthY = cy + noseH + tileSize / 10;
            int mouthW = tileSize / 6;
            DrawLine(image, cx - mouthW, mouthY, cx + mouthW, mouthY, new Color(100, 50, 50), 2);
            ApplyCellRims(image, CellOrganic, -6, 4);
            return image.Pixels;
        }

    }
}

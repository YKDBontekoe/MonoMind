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
    }
}

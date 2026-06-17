using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace Autonocraft.Engine
{

    public sealed partial class ProceduralAtlasBuilder
    {
        private Image MakeToolIcon(string stem)
        {
            string[] parts = stem.Split('_');
            if (parts.Length < 3)
            {
                return FillSolid(new Color(0, 0, 0, 0));
            }

            string tier = parts[1];
            string toolType = parts[2];
            var image = FillSolid(new Color(0, 0, 0, 0));

            (Color head, Color headDark) = tier switch
            {
                "wood" => (new Color(168, 122, 72), new Color(118, 82, 48)),
                "stone" => (new Color(156, 156, 162), new Color(104, 104, 110)),
                "iron" => (new Color(204, 210, 218), new Color(142, 148, 158)),
                "gold" => (new Color(252, 214, 72), new Color(198, 158, 28)),
                "copper" => (new Color(230, 115, 60), new Color(180, 85, 40)),
                "silver" => (new Color(210, 220, 225), new Color(155, 165, 170)),
                "diamond" => (new Color(60, 220, 230), new Color(30, 160, 180)),
                "emerald" => (new Color(40, 220, 110), new Color(20, 160, 70)),
                _ => (new Color(160, 160, 160), new Color(110, 110, 110))
            };

            Color handle = new Color(124, 88, 52);
            Color handleDark = new Color(84, 58, 34);
            Color highlight = Shade(head, 36);
            int s = Math.Max(4, _tileSize / 16);
            int cx = _tileSize / 2;
            int cy = _tileSize / 2;

            switch (toolType)
            {
                case "pickaxe":
                    DrawToolLine(image, cx - 3 * s, cy + 4 * s, cx + s, cy - s, handle, s);
                    DrawToolLine(image, cx - 3 * s + 1, cy + 4 * s + 1, cx + s + 1, cy - s + 1, handleDark, Math.Max(1, s / 3));
                    FillPolygon(image, new[]
                    {
                        (cx - 5 * s, cy - s),
                        (cx - 3 * s, cy - 3 * s),
                        (cx + 3 * s, cy - 3 * s),
                        (cx + 5 * s, cy - s),
                        (cx + 4 * s, cy),
                        (cx, cy - 2 * s),
                        (cx - 4 * s, cy)
                    }, head);
                    DrawToolLine(image, cx - 4 * s, cy - 2 * s, cx + 4 * s, cy - 2 * s, highlight, Math.Max(1, s / 2));
                    FillTriangle(image, cx - 5 * s, cy - s, cx - 6 * s, cy + s, cx - 4 * s, cy, headDark);
                    FillTriangle(image, cx + 5 * s, cy - s, cx + 6 * s, cy + s, cx + 4 * s, cy, headDark);
                    break;
                case "axe":
                    DrawToolLine(image, cx - s / 2, cy + 4 * s, cx - s / 2, cy - 2 * s, handle, s);
                    FillRect(image, cx - s - 1, cy - 3 * s, 2 * s + 2, 2 * s, headDark);
                    FillPolygon(image, new[]
                    {
                        (cx + s, cy - 4 * s),
                        (cx + 5 * s, cy - s),
                        (cx + 4 * s, cy + 2 * s),
                        (cx + 2 * s, cy - s)
                    }, head);
                    DrawToolLine(image, cx + 2 * s, cy - 3 * s, cx + 4 * s, cy, highlight, Math.Max(2, s / 2));
                    FillTriangle(image, cx + 4 * s, cy + 2 * s, cx + 5 * s, cy + 3 * s, cx + 3 * s, cy + s, headDark);
                    break;
                case "shovel":
                    DrawToolLine(image, cx, cy + 4 * s, cx, cy - s, handle, s);
                    FillPolygon(image, new[]
                    {
                        (cx, cy - 4 * s),
                        (cx - 3 * s, cy - s),
                        (cx - 2 * s, cy + s),
                        (cx + 2 * s, cy + s),
                        (cx + 3 * s, cy - s)
                    }, head);
                    DrawToolLine(image, cx - s, cy - 2 * s, cx + s, cy - 2 * s, highlight, Math.Max(2, s / 2));
                    FillRect(image, cx - 2 * s, cy, 4 * s + 1, s + 1, headDark);
                    // Central crease line shading
                    DrawToolLine(image, cx, cy - 3 * s, cx, cy + s, headDark, Math.Max(1, s / 3));
                    DrawToolLine(image, cx - 1, cy - 3 * s, cx - 1, cy + s, highlight, Math.Max(1, s / 3));
                    break;
                case "sword":
                    FillEllipse(image, cx - s, cy + 5 * s, cx + s, cy + 7 * s, headDark);
                    FillRect(image, cx - s / 2, cy + 3 * s, s + 1, 2 * s + 1, new Color(100, 70, 40));
                    FillPolygon(image, new[]
                    {
                        (cx - 3 * s, cy + s),
                        (cx - 2 * s, cy + 2 * s),
                        (cx + 2 * s, cy + 2 * s),
                        (cx + 3 * s, cy + s),
                        (cx, cy + 2 * s)
                    }, headDark);
                    FillRect(image, cx - s, cy - 4 * s, s, 5 * s, head);
                    FillRect(image, cx, cy - 4 * s, s + 1, 5 * s, headDark);
                    FillRect(image, cx - s, cy - 4 * s, 1, 5 * s, highlight);
                    FillTriangle(image, cx, cy - 5 * s, cx - s, cy - 4 * s, cx, cy - 4 * s, highlight);
                    FillTriangle(image, cx, cy - 5 * s, cx, cy - 4 * s, cx + s, cy - 4 * s, headDark);
                    break;
            }

            FillEllipse(image, cx - 4 * s, cy + 5 * s, cx + 4 * s, cy + 7 * s, new Color(0, 0, 0, 48));
            return image;
        }
    }
}

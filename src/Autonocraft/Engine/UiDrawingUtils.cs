using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.Engine
{
    public static class UiDrawingUtils
    {
        public static void DrawRoundedRect(SpriteBatch sb, Texture2D whiteTexture, float x, float y, float w, float h, float radius, Color color)
        {
            DrawRoundedRectInternal(sb, whiteTexture, x, y, w, h, radius, color);
        }

        public static void DrawRoundedRectOutline(SpriteBatch sb, Texture2D whiteTexture, float x, float y, float w, float h, float radius, Color color, float thickness, float alpha = 1f)
        {
            DrawRoundedRectOutlineInternal(sb, whiteTexture, x, y, w, h, radius, color, thickness, alpha);
        }

        public static void DrawRoundedPanel(SpriteBatch sb, Texture2D whiteTexture, float x, float y, float w, float h, Color fill, Color border, float borderAlpha = 0.8f, float alpha = 1f, float radius = UiTheme.RadiusMd)
        {
            DrawRoundedRect(sb, whiteTexture, x, y, w, h, radius, fill * alpha);
            DrawRoundedRectOutline(sb, whiteTexture, x, y, w, h, radius, border, 1f, borderAlpha * alpha);
        }

        private static void DrawRoundedRectInternal(SpriteBatch sb, Texture2D whiteTexture, float x, float y, float w, float h, float radius, Color color)
        {
            radius = Math.Clamp(radius, 0f, Math.Min(w, h) * 0.5f);
            if (radius <= 0.5f)
            {
                sb.Draw(whiteTexture, new Rectangle((int)x, (int)y, (int)w, (int)h), color);
                return;
            }

            sb.Draw(whiteTexture, new Rectangle((int)(x + radius), (int)y, (int)(w - radius * 2f), (int)h), color);
            sb.Draw(whiteTexture, new Rectangle((int)x, (int)(y + radius), (int)w, (int)(h - radius * 2f)), color);
            FillCircleCorner(sb, whiteTexture, x + radius, y + radius, radius, color, 2);
            FillCircleCorner(sb, whiteTexture, x + w - radius, y + radius, radius, color, 3);
            FillCircleCorner(sb, whiteTexture, x + radius, y + h - radius, radius, color, 1);
            FillCircleCorner(sb, whiteTexture, x + w - radius, y + h - radius, radius, color, 0);
        }

        private static void DrawRoundedRectOutlineInternal(SpriteBatch sb, Texture2D whiteTexture, float x, float y, float w, float h, float radius, Color color, float thickness, float alpha)
        {
            Color drawCol = color * alpha;
            radius = Math.Clamp(radius, 0f, Math.Min(w, h) * 0.5f);
            if (radius <= 0.5f)
            {
                DrawRectOutlineInternal(sb, whiteTexture, x, y, w, h, thickness, drawCol);
                return;
            }

            sb.Draw(whiteTexture, new Rectangle((int)(x + radius), (int)y, (int)(w - radius * 2f), (int)thickness), drawCol);
            sb.Draw(whiteTexture, new Rectangle((int)(x + radius), (int)(y + h - thickness), (int)(w - radius * 2f), (int)thickness), drawCol);
            sb.Draw(whiteTexture, new Rectangle((int)x, (int)(y + radius), (int)thickness, (int)(h - radius * 2f)), drawCol);
            sb.Draw(whiteTexture, new Rectangle((int)(x + w - thickness), (int)(y + radius), (int)thickness, (int)(h - radius * 2f)), drawCol);

            DrawCornerArcOutline(sb, whiteTexture, x + radius, y + radius, radius, thickness, drawCol, 2);
            DrawCornerArcOutline(sb, whiteTexture, x + w - radius, y + radius, radius, thickness, drawCol, 1);
            DrawCornerArcOutline(sb, whiteTexture, x + radius, y + h - radius, radius, thickness, drawCol, 3);
            DrawCornerArcOutline(sb, whiteTexture, x + w - radius, y + h - radius, radius, thickness, drawCol, 0);
        }

        private static void FillCircleCorner(SpriteBatch sb, Texture2D whiteTexture, float cx, float cy, float radius, Color color, int quadrant)
        {
            int r = (int)MathF.Ceiling(radius);
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > radius * radius) continue;
                    bool inQuad = quadrant switch
                    {
                        0 => dx >= 0 && dy >= 0,
                        1 => dx <= 0 && dy >= 0,
                        2 => dx <= 0 && dy <= 0,
                        3 => dx >= 0 && dy <= 0,
                        _ => true
                    };
                    if (!inQuad) continue;
                    sb.Draw(whiteTexture, new Rectangle((int)(cx + dx), (int)(cy + dy), 1, 1), color);
                }
            }
        }

        private static void DrawCornerArcOutline(SpriteBatch sb, Texture2D whiteTexture, float cx, float cy, float radius, float thickness, Color color, int quadrant)
        {
            int r = (int)MathF.Ceiling(radius);
            float inner = Math.Max(0f, radius - thickness);
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > radius || dist < inner) continue;
                    bool inQuad = quadrant switch
                    {
                        0 => dx >= 0 && dy >= 0,
                        1 => dx <= 0 && dy >= 0,
                        2 => dx <= 0 && dy <= 0,
                        3 => dx >= 0 && dy <= 0,
                        _ => true
                    };
                    if (!inQuad) continue;
                    sb.Draw(whiteTexture, new Rectangle((int)(cx + dx), (int)(cy + dy), 1, 1), color);
                }
            }
        }

        private static void DrawRectOutlineInternal(SpriteBatch sb, Texture2D whiteTexture, float x, float y, float w, float h, float thickness, Color color)
        {
            sb.Draw(whiteTexture, new Rectangle((int)x, (int)y, (int)w, (int)thickness), color);
            sb.Draw(whiteTexture, new Rectangle((int)x, (int)(y + h - thickness), (int)w, (int)thickness), color);
            sb.Draw(whiteTexture, new Rectangle((int)x, (int)(y + thickness), (int)thickness, (int)(h - 2 * thickness)), color);
            sb.Draw(whiteTexture, new Rectangle((int)(x + w - thickness), (int)(y + thickness), (int)thickness, (int)(h - 2 * thickness)), color);
        }
    }
}

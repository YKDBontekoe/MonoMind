using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Engine.Animation;

namespace Autonocraft.Engine
{
    public class UiRenderer : IDisposable
    {
        private static readonly Dictionary<char, byte[]> Font = new Dictionary<char, byte[]>
        {
            ['0'] = new byte[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E },
            ['1'] = new byte[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E },
            ['2'] = new byte[] { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F },
            ['3'] = new byte[] { 0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E },
            ['4'] = new byte[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 },
            ['5'] = new byte[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E },
            ['6'] = new byte[] { 0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E },
            ['7'] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 },
            ['8'] = new byte[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E },
            ['9'] = new byte[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C },
            [':'] = new byte[] { 0x00, 0x04, 0x00, 0x00, 0x00, 0x04, 0x00 },
            ['.'] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C },
            ['-'] = new byte[] { 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00 },
            ['/'] = new byte[] { 0x01, 0x02, 0x04, 0x08, 0x10, 0x10, 0x10 },
            ['A'] = new byte[] { 0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
            ['B'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E },
            ['C'] = new byte[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E },
            ['D'] = new byte[] { 0x1C, 0x12, 0x11, 0x11, 0x11, 0x12, 0x1C },
            ['E'] = new byte[] { 0x1F, 0x10, 0x10, 0x1C, 0x10, 0x10, 0x1F },
            ['F'] = new byte[] { 0x1F, 0x10, 0x10, 0x1C, 0x10, 0x10, 0x10 },
            ['G'] = new byte[] { 0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F },
            ['H'] = new byte[] { 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
            ['I'] = new byte[] { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E },
            ['J'] = new byte[] { 0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C },
            ['K'] = new byte[] { 0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11 },
            ['L'] = new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F },
            ['M'] = new byte[] { 0x11, 0x1B, 0x15, 0x11, 0x11, 0x11, 0x11 },
            ['N'] = new byte[] { 0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11 },
            ['O'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
            ['P'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 },
            ['Q'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D },
            ['R'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11 },
            ['S'] = new byte[] { 0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E },
            ['T'] = new byte[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 },
            ['U'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
            ['V'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04 },
            ['W'] = new byte[] { 0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11 },
            ['X'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11 },
            ['Y'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04 },
            ['Z'] = new byte[] { 0x1F, 0x02, 0x04, 0x08, 0x10, 0x10, 0x1F },
            [' '] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
        };

        private readonly GraphicsDevice _device;
        private readonly Texture2D _whiteTexture;
        private readonly SpriteBatch _spriteBatch;

        public UiRenderer(GraphicsDevice device, Texture2D whiteTexture)
        {
            _device = device;
            _whiteTexture = whiteTexture;
            _spriteBatch = new SpriteBatch(device);
        }

        public void DrawFullscreenBackground(Color color)
        {
            int w = _device.Viewport.Width;
            int h = _device.Viewport.Height;
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            _spriteBatch.Draw(_whiteTexture, new Rectangle(0, 0, w, h), color);
            _spriteBatch.End();
        }

        public void DrawPanel(float x, float y, float w, float h, Color fill, Color border, float borderAlpha = 0.8f, float alpha = 1f)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)x, (int)y, (int)w, (int)h), fill * alpha);
            DrawRectOutlineInternal(x, y, w, h, 1f, border, borderAlpha * alpha);
            _spriteBatch.End();
        }

        public void DrawButton(float x, float y, float w, float h, string label, bool hovered, bool pressed, float textPixelSize = 1.6f, float alpha = 1f, float hoverT = 1f)
        {
            float hoverBlend = Math.Clamp(hoverT, 0f, 1f);
            Color baseFill = new Color(0.06f, 0.08f, 0.12f) * 0.90f;
            Color hoverFill = new Color(0.10f, 0.16f, 0.24f) * 0.92f;
            Color pressedFill = new Color(0.12f, 0.22f, 0.32f) * 0.95f;

            Color fill = pressed
                ? pressedFill
                : Color.Lerp(baseFill, hoverFill, hoverBlend);

            Color baseBorder = new Color(0.2f, 0.3f, 0.4f);
            Color hoverBorder = new Color(0.0f, 0.8f, 1.0f);
            Color border = Color.Lerp(baseBorder, hoverBorder, hoverBlend);
            float borderAlpha = 0.7f + 0.3f * hoverBlend;
            float borderThickness = 1f + hoverBlend;

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)x, (int)y, (int)w, (int)h), fill * alpha);
            DrawRectOutlineInternal(x, y, w, h, borderThickness, border, borderAlpha * alpha);

            float textWidth = MeasureString(label, textPixelSize);
            float textX = x + (w - textWidth) / 2f;
            float textY = y + (h - 7f * textPixelSize) / 2f;
            Color textColor = Color.Lerp(new Color(0.85f, 0.88f, 0.92f), Color.White, hoverBlend);
            DrawStringInternal(label, textX, textY, textPixelSize, textColor, alpha);
            _spriteBatch.End();
        }

        public void DrawCenteredText(string text, float centerY, float pixelSize, Color color, float alpha = 1f)
        {
            float textWidth = MeasureString(text, pixelSize);
            float x = (_device.Viewport.Width - textWidth) / 2f;

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            DrawStringInternal(text, x, centerY, pixelSize, color, alpha);
            _spriteBatch.End();
        }

        public void DrawProgressBar(float x, float y, float w, float h, float progress, string label, float textScale = 1f, float alpha = 1f)
        {
            progress = Math.Clamp(progress, 0f, 1f);
            float labelSize = 1.3f * textScale;
            float pctSize = 1.2f * textScale;

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(x - 2), (int)(y - 2), (int)(w + 4), (int)(h + 4)), Color.Black * 0.7f * alpha);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)x, (int)y, (int)w, (int)h), new Color(0.08f, 0.10f, 0.14f) * 0.95f * alpha);

            if (progress > 0.01f)
            {
                _spriteBatch.Draw(_whiteTexture, new Rectangle((int)x, (int)y, (int)(w * progress), (int)h), new Color(0.2f, 0.75f, 1.0f) * alpha);
            }

            DrawRectOutlineInternal(x, y, w, h, 1f, new Color(0.2f, 0.3f, 0.4f), 0.8f * alpha);

            float labelWidth = MeasureString(label, labelSize);
            DrawStringInternal(label, x + (w - labelWidth) / 2f, y - 22f * textScale, labelSize, new Color(0.8f, 0.9f, 1.0f), alpha);

            int pct = (int)MathF.Round(progress * 100f);
            string pctText = $"{pct}%";
            float pctWidth = MeasureString(pctText, pctSize);
            DrawStringInternal(pctText, x + (w - pctWidth) / 2f, y + h + 8f * textScale, pctSize, new Color(0.7f, 0.75f, 0.8f), alpha);

            _spriteBatch.End();
        }

        public float MeasureString(string text, float pixelSize)
        {
            return text.Length * 6f * pixelSize;
        }

        public void DrawString(string text, float x, float y, float pixelSize, Color color, float alpha = 1f)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            DrawStringInternal(text, x, y, pixelSize, color, alpha);
            _spriteBatch.End();
        }

        public void DrawFilledRect(float x, float y, float w, float h, Color color)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)x, (int)y, (int)w, (int)h), color);
            _spriteBatch.End();
        }

        public void DrawBatch(Action<SpriteBatch, Texture2D> drawAction)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            drawAction(_spriteBatch, _whiteTexture);
            _spriteBatch.End();
        }

        public void DrawVignette(float strength = 0.5f, float alpha = 1f)
        {
            int w = _device.Viewport.Width;
            int h = _device.Viewport.Height;
            const int bands = 14;
            DrawBatch((batch, tex) =>
            {
                float bandW = w / (float)bands;
                for (int i = 0; i < bands; i++)
                {
                    float t = i / (float)(bands - 1);
                    float edge = MathF.Abs(t - 0.5f) * 2f;
                    float a = edge * edge * strength * alpha;
                    if (a < 0.01f) continue;
                    batch.Draw(tex, new Rectangle((int)(i * bandW), 0, (int)(bandW + 1f), h), Color.Black * a);
                }

                float bandH = h / (float)bands;
                for (int i = 0; i < bands; i++)
                {
                    float t = i / (float)(bands - 1);
                    float edge = MathF.Abs(t - 0.5f) * 2f;
                    float a = edge * edge * (strength * 0.65f) * alpha;
                    if (a < 0.01f) continue;
                    batch.Draw(tex, new Rectangle(0, (int)(i * bandH), w, (int)(bandH + 1f)), Color.Black * a);
                }
            });
        }

        public void DrawSoftGlow(float x, float y, float w, float h, Color color, float alpha = 1f, int layers = 4)
        {
            DrawBatch((batch, tex) =>
            {
                for (int i = layers; i >= 1; i--)
                {
                    float expand = i * 3f;
                    float layerAlpha = alpha * (0.08f / i);
                    batch.Draw(
                        tex,
                        new Rectangle((int)(x - expand), (int)(y - expand), (int)(w + expand * 2f), (int)(h + expand * 2f)),
                        color * layerAlpha);
                }
            });
        }

        public void DrawCornerAccents(float x, float y, float w, float h, Color color, float length, float thickness, float alpha = 1f)
        {
            DrawBatch((batch, tex) =>
            {
                Color drawCol = color * alpha;
                batch.Draw(tex, new Rectangle((int)x, (int)y, (int)length, (int)thickness), drawCol);
                batch.Draw(tex, new Rectangle((int)x, (int)y, (int)thickness, (int)length), drawCol);
                batch.Draw(tex, new Rectangle((int)(x + w - length), (int)y, (int)length, (int)thickness), drawCol);
                batch.Draw(tex, new Rectangle((int)(x + w - thickness), (int)y, (int)thickness, (int)length), drawCol);
                batch.Draw(tex, new Rectangle((int)x, (int)(y + h - thickness), (int)length, (int)thickness), drawCol);
                batch.Draw(tex, new Rectangle((int)x, (int)(y + h - length), (int)thickness, (int)length), drawCol);
                batch.Draw(tex, new Rectangle((int)(x + w - length), (int)(y + h - thickness), (int)length, (int)thickness), drawCol);
                batch.Draw(tex, new Rectangle((int)(x + w - thickness), (int)(y + h - length), (int)thickness, (int)length), drawCol);
            });
        }

        public void DrawHorizontalRule(float x, float y, float w, Color color, float thickness = 1f, float alpha = 1f)
        {
            DrawFilledRect(x, y, w, thickness, color * alpha);
        }

        public void DrawFramedPanel(float x, float y, float w, float h, Color fill, Color border, float alpha = 1f)
        {
            DrawSoftGlow(x, y, w, h, border, alpha * 0.55f, 3);
            DrawPanel(x, y, w, h, fill, border, 0.9f, alpha);
            DrawCornerAccents(x, y, w, h, border, Math.Min(18f, w * 0.06f), 2f, alpha * 0.95f);
        }

        public void DrawCenteredTitle(string text, float centerY, float pixelSize, Color color, float alpha = 1f)
        {
            float textWidth = MeasureString(text, pixelSize);
            float x = (_device.Viewport.Width - textWidth) / 2f;
            float shadowOffset = Math.Max(1f, pixelSize * 0.8f);

            DrawBatch((batch, tex) =>
            {
                DrawStringInternal(text, x + shadowOffset, centerY + shadowOffset, pixelSize, Color.Black * (0.45f * alpha), alpha, batch, tex);
                DrawStringInternal(text, x, centerY, pixelSize, color, alpha, batch, tex);
            });
        }

        public void DrawIntSlider(
            float x,
            float y,
            float width,
            float trackHeight,
            float thumbSize,
            int min,
            int max,
            int value,
            bool hovered,
            bool dragging)
        {
            value = Math.Clamp(value, min, max);
            float range = Math.Max(1, max - min);
            float t = (value - min) / range;
            float thumbX = x + t * Math.Max(1f, width - thumbSize);

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

            _spriteBatch.Draw(
                _whiteTexture,
                new Rectangle((int)(x - 2f), (int)(y - 2f), (int)(width + 4f), (int)(trackHeight + 4f)),
                Color.Black * 0.55f);
            _spriteBatch.Draw(
                _whiteTexture,
                new Rectangle((int)x, (int)y, (int)width, (int)trackHeight),
                new Color(0.08f, 0.10f, 0.14f) * 0.95f);

            if (t > 0.01f)
            {
                float fillWidth = thumbX + thumbSize * 0.5f - x;
                _spriteBatch.Draw(
                    _whiteTexture,
                    new Rectangle((int)x, (int)y, (int)Math.Max(trackHeight, fillWidth), (int)trackHeight),
                    new Color(0.18f, 0.55f, 0.82f));
            }

            Color thumbColor = dragging
                ? new Color(0.35f, 0.85f, 1.0f)
                : hovered
                    ? new Color(0.28f, 0.72f, 0.96f)
                    : new Color(0.22f, 0.62f, 0.88f);
            _spriteBatch.Draw(
                _whiteTexture,
                new Rectangle((int)thumbX, (int)(y - (thumbSize - trackHeight) * 0.5f), (int)thumbSize, (int)thumbSize),
                thumbColor);
            DrawRectOutlineInternal(x, y, width, trackHeight, 1f, new Color(0.2f, 0.3f, 0.4f), 0.85f);
            DrawRectOutlineInternal(thumbX, y - (thumbSize - trackHeight) * 0.5f, thumbSize, thumbSize, hovered || dragging ? 2f : 1f, new Color(0.45f, 0.75f, 0.95f), 0.95f);

            _spriteBatch.End();
        }

        public static int GetSliderValueFromPosition(float x, float width, float thumbSize, int min, int max, float mouseX)
        {
            float range = Math.Max(1, max - min);
            float usableWidth = Math.Max(1f, width - thumbSize);
            float t = Math.Clamp((mouseX - x - thumbSize * 0.5f) / usableWidth, 0f, 1f);
            return min + (int)MathF.Round(t * range);
        }

        public static Rectangle GetSliderTrackRect(float x, float y, float width, float trackHeight, float thumbSize)
        {
            float paddingY = Math.Max(0f, (thumbSize - trackHeight) * 0.5f);
            return new Rectangle(
                (int)x,
                (int)(y - paddingY),
                (int)width,
                (int)(trackHeight + paddingY * 2f));
        }

        private void DrawRectOutlineInternal(float x, float y, float w, float h, float thickness, Color color, float alpha)
        {
            Color drawCol = color * alpha;
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)x, (int)y, (int)w, (int)thickness), drawCol);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)x, (int)(y + h - thickness), (int)w, (int)thickness), drawCol);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)x, (int)(y + thickness), (int)thickness, (int)(h - 2 * thickness)), drawCol);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(x + w - thickness), (int)(y + thickness), (int)thickness, (int)(h - 2 * thickness)), drawCol);
        }

        private void DrawStringInternal(string text, float startX, float startY, float pixelSize, Color color, float alpha)
        {
            DrawStringInternal(text, startX, startY, pixelSize, color, alpha, _spriteBatch, _whiteTexture);
        }

        private static void DrawStringInternal(string text, float startX, float startY, float pixelSize, Color color, float alpha, SpriteBatch batch, Texture2D tex)
        {
            float curX = startX;
            Color drawCol = color * alpha;
            foreach (char c in text.ToUpperInvariant())
            {
                char lookup = Font.ContainsKey(c) ? c : ' ';
                byte[] rows = Font[lookup];

                for (int r = 0; r < 7; r++)
                {
                    byte rowVal = rows[r];
                    for (int col = 0; col < 5; col++)
                    {
                        if (((rowVal >> (4 - col)) & 1) == 1)
                        {
                            int px = (int)(curX + col * pixelSize);
                            int py = (int)(startY + r * pixelSize);
                            int sz = (int)Math.Max(1f, pixelSize);
                            batch.Draw(tex, new Rectangle(px, py, sz, sz), drawCol);
                        }
                    }
                }
                curX += 6f * pixelSize;
            }
        }

        public void Dispose()
        {
            _spriteBatch.Dispose();
        }
    }
}

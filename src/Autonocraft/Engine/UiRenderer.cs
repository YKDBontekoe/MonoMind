using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Engine.Animation;

namespace Autonocraft.Engine
{
    public class UiRenderer : IDisposable
    {
        private static readonly RasterizerState ScissorOn = new();
        private static readonly BlendState AlphaBlend = BlendState.AlphaBlend;
        private static readonly SamplerState PointClamp = SamplerState.PointClamp;

        private readonly GraphicsDevice _device;
        private readonly Texture2D _whiteTexture;
        private readonly SpriteBatch _spriteBatch;
        private readonly UiTypography _typography;
        private WorldThumbnailRenderer? _worldThumbnails;

        public UiRenderer(GraphicsDevice device, Texture2D whiteTexture, UiTypography typography)
        {
            _device = device;
            _whiteTexture = whiteTexture;
            _typography = typography;
            _spriteBatch = new SpriteBatch(device);
        }

        public UiTypography Typography => _typography;

        public GraphicsDevice Device => _device;

        public WorldThumbnailRenderer WorldThumbnails => _worldThumbnails ??= new WorldThumbnailRenderer(_device);

        public void DrawAtlasTile(Texture2D atlas, Rectangle dest, Rectangle source, float alpha = 1f)
        {
            BeginBatch();
            _spriteBatch.Draw(atlas, dest, source, Color.White * alpha);
            EndBatch();
        }

        public void DrawFullscreenBackground(Color color)
        {
            int w = _device.Viewport.Width;
            int h = _device.Viewport.Height;
            BeginBatch();
            _spriteBatch.Draw(_whiteTexture, new Rectangle(0, 0, w, h), color);
            EndBatch();
        }

        public void DrawCard(float x, float y, float w, float h, float alpha = 1f, float radius = UiTheme.RadiusLg, bool shadow = true)
        {
            if (shadow)
            {
                DrawRoundedRect(x, y + UiTheme.ShadowOffset, w, h, radius, new Color(0.12f, 0.14f, 0.18f) * (UiTheme.ShadowAlpha * alpha));
            }

            DrawRoundedRect(x, y, w, h, radius, UiTheme.PanelFill * alpha);
            DrawRoundedRectOutline(x, y, w, h, radius, UiTheme.PanelBorder, 1f, 0.75f * alpha);
        }

        public void DrawPanel(float x, float y, float w, float h, Color fill, Color border, float borderAlpha = 0.8f, float alpha = 1f, float radius = UiTheme.RadiusMd)
        {
            DrawRoundedRect(x, y, w, h, radius, fill * alpha);
            DrawRoundedRectOutline(x, y, w, h, radius, border, 1f, borderAlpha * alpha);
        }

        public void DrawFramedPanel(float x, float y, float w, float h, Color fill, Color border, float alpha = 1f, float radius = UiTheme.RadiusLg)
        {
            DrawRoundedRect(x, y + 2f, w, h, radius, new Color(0.12f, 0.14f, 0.18f) * (0.06f * alpha));
            DrawCard(x, y, w, h, alpha, radius, shadow: false);
            DrawRoundedRectOutline(x, y, w, h, radius, border, 1f, 0.7f * alpha);
        }

        public void DrawButton(
            float x,
            float y,
            float w,
            float h,
            string label,
            bool hovered,
            bool pressed,
            float fontSize,
            float alpha = 1f,
            float hoverT = 1f,
            bool disabled = false)
        {
            DrawButton(x, y, w, h, label, hovered, pressed, UiButtonStyle.Secondary, fontSize, alpha, hoverT, disabled);
        }

        public void DrawButton(
            float x,
            float y,
            float w,
            float h,
            string label,
            bool hovered,
            bool pressed,
            UiButtonStyle style = UiButtonStyle.Secondary,
            float fontSize = UiTheme.FontBody,
            float alpha = 1f,
            float hoverT = 1f,
            bool disabled = false)
        {
            float hoverBlend = Math.Clamp(hoverT, 0f, 1f);
            float radius = Math.Min(UiTheme.RadiusMd, h * 0.35f);

            Color fill;
            Color border;
            Color textColor;
            float borderThickness = 1f;

            if (disabled)
            {
                fill = UiTheme.PanelBgMuted * 0.9f;
                border = UiTheme.PanelBorder;
                textColor = UiTheme.Meta;
                alpha *= 0.55f;
                hoverBlend = 0f;
            }
            else
            {
                switch (style)
                {
                    case UiButtonStyle.Primary:
                        fill = Color.Lerp(UiTheme.Accent, UiTheme.AccentHover, pressed ? 0.35f : hoverBlend * 0.25f);
                        border = fill;
                        textColor = UiTheme.ButtonPrimaryText;
                        borderThickness = 0f;
                        break;
                    case UiButtonStyle.Danger:
                        fill = Color.Lerp(UiTheme.DangerSoft, UiTheme.Danger * 0.15f, hoverBlend);
                        border = Color.Lerp(UiTheme.Danger * 0.45f, UiTheme.Danger, hoverBlend);
                        textColor = UiTheme.ButtonDangerText;
                        break;
                    case UiButtonStyle.Ghost:
                        fill = Color.Lerp(Color.Transparent, UiTheme.PanelBgAccent, hoverBlend * 0.65f);
                        border = Color.Transparent;
                        textColor = Color.Lerp(UiTheme.ButtonGhostText, UiTheme.Accent, hoverBlend * 0.5f);
                        borderThickness = 0f;
                        break;
                    default:
                        fill = Color.Lerp(UiTheme.PanelFill, UiTheme.AccentSoft, hoverBlend * 0.85f);
                        border = Color.Lerp(UiTheme.PanelBorder, UiTheme.Accent * 0.55f, hoverBlend);
                        textColor = Color.Lerp(UiTheme.ButtonSecondaryText, UiTheme.Accent, hoverBlend * 0.55f);
                        break;
                }
            }

            BeginBatch();
            if (style == UiButtonStyle.Primary && hoverBlend > 0.01f && !disabled)
            {
                DrawRoundedRectInternal(x - 2f, y - 2f, w + 4f, h + 4f, radius + 2f, UiTheme.AccentGlow * (0.18f * hoverBlend * alpha));
            }

            if (style != UiButtonStyle.Ghost && borderThickness > 0f)
            {
                DrawRoundedRectInternal(x, y + 1.5f, w, h, radius, new Color(0.12f, 0.14f, 0.18f) * (0.05f * alpha));
            }

            DrawRoundedRectInternal(x, y, w, h, radius, fill * alpha);
            if (borderThickness > 0f && border.A > 0)
            {
                DrawRoundedRectOutlineInternal(x, y, w, h, radius, border, borderThickness, alpha);
            }

            float textWidth = MeasureString(label, fontSize);
            float textX = x + (w - textWidth) / 2f;
            float textY = y + (h - fontSize) / 2f - 1f;
            bool semiBold = style == UiButtonStyle.Primary || style == UiButtonStyle.Danger;
            _typography.Draw(_spriteBatch, label, textX, textY, fontSize, textColor, semiBold, alpha);
            EndBatch();
        }

        public void DrawCenteredText(string text, float centerY, float fontSize, Color color, float alpha = 1f, bool semiBold = false)
        {
            float textWidth = MeasureString(text, fontSize, semiBold);
            float x = (_device.Viewport.Width - textWidth) / 2f;
            DrawLabel(text, x, centerY, fontSize, color, semiBold, alpha);
        }

        public void DrawCenteredTitle(string text, float centerY, float fontSize, Color color, float alpha = 1f)
        {
            float textWidth = MeasureString(text, fontSize, semiBold: true);
            float x = (_device.Viewport.Width - textWidth) / 2f;
            DrawLabel(text, x, centerY, fontSize, color, semiBold: true, alpha);
        }

        public void DrawLabel(string text, float x, float y, float fontSize, Color color, bool semiBold = false, float alpha = 1f)
        {
            BeginBatch();
            _typography.Draw(_spriteBatch, text, x, y, fontSize, color, semiBold, alpha);
            EndBatch();
        }

        public void DrawString(string text, float x, float y, float fontSize, Color color, float alpha = 1f, bool semiBold = false)
        {
            DrawLabel(text, x, y, fontSize, color, semiBold, alpha);
        }

        public float MeasureString(string text, float fontSize, bool semiBold = false)
        {
            return _typography.Measure(text, fontSize, semiBold);
        }

        public void DrawProgressBar(float x, float y, float w, float h, float progress, string label, float textScale = 1f, float alpha = 1f)
        {
            progress = Math.Clamp(progress, 0f, 1f);
            float labelSize = UiTheme.FontSmall * textScale;
            float pctSize = UiTheme.FontCaption * textScale;
            float radius = Math.Min(UiTheme.RadiusSm, h * 0.45f);

            BeginBatch();
            DrawRoundedRectInternal(x, y, w, h, radius, UiTheme.ProgressTrack * alpha);
            if (progress > 0.01f)
            {
                float fillW = Math.Max(radius * 2f, w * progress);
                DrawRoundedRectInternal(x, y, fillW, h, radius, UiTheme.ProgressFill * alpha);
            }

            DrawRoundedRectOutlineInternal(x, y, w, h, radius, UiTheme.PanelBorder, 1f, 0.6f * alpha);

            float labelWidth = _typography.Measure(label, labelSize);
            _typography.Draw(_spriteBatch, label, x + (w - labelWidth) / 2f, y - 24f * textScale, labelSize, UiTheme.ProgressLabel, alpha: alpha);

            int pct = (int)MathF.Round(progress * 100f);
            string pctText = $"{pct}%";
            float pctWidth = _typography.Measure(pctText, pctSize);
            _typography.Draw(_spriteBatch, pctText, x + (w - pctWidth) / 2f, y + h + 6f * textScale, pctSize, UiTheme.ProgressPercent, alpha: alpha);
            EndBatch();
        }

        public void DrawFilledRect(float x, float y, float w, float h, Color color)
        {
            BeginBatch();
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)x, (int)y, (int)w, (int)h), color);
            EndBatch();
        }

        public void DrawTexture(Texture2D texture, float x, float y, float w, float h, float alpha = 1f)
        {
            BeginBatch();
            _spriteBatch.Draw(texture, new Rectangle((int)x, (int)y, (int)w, (int)h), Color.White * alpha);
            EndBatch();
        }

        public void DrawThumbnailFrame(Texture2D texture, float x, float y, float size, float alpha = 1f, float radius = UiTheme.RadiusMd)
        {
            DrawTexture(texture, x, y, size, size, alpha);
            DrawRoundedRectOutline(x, y, size, size, radius, UiTheme.PanelBorder, 1.5f, alpha);
        }

        public void DrawRoundedRect(float x, float y, float w, float h, float radius, Color color)
        {
            BeginBatch();
            DrawRoundedRectInternal(x, y, w, h, radius, color);
            EndBatch();
        }

        public void DrawRoundedRectOutline(float x, float y, float w, float h, float radius, Color color, float thickness, float alpha = 1f)
        {
            BeginBatch();
            DrawRoundedRectOutlineInternal(x, y, w, h, radius, color, thickness, alpha);
            EndBatch();
        }

        public void DrawBatch(Action<SpriteBatch, Texture2D> drawAction)
        {
            BeginBatch();
            drawAction(_spriteBatch, _whiteTexture);
            EndBatch();
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
                    float expand = i * 4f;
                    float layerAlpha = alpha * (0.06f / i);
                    batch.Draw(
                        tex,
                        new Rectangle((int)(x - expand), (int)(y - expand), (int)(w + expand * 2f), (int)(h + expand * 2f)),
                        color * layerAlpha);
                }
            });
        }

        public void DrawHorizontalRule(float x, float y, float w, Color color, float thickness = 1f, float alpha = 1f)
        {
            DrawFilledRect(x, y, w, thickness, color * alpha);
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
            float trackRadius = trackHeight * 0.5f;
            float thumbRadius = thumbSize * 0.5f;

            BeginBatch();
            DrawRoundedRectInternal(x, y, width, trackHeight, trackRadius, UiTheme.ProgressTrack);
            if (t > 0.01f)
            {
                float fillWidth = thumbX + thumbSize * 0.5f - x;
                DrawRoundedRectInternal(x, y, Math.Max(trackHeight, fillWidth), trackHeight, trackRadius, UiTheme.ProgressFill);
            }

            Color thumbColor = dragging
                ? UiTheme.SliderThumbActive
                : hovered
                    ? UiTheme.SliderThumbHover
                    : UiTheme.SliderThumb;
            float thumbY = y - (thumbSize - trackHeight) * 0.5f;
            DrawRoundedRectInternal(thumbX, thumbY, thumbSize, thumbSize, thumbRadius, thumbColor);
            DrawRoundedRectOutlineInternal(x, y, width, trackHeight, trackRadius, UiTheme.PanelBorder, 1f, 0.55f);
            EndBatch();
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

        private void BeginBatch()
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, AlphaBlend, PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        }

        private void EndBatch()
        {
            _spriteBatch.End();
        }

        private void DrawRoundedRectInternal(float x, float y, float w, float h, float radius, Color color)
        {
            radius = Math.Clamp(radius, 0f, Math.Min(w, h) * 0.5f);
            if (radius <= 0.5f)
            {
                _spriteBatch.Draw(_whiteTexture, new Rectangle((int)x, (int)y, (int)w, (int)h), color);
                return;
            }

            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(x + radius), (int)y, (int)(w - radius * 2f), (int)h), color);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)x, (int)(y + radius), (int)w, (int)(h - radius * 2f)), color);
            FillCircleCorner(x + radius, y + radius, radius, color, 2);
            FillCircleCorner(x + w - radius, y + radius, radius, color, 3);
            FillCircleCorner(x + radius, y + h - radius, radius, color, 1);
            FillCircleCorner(x + w - radius, y + h - radius, radius, color, 0);
        }

        private void DrawRoundedRectOutlineInternal(float x, float y, float w, float h, float radius, Color color, float thickness, float alpha)
        {
            Color drawCol = color * alpha;
            radius = Math.Clamp(radius, 0f, Math.Min(w, h) * 0.5f);
            if (radius <= 0.5f)
            {
                DrawRectOutlineInternal(x, y, w, h, thickness, drawCol);
                return;
            }

            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(x + radius), (int)y, (int)(w - radius * 2f), (int)thickness), drawCol);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(x + radius), (int)(y + h - thickness), (int)(w - radius * 2f), (int)thickness), drawCol);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)x, (int)(y + radius), (int)thickness, (int)(h - radius * 2f)), drawCol);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(x + w - thickness), (int)(y + radius), (int)thickness, (int)(h - radius * 2f)), drawCol);

            DrawCornerArcOutline(x + radius, y + radius, radius, thickness, drawCol, 2);
            DrawCornerArcOutline(x + w - radius, y + radius, radius, thickness, drawCol, 1);
            DrawCornerArcOutline(x + radius, y + h - radius, radius, thickness, drawCol, 3);
            DrawCornerArcOutline(x + w - radius, y + h - radius, radius, thickness, drawCol, 0);
        }

        private void FillCircleCorner(float cx, float cy, float radius, Color color, int quadrant)
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
                    _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(cx + dx), (int)(cy + dy), 1, 1), color);
                }
            }
        }

        private void DrawCornerArcOutline(float cx, float cy, float radius, float thickness, Color color, int quadrant)
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
                    _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(cx + dx), (int)(cy + dy), 1, 1), color);
                }
            }
        }

        private void DrawRectOutlineInternal(float x, float y, float w, float h, float thickness, Color color)
        {
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)x, (int)y, (int)w, (int)thickness), color);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)x, (int)(y + h - thickness), (int)w, (int)thickness), color);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)x, (int)(y + thickness), (int)thickness, (int)(h - 2 * thickness)), color);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(x + w - thickness), (int)(y + thickness), (int)thickness, (int)(h - 2 * thickness)), color);
        }

        public void Dispose()
        {
            _worldThumbnails?.Dispose();
            _typography.Dispose();
            _spriteBatch.Dispose();
        }
    }
}

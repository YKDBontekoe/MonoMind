using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.Engine
{
    public sealed class HudToast
    {
        private string _message = string.Empty;
        private Color _color = UiTheme.HudTextPrimary;
        private float _timer;
        private float _duration;

        public string CurrentMessage => _timer > 0f ? _message : string.Empty;
        public bool IsVisible => _timer > 0f && !string.IsNullOrEmpty(_message);

        public void Show(string message, Color? color = null, float durationSeconds = 3f)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _message = message.Trim();
            _color = color ?? UiTheme.HudTextPrimary;
            _duration = Math.Max(0.5f, durationSeconds);
            _timer = _duration;
        }

        public void Clear()
        {
            _timer = 0f;
            _message = string.Empty;
        }

        public void Update(float deltaTime)
        {
            if (_timer > 0f)
            {
                _timer = Math.Max(0f, _timer - deltaTime);
            }
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D white, UiLayout layout, float hotbarPlateY)
        {
            if (_timer <= 0f || string.IsNullOrEmpty(_message))
            {
                return;
            }

            float fadeIn = Math.Clamp((_duration - _timer) / 0.15f, 0f, 1f);
            float fadeOut = Math.Clamp(_timer / 0.45f, 0f, 1f);
            float alpha = Math.Min(fadeIn, fadeOut);

            float textSize = layout.S(1.05f);
            float textW = PixelFont.MeasureString(_message, textSize);
            float padX = layout.S(18f);
            float padY = layout.S(8f);
            float maxPanelW = Math.Max(layout.S(180f), layout.Width - layout.Padding * 2f);
            if (textW + padX * 2f > maxPanelW)
            {
                textSize *= (maxPanelW - padX * 2f) / Math.Max(1f, textW);
                textW = PixelFont.MeasureString(_message, textSize);
            }

            float panelW = textW + padX * 2f;
            float panelH = layout.S(28f);
            float panelX = layout.CenterX - panelW / 2f;
            float panelY = hotbarPlateY - panelH - layout.S(10f);

            DrawGlassPanel(spriteBatch, white, panelX, panelY, panelW, panelH, _color, alpha * UiTheme.HudGlassAlpha);
            PixelFont.DrawString(
                spriteBatch,
                white,
                _message,
                panelX + padX,
                panelY + padY,
                textSize,
                _color * (0.95f * alpha),
                alpha);
        }

        private static void DrawGlassPanel(
            SpriteBatch spriteBatch,
            Texture2D white,
            float x,
            float y,
            float w,
            float h,
            Color accent,
            float alpha)
        {
            spriteBatch.Draw(white, new Rectangle((int)(x + 1f), (int)(y + 2f), (int)w, (int)h), Color.Black * (0.22f * alpha));
            spriteBatch.Draw(white, new Rectangle((int)x, (int)y, (int)w, (int)h), UiTheme.HudGlassFill * (0.92f * alpha));
            spriteBatch.Draw(
                white,
                new Rectangle((int)x, (int)y, (int)w, (int)Math.Max(1f, h * 0.06f)),
                accent * (0.75f * alpha));
            DrawOutline(spriteBatch, white, x, y, w, h, UiTheme.HudGlassBorder, 0.60f * alpha);
        }

        private static void DrawOutline(SpriteBatch sb, Texture2D white, float x, float y, float w, float h, Color color, float alpha)
        {
            Color drawCol = color * alpha;
            sb.Draw(white, new Rectangle((int)x, (int)y, (int)w, 1), drawCol);
            sb.Draw(white, new Rectangle((int)x, (int)(y + h - 1f), (int)w, 1), drawCol);
            sb.Draw(white, new Rectangle((int)x, (int)(y + 1f), 1, (int)(h - 2f)), drawCol);
            sb.Draw(white, new Rectangle((int)(x + w - 1f), (int)(y + 1f), 1, (int)(h - 2f)), drawCol);
        }
    }
}

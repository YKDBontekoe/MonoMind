using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.Engine
{
    public sealed class HudToast
    {
        private string _message = string.Empty;
        private Color _color = Color.White;
        private float _timer;
        private float _duration;

        public void Show(string message, Color? color = null, float durationSeconds = 3f)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _message = message.ToUpperInvariant();
            _color = color ?? new Color(0.95f, 0.92f, 0.85f);
            _duration = Math.Max(0.5f, durationSeconds);
            _timer = _duration;
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
            float panelW = textW + padX * 2f;
            float panelH = layout.S(28f);
            float panelX = layout.CenterX - panelW / 2f;
            float panelY = hotbarPlateY - panelH - layout.S(10f);

            DrawGlassPanel(spriteBatch, white, panelX, panelY, panelW, panelH, _color, alpha * 0.82f);
            PixelFont.DrawString(
                spriteBatch,
                white,
                _message,
                panelX + padX,
                panelY + padY,
                textSize,
                _color * (0.92f * alpha),
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
            spriteBatch.Draw(white, new Rectangle((int)x, (int)y, (int)w, (int)h), new Color(0.04f, 0.06f, 0.1f) * (0.88f * alpha));
            spriteBatch.Draw(
                white,
                new Rectangle((int)x, (int)(y + h - Math.Max(1f, h * 0.08f)), (int)w, (int)Math.Max(1f, h * 0.08f)),
                accent * (0.55f * alpha));
        }
    }
}

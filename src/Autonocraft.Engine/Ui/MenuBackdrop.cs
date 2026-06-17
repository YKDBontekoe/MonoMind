using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Engine.Animation;

namespace Autonocraft.Engine
{
    public sealed class MenuBackdrop
    {
        private float _time;

        public MenuBackdrop(int unusedOrbCount = 0)
        {
            _ = unusedOrbCount;
        }

        public void Update(float deltaTime)
        {
            _time += deltaTime;
        }

        public void Draw(UiRenderer ui, Viewport viewport, float alpha = 1f)
        {
            int w = viewport.Width;
            int h = viewport.Height;

            ui.DrawBatch((batch, tex) =>
            {
                DrawMeshGradient(batch, tex, w, h, alpha);
                DrawSoftBlobs(batch, tex, w, h, _time, alpha);
                DrawNoise(batch, tex, w, h, alpha);
            });

            ui.DrawVignette(0.38f, alpha);
        }

        private static void DrawMeshGradient(SpriteBatch batch, Texture2D tex, int w, int h, float alpha)
        {
            const int cols = 6;
            const int rows = 5;
            float cellW = w / (float)cols;
            float cellH = h / (float)rows;

            Color topLeft = new Color(0.10f, 0.11f, 0.14f);
            Color topRight = new Color(0.08f, 0.09f, 0.12f);
            Color bottomLeft = new Color(0.07f, 0.08f, 0.10f);
            Color bottomRight = new Color(0.06f, 0.07f, 0.09f);

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    float tx = col / (float)(cols - 1);
                    float ty = row / (float)(rows - 1);
                    Color top = Color.Lerp(topLeft, topRight, tx);
                    Color bottom = Color.Lerp(bottomLeft, bottomRight, tx);
                    Color color = Color.Lerp(top, bottom, ty);
                    batch.Draw(tex, new Rectangle((int)(col * cellW), (int)(row * cellH), (int)(cellW + 1f), (int)(cellH + 1f)), color * (0.98f * alpha));
                }
            }
        }

        private static void DrawSoftBlobs(SpriteBatch batch, Texture2D tex, int w, int h, float time, float alpha)
        {
            DrawBlob(batch, tex, w * 0.18f, h * 0.22f, 220f, time * 0.04f, UiTheme.AccentGlow, alpha * 0.08f);
            DrawBlob(batch, tex, w * 0.82f, h * 0.30f, 260f, time * 0.03f + 1.2f, new Color(0.30f, 0.45f, 0.75f), alpha * 0.06f);
            DrawBlob(batch, tex, w * 0.55f, h * 0.78f, 300f, time * 0.025f + 2.4f, new Color(0.22f, 0.32f, 0.55f), alpha * 0.05f);
        }

        private static void DrawBlob(SpriteBatch batch, Texture2D tex, float cx, float cy, float radius, float phase, Color color, float alpha)
        {
            float driftX = MathF.Sin(phase) * 18f;
            float driftY = MathF.Cos(phase * 0.8f) * 12f;
            cx += driftX;
            cy += driftY;

            const int layers = 8;
            for (int i = layers; i >= 1; i--)
            {
                float expand = i * (radius * 0.14f);
                float layerAlpha = alpha * (1f / i);
                batch.Draw(
                    tex,
                    new Rectangle((int)(cx - expand), (int)(cy - expand), (int)(expand * 2f), (int)(expand * 2f)),
                    color * layerAlpha);
            }
        }

        private static void DrawNoise(SpriteBatch batch, Texture2D tex, int w, int h, float alpha)
        {
            var rng = new Random(17);
            int specks = (w * h) / 9000;
            var noiseColor = new Color(0.55f, 0.60f, 0.68f);
            for (int i = 0; i < specks; i++)
            {
                int x = rng.Next(w);
                int y = rng.Next(h);
                float a = 0.015f + (float)rng.NextDouble() * 0.025f;
                batch.Draw(tex, new Rectangle(x, y, 1, 1), noiseColor * (a * alpha));
            }
        }
    }
}

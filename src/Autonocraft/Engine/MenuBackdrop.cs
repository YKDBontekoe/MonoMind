using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Engine.Animation;

namespace Autonocraft.Engine
{
    public sealed class MenuBackdrop
    {
        private sealed class FloatingBlock
        {
            public float X;
            public float Y;
            public float Size;
            public float Speed;
            public float Phase;
            public float Depth;
            public Color Color;
        }

        private sealed class TerrainChunk
        {
            public float X;
            public float Width;
            public float Height;
            public float Shade;
        }

        private sealed class Star
        {
            public float X;
            public float Y;
            public float Size;
            public float Freq;
            public float Phase;
        }

        private readonly FloatingBlock[] _blocks;
        private readonly TerrainChunk[] _terrain;
        private readonly Star[] _stars;
        private float _time;

        public MenuBackdrop(int blockCount = 40)
        {
            var rng = new Random(42);
            _blocks = new FloatingBlock[blockCount];
            for (int i = 0; i < blockCount; i++)
            {
                _blocks[i] = new FloatingBlock
                {
                    X = (float)rng.NextDouble(),
                    Y = (float)rng.NextDouble(),
                    Size = 8f + (float)rng.NextDouble() * 20f,
                    Speed = 0.01f + (float)rng.NextDouble() * 0.03f,
                    Phase = (float)rng.NextDouble() * MathF.PI * 2f,
                    Depth = 0.4f + (float)rng.NextDouble() * 0.6f,
                    Color = PickBlockColor(rng)
                };
            }

            var terrainRng = new Random(7);
            var chunks = new System.Collections.Generic.List<TerrainChunk>();
            float x = 0f;
            while (x < 1400f)
            {
                float chunkW = 36f + (float)terrainRng.NextDouble() * 72f;
                chunks.Add(new TerrainChunk
                {
                    X = x,
                    Width = chunkW,
                    Height = 24f + (float)terrainRng.NextDouble() * 56f,
                    Shade = 0.035f + (float)terrainRng.NextDouble() * 0.045f
                });
                x += chunkW * 0.82f;
            }

            _terrain = chunks.ToArray();

            var starRng = new Random(99);
            _stars = new Star[56];
            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i] = new Star
                {
                    X = (float)starRng.NextDouble(),
                    Y = (float)starRng.NextDouble() * 0.5f,
                    Size = 1f + (float)starRng.NextDouble() * 2.5f,
                    Freq = 0.1f + (float)starRng.NextDouble() * 0.1f,
                    Phase = (float)starRng.NextDouble() * 10f
                };
            }
        }

        public void Update(float deltaTime)
        {
            _time += deltaTime;
        }

        public void Draw(UiRenderer ui, Viewport viewport, float alpha = 1f)
        {
            int w = viewport.Width;
            int h = viewport.Height;
            float horizonY = h * 0.74f;

            ui.DrawFullscreenBackground(new Color(0.015f, 0.025f, 0.05f));

            ui.DrawBatch((batch, tex) =>
            {
                DrawSkyGradient(batch, tex, w, h, alpha);
                DrawAurora(batch, tex, w, h, _time, alpha);
                DrawMoon(batch, tex, w, h, _time, alpha);
                DrawTerrain(batch, tex, w, h, horizonY, alpha);
                DrawFloatingBlocks(batch, tex, w, h, alpha);
                DrawStars(batch, tex, w, h, _time, alpha);
            });

            ui.DrawVignette(0.42f, alpha);
        }

        private void DrawFloatingBlocks(SpriteBatch batch, Texture2D tex, int w, int h, float alpha)
        {
            foreach (var block in _blocks)
            {
                float parallax = block.Depth;
                float driftX = MathF.Sin(_time * block.Speed * 36f + block.Phase) * (8f + 14f * parallax);
                float driftY = (_time * block.Speed * h * 0.28f * parallax + block.Y * h) % (h + block.Size * 2f) - block.Size;
                float x = block.X * w + driftX;
                float y = driftY;
                float size = block.Size * (0.65f + 0.35f * parallax);
                float pulse = 0.32f + 0.12f * Tween.Pulse(_time + block.Phase, 0.07f);
                Color baseColor = block.Color * (pulse * alpha);

                batch.Draw(tex, new Rectangle((int)(x + size * 0.18f), (int)(y + size * 0.12f), (int)(size * 0.82f), (int)(size * 0.88f)), Color.Black * (0.22f * alpha));
                batch.Draw(tex, new Rectangle((int)x, (int)y, (int)size, (int)size), baseColor);
                batch.Draw(tex, new Rectangle((int)x, (int)y, (int)size, (int)Math.Max(1f, size * 0.2f)), Color.Lerp(baseColor, Color.White, 0.3f));
                batch.Draw(tex, new Rectangle((int)x, (int)y, (int)Math.Max(1f, size * 0.14f), (int)size), Color.Black * (0.18f * alpha));
                batch.Draw(tex, new Rectangle((int)(x + size - Math.Max(1f, size * 0.1f)), (int)y, (int)Math.Max(1f, size * 0.1f), (int)size), Color.Black * (0.12f * alpha));
            }
        }

        private static void DrawSkyGradient(SpriteBatch batch, Texture2D tex, int w, int h, float alpha)
        {
            const int bands = 12;
            float bandH = h / (float)bands;
            for (int i = 0; i < bands; i++)
            {
                float t = i / (float)(bands - 1);
                Color zenith = new Color(0.05f, 0.1f, 0.22f);
                Color mid = new Color(0.03f, 0.06f, 0.12f);
                Color horizon = new Color(0.02f, 0.04f, 0.08f);
                Color color = t < 0.5f
                    ? Color.Lerp(zenith, mid, t * 2f)
                    : Color.Lerp(mid, horizon, (t - 0.5f) * 2f);
                batch.Draw(tex, new Rectangle(0, (int)(i * bandH), w, (int)(bandH + 1f)), color * (0.7f * alpha));
            }
        }

        private static void DrawAurora(SpriteBatch batch, Texture2D tex, int w, int h, float time, float alpha)
        {
            const int strips = 6;
            float baseY = h * 0.14f;
            for (int i = 0; i < strips; i++)
            {
                float wave = MathF.Sin(time * 0.35f + i * 0.8f) * 0.5f + 0.5f;
                float stripY = baseY + i * 10f + wave * 8f;
                float stripW = w * (0.55f + 0.1f * i);
                float stripX = (w - stripW) * 0.5f + MathF.Sin(time * 0.2f + i) * 24f;
                Color color = Color.Lerp(new Color(0.08f, 0.35f, 0.42f), new Color(0.15f, 0.2f, 0.55f), i / (float)strips);
                batch.Draw(tex, new Rectangle((int)stripX, (int)stripY, (int)stripW, 3), color * ((0.08f + wave * 0.06f) * alpha));
            }
        }

        private static void DrawMoon(SpriteBatch batch, Texture2D tex, int w, int h, float time, float alpha)
        {
            float moonX = w * 0.78f + MathF.Sin(time * 0.15f) * 6f;
            float moonY = h * 0.14f;
            float moonR = 28f;
            float glowPulse = 0.7f + 0.3f * Tween.Pulse(time, 0.12f);

            for (int i = 4; i >= 1; i--)
            {
                float expand = i * 6f;
                batch.Draw(
                    tex,
                    new Rectangle((int)(moonX - moonR - expand), (int)(moonY - moonR - expand), (int)((moonR + expand) * 2f), (int)((moonR + expand) * 2f)),
                    new Color(0.55f, 0.7f, 0.95f) * (0.03f * glowPulse * alpha / i));
            }

            batch.Draw(tex, new Rectangle((int)(moonX - moonR), (int)(moonY - moonR), (int)(moonR * 2f), (int)(moonR * 2f)), new Color(0.82f, 0.88f, 0.96f) * (0.55f * alpha));
            batch.Draw(tex, new Rectangle((int)(moonX - moonR * 0.55f), (int)(moonY - moonR * 0.35f), (int)(moonR * 0.5f), (int)(moonR * 0.35f)), new Color(0.7f, 0.76f, 0.86f) * (0.25f * alpha));
        }

        private void DrawTerrain(SpriteBatch batch, Texture2D tex, int w, int h, float horizonY, float alpha)
        {
            foreach (var chunk in _terrain)
            {
                float x = chunk.X % (w + chunk.Width) - chunk.Width * 0.2f;
                float shade = chunk.Shade;
                batch.Draw(
                    tex,
                    new Rectangle((int)x, (int)(horizonY - chunk.Height), (int)chunk.Width, (int)(chunk.Height + h - horizonY)),
                    new Color(shade, shade + 0.025f, shade + 0.05f) * alpha);
            }

            batch.Draw(tex, new Rectangle(0, (int)horizonY, w, (int)(h - horizonY)), new Color(0.025f, 0.04f, 0.06f) * (0.92f * alpha));
        }

        private void DrawStars(SpriteBatch batch, Texture2D tex, int w, int h, float time, float alpha)
        {
            foreach (var star in _stars)
            {
                float twinkle = 0.2f + 0.8f * Tween.Pulse(time + star.Phase, star.Freq);
                batch.Draw(
                    tex,
                    new Rectangle((int)(star.X * w), (int)(star.Y * h), (int)star.Size, (int)star.Size),
                    new Color(0.75f, 0.86f, 1.0f) * (twinkle * 0.38f * alpha));
            }
        }

        private static Color PickBlockColor(Random rng)
        {
            return rng.Next(6) switch
            {
                0 => new Color(0.2f, 0.52f, 0.26f),
                1 => new Color(0.36f, 0.38f, 0.44f),
                2 => new Color(0.16f, 0.4f, 0.7f),
                3 => new Color(0.42f, 0.3f, 0.16f),
                4 => new Color(0.52f, 0.45f, 0.3f),
                _ => new Color(0.28f, 0.62f, 0.58f)
            };
        }
    }
}

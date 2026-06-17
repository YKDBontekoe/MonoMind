using System;
using System.IO;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.Engine
{
    public sealed class UiTypography : IDisposable
    {
        private readonly FontSystem _regularSystem;
        private readonly FontSystem _semiBoldSystem;

        public UiTypography(GraphicsDevice device)
        {
            var settings = new FontSystemSettings
            {
                FontResolutionFactor = 2,
                KernelWidth = 2,
                KernelHeight = 2
            };

            _regularSystem = new FontSystem(settings);
            _semiBoldSystem = new FontSystem(settings);

            string baseDir = AppContext.BaseDirectory;
            string regularPath = Path.Combine(baseDir, "Fonts", "Inter-Regular.ttf");
            string semiBoldPath = Path.Combine(baseDir, "Fonts", "Inter-SemiBold.ttf");

            if (!File.Exists(regularPath) || !File.Exists(semiBoldPath))
            {
                throw new FileNotFoundException(
                    "UI fonts not found. Expected Fonts/Inter-Regular.ttf and Fonts/Inter-SemiBold.ttf next to the executable.");
            }

            _regularSystem.AddFont(File.ReadAllBytes(regularPath));
            _semiBoldSystem.AddFont(File.ReadAllBytes(semiBoldPath));
        }

        public DynamicSpriteFont GetRegular(float size) => _regularSystem.GetFont(size);

        public DynamicSpriteFont GetSemiBold(float size) => _semiBoldSystem.GetFont(size);

        public float Measure(string text, float size, bool semiBold = false)
        {
            var font = semiBold ? GetSemiBold(size) : GetRegular(size);
            return font.MeasureString(text).X;
        }

        public void Draw(
            SpriteBatch batch,
            string text,
            float x,
            float y,
            float size,
            Color color,
            bool semiBold = false,
            float alpha = 1f)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var font = semiBold ? GetSemiBold(size) : GetRegular(size);
            font.DrawText(batch, text, new Vector2(x, y), color * alpha);
        }

        public void Dispose()
        {
            _regularSystem.Dispose();
            _semiBoldSystem.Dispose();
        }
    }
}

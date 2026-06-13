using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.Engine
{
    public readonly struct UiLayout
    {
        public const float RefWidth = 1280f;
        public const float RefHeight = 720f;

        public float Width { get; }
        public float Height { get; }
        public float Scale { get; }
        public float Padding { get; }
        public float CenterX => Width / 2f;
        public float CenterY => Height / 2f;

        public UiLayout(Viewport viewport) : this(viewport.Width, viewport.Height) { }

        public UiLayout(float width, float height)
        {
            Width = width;
            Height = height;
            Scale = System.Math.Clamp(height / RefHeight, 0.45f, 3f);
            Padding = S(20f);
        }

        public float S(float designPixels) => designPixels * Scale;
    }
}

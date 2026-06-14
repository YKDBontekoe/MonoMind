using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.Engine
{
    public static class UiTheme
    {
        public static readonly Color PanelFill = new Color(0.04f, 0.06f, 0.1f);
        public static readonly Color PanelBorder = new Color(0.2f, 0.45f, 0.65f);
        public static readonly Color Title = new Color(0.82f, 0.92f, 1.0f);
        public static readonly Color Subtitle = new Color(0.52f, 0.68f, 0.82f);
        public static readonly Color Section = new Color(0.55f, 0.72f, 0.88f);
        public static readonly Color Meta = new Color(0.55f, 0.65f, 0.75f);
        public static readonly Color Hint = new Color(0.36f, 0.42f, 0.5f);
        public static readonly Color Accent = new Color(0.25f, 0.78f, 1.0f);
        public static readonly Color AccentGlow = new Color(0.15f, 0.55f, 0.9f);
        public static readonly Color Danger = new Color(0.95f, 0.35f, 0.35f);
        public static readonly Color Rule = new Color(0.16f, 0.28f, 0.4f);
        public static readonly Color StatLabel = new Color(0.55f, 0.6f, 0.68f);
        public static readonly Color StatValue = new Color(0.85f, 0.88f, 0.92f);

        public static void DrawMenuScrim(UiRenderer ui, Viewport viewport, float alpha)
        {
            ui.DrawFilledRect(0, 0, viewport.Width, viewport.Height, Color.Black * (0.35f * alpha));
        }

        public static void DrawSectionHeader(UiRenderer ui, string label, float x, float y, UiLayout layout, float alpha, float size = 1.2f)
        {
            ui.DrawString(label, x, y, layout.S(size), Section * alpha);
        }
    }
}

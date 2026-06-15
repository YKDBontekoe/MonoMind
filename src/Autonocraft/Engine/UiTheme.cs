using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.Engine
{
    public static class UiTheme
    {
        // Reusable Design System Colors
        public static readonly Color PanelFill = new Color(0.04f, 0.06f, 0.1f);
        public static readonly Color PanelBorder = new Color(0.2f, 0.45f, 0.65f);

        // High Contrast Typography Colors
        public static readonly Color Title = new Color(0.92f, 0.96f, 1.0f);      // Crisp, bright off-white blue
        public static readonly Color Subtitle = new Color(0.78f, 0.85f, 0.92f);  // Light grey-blue for subtitles
        public static readonly Color Section = new Color(0.65f, 0.8f, 0.95f);    // Section headers
        public static readonly Color Meta = new Color(0.72f, 0.78f, 0.84f);       // Labels / detailed information
        public static readonly Color Hint = new Color(0.62f, 0.68f, 0.75f);       // Keyboard hints / auxiliary text

        // Interactive / Accent Colors
        public static readonly Color Accent = new Color(0.25f, 0.78f, 1.0f);      // Vibrant teal/blue accent
        public static readonly Color AccentGlow = new Color(0.15f, 0.55f, 0.9f);  // Glow border
        public static readonly Color Danger = new Color(0.95f, 0.35f, 0.35f);      // Danger red
        public static readonly Color Rule = new Color(0.16f, 0.28f, 0.4f);        // Dividers

        // Stats Display
        public static readonly Color StatLabel = new Color(0.7f, 0.76f, 0.84f);
        public static readonly Color StatValue = new Color(0.9f, 0.94f, 0.98f);

        // Muted Panel Fills for Layout Cards
        public static readonly Color PanelBgMuted = new Color(0.06f, 0.08f, 0.11f);
        public static readonly Color PanelBgAccent = new Color(0.08f, 0.12f, 0.1f);
        public static readonly Color PanelBgHighlight = new Color(0.1f, 0.16f, 0.14f);

        // Reusable Component Sizing / Scales
        public const float ScaleTitle = 1.35f;
        public const float ScaleSection = 1.15f;
        public const float ScaleNormal = 1.0f;
        public const float ScaleSmall = 0.95f; // Minimum size to prevent pixelation/illegibility

        public static void DrawMenuScrim(UiRenderer ui, Viewport viewport, float alpha)
        {
            ui.DrawFilledRect(0, 0, viewport.Width, viewport.Height, Color.Black * (0.35f * alpha));
        }

        public static void DrawSectionHeader(UiRenderer ui, string label, float x, float y, UiLayout layout, float alpha, float size = ScaleSection)
        {
            ui.DrawString(label, x, y, layout.S(size), Section * alpha);
        }
    }
}

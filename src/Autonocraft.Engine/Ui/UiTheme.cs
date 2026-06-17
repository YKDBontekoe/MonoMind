using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.Engine
{
    public static class UiTheme
    {
        // Surfaces — soft dark menu palette
        public static readonly Color Scrim = new Color(0.06f, 0.07f, 0.09f);
        public static readonly Color PanelFill = new Color(0.145f, 0.161f, 0.188f);
        public static readonly Color PanelBorder = new Color(0.24f, 0.27f, 0.32f);
        public static readonly Color PanelBgMuted = new Color(0.118f, 0.133f, 0.157f);
        public static readonly Color PanelBgAccent = new Color(0.165f, 0.184f, 0.216f);
        public static readonly Color PanelBgHighlight = new Color(0.20f, 0.225f, 0.265f);
        public static readonly Color SurfaceElevated = new Color(0.18f, 0.20f, 0.24f);

        // Typography — light on dark panels (~#252930)
        public static readonly Color Title = new Color(0.94f, 0.95f, 0.97f);
        public static readonly Color TextPrimary = Title;
        public static readonly Color Subtitle = new Color(0.72f, 0.76f, 0.82f);
        public static readonly Color Section = new Color(0.88f, 0.90f, 0.94f);
        public static readonly Color Label = new Color(0.62f, 0.66f, 0.72f);
        public static readonly Color Meta = Label;
        public static readonly Color Hint = new Color(0.54f, 0.58f, 0.64f);
        public static readonly Color Muted = Hint;

        // Accent & semantic
        public static readonly Color Accent = new Color(0.35f, 0.58f, 0.98f);
        public static readonly Color AccentHover = new Color(0.28f, 0.52f, 0.94f);
        public static readonly Color AccentGlow = new Color(0.40f, 0.62f, 1.0f);
        public static readonly Color AccentSoft = new Color(0.18f, 0.28f, 0.48f);
        public static readonly Color Danger = new Color(0.95f, 0.32f, 0.36f);
        public static readonly Color DangerSoft = new Color(0.28f, 0.14f, 0.16f);
        public static readonly Color Success = new Color(0.28f, 0.72f, 0.48f);
        public static readonly Color Rule = new Color(0.28f, 0.31f, 0.36f);

        // Stats
        public static readonly Color StatLabel = Label;
        public static readonly Color StatValue = new Color(0.90f, 0.92f, 0.95f);
        public static readonly Color StatAccentTime = new Color(0.40f, 0.64f, 1.0f);
        public static readonly Color StatAccentMove = new Color(0.32f, 0.78f, 0.52f);
        public static readonly Color StatAccentCombat = new Color(0.95f, 0.48f, 0.40f);
        public static readonly Color StatAccentWorld = new Color(0.68f, 0.52f, 0.92f);
        public static readonly Color StatAccentExplore = new Color(0.40f, 0.72f, 0.88f);

        // Buttons
        public static readonly Color ButtonPrimaryText = Color.White;
        public static readonly Color ButtonSecondaryText = new Color(0.88f, 0.90f, 0.94f);
        public static readonly Color ButtonGhostText = new Color(0.68f, 0.72f, 0.78f);
        public static readonly Color ButtonDangerText = new Color(0.98f, 0.55f, 0.58f);

        // Progress & sliders
        public static readonly Color ProgressTrack = new Color(0.22f, 0.25f, 0.30f);
        public static readonly Color ProgressFill = Accent;
        public static readonly Color ProgressLabel = new Color(0.88f, 0.90f, 0.94f);
        public static readonly Color ProgressPercent = Meta;
        public static readonly Color SliderThumb = Accent;
        public static readonly Color SliderThumbHover = new Color(0.45f, 0.66f, 1.0f);
        public static readonly Color SliderThumbActive = new Color(0.52f, 0.72f, 1.0f);

        // HUD — dark glass
        public static readonly Color HudGlassFill = new Color(0.08f, 0.09f, 0.12f);
        public static readonly Color HudGlassBorder = new Color(0.22f, 0.24f, 0.30f);
        public static readonly Color HudSlotFill = new Color(0.12f, 0.13f, 0.17f);
        public static readonly Color HudSlotSelected = new Color(0.16f, 0.22f, 0.34f);
        public static readonly Color HudSlotBorder = new Color(0.28f, 0.30f, 0.36f);
        public static readonly Color HudTextPrimary = new Color(0.94f, 0.95f, 0.97f);
        public static readonly Color HudTextSecondary = new Color(0.62f, 0.66f, 0.72f);
        public static readonly Color HudBarTrack = new Color(0.18f, 0.19f, 0.24f);
        public static readonly Color HudSkillFill = new Color(0.28f, 0.52f, 0.96f);

        // Overlays
        public static readonly Color DeathScrim = new Color(0.04f, 0.05f, 0.07f);
        public static readonly Color OverlayScrim = new Color(0.06f, 0.07f, 0.09f);
        public static readonly Color DeathPanel = PanelFill;
        public static readonly Color DeathBorder = Danger;

        // Type scale (px at 720p reference)
        public const float FontHero = 42f;
        public const float FontTitle = 28f;
        public const float FontSection = 18f;
        public const float FontBody = 15f;
        public const float FontSmall = 13f;
        public const float FontCaption = 11f;

        // Legacy pixel-font scale factors (HUD still uses scale × 11 → px)
        public const float ScaleTitle = 1.35f;
        public const float ScaleSection = 1.15f;
        public const float ScaleNormal = 1.0f;
        public const float ScaleSmall = 0.95f;

        // Layout
        public const float RadiusSm = 6f;
        public const float RadiusMd = 10f;
        public const float RadiusLg = 14f;
        public const float RadiusXl = 18f;
        public const float ShadowOffset = 3f;
        public const float ShadowAlpha = 0.22f;

        public const float HudGlassAlpha = 0.88f;
        public const float MenuScrimAlpha = 0.55f;

        public static void DrawMenuScrim(UiRenderer ui, Viewport viewport, float alpha)
        {
            ui.DrawFilledRect(0, 0, viewport.Width, viewport.Height, Scrim * (MenuScrimAlpha * alpha));
        }

        public static void DrawSectionHeader(UiRenderer ui, string label, float x, float y, UiLayout layout, float alpha, float fontSize = FontSection)
        {
            ui.DrawLabel(label, x, y, layout.S(fontSize), Section, semiBold: true, alpha: alpha);
        }
    }
}

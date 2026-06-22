using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Engine;
using Microsoft.Xna.Framework;

namespace Autonocraft.UI.Menu
{
    /// <summary>
    /// Shared backdrop and scrim drawing for pre-game menu screens.
    /// </summary>
    public static class MenuChrome
    {
        public static void DrawBackdrop(MenuBackdrop backdrop, UiRenderer ui, Viewport viewport, float deltaTime, float alpha = 1f)
        {
            backdrop.Update(deltaTime);
            backdrop.Draw(ui, viewport, alpha);
        }

        public static void DrawBackdrop(MenuBackdrop backdrop, UiRenderer ui, Viewport viewport, float alpha = 1f)
        {
            backdrop.Draw(ui, viewport, alpha);
        }

        public static void DrawOverlayScrim(UiRenderer ui, Viewport viewport, float alpha = 0.72f)
        {
            UiTheme.DrawMenuScrim(ui, viewport, alpha);
        }

        public static void DrawTitleBlock(
            UiRenderer ui,
            UiLayout layout,
            string title,
            string tagline,
            float titleYFactor,
            float alpha,
            float offsetY = 0f)
        {
            float titleY = layout.Height * titleYFactor + offsetY;
            ui.DrawCenteredTitle(title, titleY, layout.S(UiTheme.FontHero), UiTheme.Title, alpha);
            ui.DrawCenteredText(tagline, titleY + layout.S(46f), layout.S(UiTheme.FontBody), UiTheme.Subtitle, alpha * 0.92f);
        }

        public static void DrawHintFooter(UiRenderer ui, UiLayout layout, string hint, float alpha, float bottomPad = 32f)
        {
            ui.DrawCenteredText(hint, layout.Height - layout.S(bottomPad), layout.S(UiTheme.FontSmall), UiTheme.Hint, 0.85f * alpha);
        }

        public static void DrawEyebrow(UiRenderer ui, UiLayout layout, string label, float x, float y, Color accent, float alpha)
        {
            float font = layout.S(UiTheme.FontCaption);
            float padX = layout.S(10f);
            float padY = layout.S(5f);
            float w = ui.MeasureString(label, font, semiBold: true) + padX * 2f;
            float h = layout.S(22f);
            ui.DrawRoundedRect(x, y, w, h, h * 0.5f, accent * (0.13f * alpha));
            ui.DrawRoundedRectOutline(x, y, w, h, h * 0.5f, accent * 0.42f, 1f, alpha);
            ui.DrawLabel(label, x + padX, y + padY, font, accent, semiBold: true, alpha: alpha);
        }

        public static void DrawMetaChip(UiRenderer ui, UiLayout layout, string label, float x, float y, Color accent, float alpha)
        {
            float font = layout.S(UiTheme.FontCaption);
            float padX = layout.S(9f);
            float chipW = ui.MeasureString(label, font, semiBold: true) + padX * 2f;
            float chipH = layout.S(20f);
            ui.DrawRoundedRect(x, y, chipW, chipH, chipH * 0.5f, accent * (0.12f * alpha));
            ui.DrawRoundedRectOutline(x, y, chipW, chipH, chipH * 0.5f, accent * 0.38f, 1f, alpha);
            ui.DrawLabel(label, x + padX, y + layout.S(4f), font, accent, semiBold: true, alpha: alpha * 0.96f);
        }

        public static void DrawSectionRule(UiRenderer ui, UiLayout layout, float x, float y, float w, float alpha)
        {
            ui.DrawFilledRect(x, y, w, 1f, UiTheme.Rule * (0.72f * alpha));
        }
    }
}

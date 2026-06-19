using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Engine;

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
    }
}

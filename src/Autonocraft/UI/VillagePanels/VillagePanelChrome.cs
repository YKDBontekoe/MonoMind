using Autonocraft.Engine;

namespace Autonocraft.UI.VillagePanels
{
    internal static class VillagePanelChrome
    {
        public static void DrawButton(
            UiRenderer ui,
            float x,
            float y,
            float width,
            float height,
            string label,
            bool hovered,
            UiButtonStyle style,
            UiLayout layout,
            float alpha,
            bool disabled = false)
        {
            ui.DrawButton(
                x,
                y,
                width,
                height,
                label,
                hovered && !disabled,
                false,
                style,
                layout.S(UiTheme.FontBody),
                alpha,
                hovered ? 1f : 0f,
                disabled);
        }
    }
}

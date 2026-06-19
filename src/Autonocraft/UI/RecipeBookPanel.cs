using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Crafting;
using Autonocraft.Engine;

namespace Autonocraft.UI
{
    public sealed class RecipeBookPanel
    {
        private const float RowHeight = 40f;
        private const float PanelWidth = 280f;

        private readonly UiRenderer _ui;
        private int _hoveredIndex = -1;
        private int _scrollOffset;
        private RecipeBookEntry? _clickedEntry;

        public RecipeBookPanel(UiRenderer ui) => _ui = ui;

        public void ResetInteraction()
        {
            _hoveredIndex = -1;
        }

        public RecipeBookEntry? ConsumeClickedEntry()
        {
            var entry = _clickedEntry;
            _clickedEntry = null;
            return entry;
        }

        public void Update(
            Rectangle panelRect,
            IReadOnlyList<RecipeBookEntry> entries,
            MouseState mouse,
            MouseState prevMouse)
        {
            _hoveredIndex = -1;
            _clickedEntry = null;

            if (entries.Count == 0)
            {
                return;
            }

            Point mousePt = new Point(mouse.X, mouse.Y);
            float rowH = RowHeight;
            int visibleRows = Math.Max(1, (int)((panelRect.Height - 56f) / rowH));
            int maxScroll = Math.Max(0, entries.Count - visibleRows);

            if (mouse.ScrollWheelValue > prevMouse.ScrollWheelValue)
            {
                _scrollOffset = Math.Max(0, _scrollOffset - 1);
            }
            else if (mouse.ScrollWheelValue < prevMouse.ScrollWheelValue)
            {
                _scrollOffset = Math.Min(maxScroll, _scrollOffset + 1);
            }

            _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScroll);

            for (int i = 0; i < visibleRows; i++)
            {
                int recipeIndex = _scrollOffset + i;
                if (recipeIndex >= entries.Count)
                {
                    break;
                }

                var rowRect = new Rectangle(
                    panelRect.X + 10,
                    panelRect.Y + 44 + (int)(i * rowH),
                    panelRect.Width - 20,
                    (int)rowH - 2);

                if (!rowRect.Contains(mousePt))
                {
                    continue;
                }

                _hoveredIndex = recipeIndex;
                if (mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
                {
                    _clickedEntry = entries[recipeIndex];
                }
            }
        }

        public void Draw(
            UiLayout layout,
            Rectangle panelRect,
            IReadOnlyList<RecipeBookEntry> entries,
            float alpha = 1f)
        {
            _ui.DrawFramedPanel(panelRect.X, panelRect.Y, panelRect.Width, panelRect.Height,
                UiTheme.SurfaceElevated, UiTheme.Accent * 0.55f, alpha);
            _ui.DrawFilledRect(panelRect.X + 1f, panelRect.Y + 1f, panelRect.Width - 2f, layout.S(3f), UiTheme.Accent * (0.55f * alpha));
            _ui.DrawString("Recipe book", panelRect.X + 14f, panelRect.Y + 12f,
                layout.S(UiTheme.FontSection), UiTheme.Title, alpha, semiBold: true);

            if (entries.Count == 0)
            {
                _ui.DrawString("No recipes for this station", panelRect.X + 14f, panelRect.Y + 48f,
                    layout.S(UiTheme.FontBody), UiTheme.Hint, alpha);
                return;
            }

            float rowH = RowHeight;
            int visibleRows = Math.Max(1, (int)((panelRect.Height - 56f) / rowH));
            int maxScroll = Math.Max(0, entries.Count - visibleRows);
            _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScroll);

            for (int i = 0; i < visibleRows; i++)
            {
                int recipeIndex = _scrollOffset + i;
                if (recipeIndex >= entries.Count)
                {
                    break;
                }

                var entry = entries[recipeIndex];
                bool craftable = entry.IsCraftable;
                bool hovered = _hoveredIndex == recipeIndex;

                float rowY = panelRect.Y + 44f + i * rowH;
                float rowW = panelRect.Width - 20f;
                float rowX = panelRect.X + 10f;

                Color rowFill = craftable
                    ? UiTheme.AccentSoft
                    : UiTheme.PanelBgMuted * 0.85f;
                Color rowBorder = craftable
                    ? UiTheme.Accent * 0.75f
                    : UiTheme.Rule * 0.65f;
                Color textColor = craftable
                    ? UiTheme.AccentGlow
                    : UiTheme.StatValue;

                if (hovered)
                {
                    rowFill = Color.Lerp(rowFill, UiTheme.PanelBgHighlight, 0.55f);
                    rowBorder = craftable ? UiTheme.Accent : UiTheme.Accent * 0.45f;
                }

                _ui.DrawPanel(rowX, rowY, rowW, rowH - 2f, rowFill, rowBorder, borderAlpha: 0.85f, alpha: alpha);

                string status = craftable ? "●" : "○";
                Color statusColor = craftable ? UiTheme.Success : UiTheme.Subtitle;
                _ui.DrawString(status, rowX + 8f, rowY + 4f, layout.S(UiTheme.FontBody), statusColor, alpha, semiBold: true);

                _ui.DrawString(entry.DisplayName, rowX + 24f, rowY + 4f, layout.S(UiTheme.FontBody), textColor, alpha,
                    semiBold: craftable);

                if (!string.IsNullOrWhiteSpace(entry.IngredientSummary))
                {
                    string detail = hovered && !string.IsNullOrWhiteSpace(entry.OutputSummary)
                        ? $"{entry.IngredientSummary} → {entry.OutputSummary}"
                        : entry.IngredientSummary;
                    _ui.DrawString(detail, rowX + 24f, rowY + 20f, layout.S(UiTheme.FontCaption),
                        UiTheme.Hint, alpha * 0.95f);
                }
            }

            _ui.DrawString("B toggle · click to fill grid", panelRect.X + 14f, panelRect.Bottom - 22f,
                layout.S(UiTheme.FontCaption), UiTheme.Hint, alpha);
        }

        public static Rectangle BuildPanelRect(UiLayout layout, float anchorPanelLeft, float anchorPanelRight, float anchorPanelY, float anchorPanelH)
        {
            float width = layout.S(PanelWidth);
            float height = Math.Max(layout.S(280f), anchorPanelH);
            float gap = layout.S(12f);
            float preferredX = anchorPanelRight + gap;
            float x = preferredX + width <= layout.Width - layout.Padding
                ? preferredX
                : Math.Max(layout.Padding, anchorPanelLeft - gap - width);
            float y = Math.Clamp(anchorPanelY, layout.Padding, Math.Max(layout.Padding, layout.Height - height - layout.Padding));
            height = Math.Min(height, layout.Height - y - layout.Padding);
            return new Rectangle((int)x, (int)y, (int)width, (int)Math.Max(layout.S(240f), height));
        }
    }
}

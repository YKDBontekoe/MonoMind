using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Core;
using Autonocraft.Crafting;
using Autonocraft.Engine;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.UI
{
    public sealed class RecipeBookPanel
    {
        private const float RowHeight = 28f;
        private const float PanelWidth = 260f;

        private readonly UiRenderer _ui;
        private int _hoveredIndex = -1;
        private int _clickedIndex = -1;

        public RecipeBookPanel(UiRenderer ui) => _ui = ui;

        public void ResetInteraction()
        {
            _hoveredIndex = -1;
            _clickedIndex = -1;
        }

        private CraftRecipe? _clickedRecipe;

        public CraftRecipe? ConsumeClickedRecipe()
        {
            var recipe = _clickedRecipe;
            _clickedRecipe = null;
            _clickedIndex = -1;
            return recipe;
        }

        public void Update(
            Rectangle panelRect,
            IReadOnlyList<CraftRecipe> recipes,
            Player player,
            CraftGridSize gridSize,
            MouseState mouse,
            MouseState prevMouse)
        {
            _hoveredIndex = -1;
            _clickedIndex = -1;
            _clickedRecipe = null;

            if (recipes.Count == 0)
            {
                return;
            }

            var inventory = new PlayerInventoryAdapter(player);
            Point mousePt = new Point(mouse.X, mouse.Y);
            float rowH = RowHeight;
            int visibleRows = Math.Max(1, (int)((panelRect.Height - 56f) / rowH));

            for (int i = 0; i < visibleRows; i++)
            {
                int recipeIndex = i;
                if (recipeIndex >= recipes.Count)
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
                    _clickedIndex = recipeIndex;
                    _clickedRecipe = recipes[recipeIndex];
                }
            }
        }

        public void Draw(
            UiLayout layout,
            Rectangle panelRect,
            IReadOnlyList<CraftRecipe> recipes,
            DiscoveryJournal journal,
            Player player,
            CraftGridSize gridSize,
            float alpha = 1f)
        {
            _ui.DrawFramedPanel(panelRect.X, panelRect.Y, panelRect.Width, panelRect.Height,
                UiTheme.SurfaceElevated, UiTheme.Accent * 0.55f, alpha);
            _ui.DrawFilledRect(panelRect.X + 1f, panelRect.Y + 1f, panelRect.Width - 2f, layout.S(3f), UiTheme.Accent * (0.55f * alpha));
            _ui.DrawString("Recipe book", panelRect.X + 14f, panelRect.Y + 12f,
                layout.S(UiTheme.FontSection), UiTheme.Title, alpha, semiBold: true);

            if (recipes.Count == 0)
            {
                _ui.DrawString("No recipes yet", panelRect.X + 14f, panelRect.Y + 48f,
                    layout.S(UiTheme.FontBody), UiTheme.Hint, alpha);
                return;
            }

            var inventory = new PlayerInventoryAdapter(player);
            float rowH = RowHeight;
            int visibleRows = Math.Max(1, (int)((panelRect.Height - 56f) / rowH));

            for (int i = 0; i < visibleRows; i++)
            {
                int recipeIndex = i;
                if (recipeIndex >= recipes.Count)
                {
                    break;
                }

                var recipe = recipes[recipeIndex];
                bool unlocked = journal.IsUnlocked(recipe.Id);
                bool craftable = unlocked && RecipeBookResolver.CanCraftWithInventory(recipe, gridSize, inventory);
                bool hovered = _hoveredIndex == recipeIndex;

                float rowY = panelRect.Y + 44f + i * rowH;
                float rowW = panelRect.Width - 20f;
                float rowX = panelRect.X + 10f;

                Color rowFill = craftable
                    ? UiTheme.AccentSoft
                    : unlocked
                        ? UiTheme.PanelBgMuted
                        : UiTheme.PanelBgMuted * 0.65f;
                Color rowBorder = craftable
                    ? UiTheme.Accent * 0.75f
                    : unlocked
                        ? UiTheme.Rule
                        : UiTheme.Rule * 0.55f;
                Color textColor = craftable
                    ? UiTheme.AccentGlow
                    : unlocked
                        ? UiTheme.StatValue
                        : UiTheme.Hint * 0.85f;

                if (hovered)
                {
                    rowFill = Color.Lerp(rowFill, UiTheme.PanelBgHighlight, 0.55f);
                    rowBorder = craftable ? UiTheme.Accent : UiTheme.Accent * 0.45f;
                }

                _ui.DrawPanel(rowX, rowY, rowW, rowH - 2f, rowFill, rowBorder, borderAlpha: 0.85f, alpha: alpha);

                string status = craftable ? "●" : unlocked ? "○" : "?";
                Color statusColor = craftable
                    ? UiTheme.Success
                    : unlocked
                        ? UiTheme.Subtitle
                        : UiTheme.Hint;
                _ui.DrawString(status, rowX + 8f, rowY + 4f, layout.S(UiTheme.FontBody), statusColor, alpha, semiBold: true);

                string label = unlocked ? recipe.DisplayName : "???";
                _ui.DrawString(label, rowX + 24f, rowY + 5f, layout.S(UiTheme.FontBody), textColor, alpha,
                    semiBold: craftable);
            }

            _ui.DrawString("B toggle · click to fill grid", panelRect.X + 14f, panelRect.Bottom - 22f,
                layout.S(UiTheme.FontCaption), UiTheme.Hint, alpha);
        }

        public static Rectangle BuildPanelRect(UiLayout layout, float anchorPanelLeft, float anchorPanelRight, float anchorPanelY, float anchorPanelH)
        {
            float width = layout.S(PanelWidth);
            float height = Math.Max(layout.S(260f), anchorPanelH);
            float gap = layout.S(12f);
            float preferredX = anchorPanelRight + gap;
            float x = preferredX + width <= layout.Width - layout.Padding
                ? preferredX
                : Math.Max(layout.Padding, anchorPanelLeft - gap - width);
            float y = Math.Clamp(anchorPanelY, layout.Padding, Math.Max(layout.Padding, layout.Height - height - layout.Padding));
            height = Math.Min(height, layout.Height - y - layout.Padding);
            return new Rectangle((int)x, (int)y, (int)width, (int)Math.Max(layout.S(220f), height));
        }
    }
}

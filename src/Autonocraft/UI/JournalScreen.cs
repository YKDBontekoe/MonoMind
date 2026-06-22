using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Crafting;
using Autonocraft.Engine;
using Autonocraft.Items;

namespace Autonocraft.UI
{
    public class JournalScreen
    {
        private readonly UiRenderer _ui;

        public JournalScreen(UiRenderer ui)
        {
            _ui = ui;
        }

        public void Draw(Viewport viewport, DiscoveryJournal journal, PlayerSkills skills, float alpha = 1f, float offsetY = 0f)
        {
            if (alpha <= 0.01f)
            {
                return;
            }

            var layout = new UiLayout(viewport.Width, viewport.Height);
            float panelW = layout.S(720f);
            float panelH = layout.S(500f);
            float panelX = layout.CenterX - panelW / 2f;
            float panelY = layout.CenterY - panelH / 2f + offsetY;

            _ui.DrawFullscreenBackground(UiTheme.OverlayScrim * (0.55f * alpha));
            _ui.DrawCard(panelX, panelY, panelW, panelH, alpha, UiTheme.RadiusXl);
            _ui.DrawCenteredTitle("Recipe book", panelY + layout.S(20f), layout.S(UiTheme.FontTitle), UiTheme.Title, alpha);

            float y = panelY + layout.S(58f);
            float left = panelX + layout.S(24f);
            float right = panelX + panelW * 0.50f;

            UiTheme.DrawSectionHeader(_ui, "Getting started", left, y, layout, alpha);
            y += layout.S(28f);
            DrawStarterLine("1 log -> planks in your inventory grid", left, ref y, layout, alpha);
            DrawStarterLine("2 planks vertical -> sticks", left, ref y, layout, alpha);
            DrawStarterLine("Workbench sigil unlocks 3x3 tools", left, ref y, layout, alpha);
            DrawStarterLine("Click ready recipes in the recipe book to fill grids", left, ref y, layout, alpha);

            y += layout.S(14f);
            UiTheme.DrawSectionHeader(_ui, "Skills", left, y, layout, alpha);
            y += layout.S(28f);
            DrawSkillLine("Mining", skills.Mining, left, ref y, layout, alpha);
            DrawSkillLine("Woodcutting", skills.Woodcutting, left, ref y, layout, alpha);
            DrawSkillLine("Combat", skills.Combat, left, ref y, layout, alpha);

            y += layout.S(14f);
            UiTheme.DrawSectionHeader(_ui, "Sigils", left, y, layout, alpha);
            y += layout.S(28f);

            foreach (var sigil in SigilRegistry.All)
            {
                bool unlocked = journal.IsUnlocked(sigil.Id);
                string line = unlocked ? sigil.DisplayName : "???";
                Color color = unlocked ? UiTheme.Title : UiTheme.Hint;
                _ui.DrawString(line, left + layout.S(12f), y, layout.S(UiTheme.FontBody), color, alpha);
                y += layout.S(22f);
            }

            UiTheme.DrawSectionHeader(_ui, "Recipes", right, panelY + layout.S(58f), layout, alpha);
            float recipeY = panelY + layout.S(88f);

            foreach (var recipe in CraftRecipeRegistry.All)
            {
                if (recipe.GridSize > CraftGridSize.ThreeByThree)
                {
                    continue;
                }

                if (recipeY > panelY + panelH - layout.S(58f))
                {
                    break;
                }

                DrawRecipeReferenceLine(recipe, journal, right, ref recipeY, layout, alpha);
            }

            _ui.DrawCenteredText("J or Esc to close", panelY + panelH - layout.S(28f), layout.S(UiTheme.FontSmall), UiTheme.Hint, 0.9f * alpha);
        }

        private void DrawSkillLine(string label, SkillProgress progress, float left, ref float y, UiLayout layout, float alpha)
        {
            string text = $"{label} L{progress.Level} ({progress.Xp:F0}/{progress.XpForNextLevel():F0} XP)";
            _ui.DrawString(text, left + layout.S(12f), y, layout.S(UiTheme.FontBody), UiTheme.StatValue, alpha);
            y += layout.S(20f);
        }

        private void DrawStarterLine(string text, float left, ref float y, UiLayout layout, float alpha)
        {
            _ui.DrawString(text, left + layout.S(12f), y, layout.S(UiTheme.FontBody), UiTheme.StatValue, alpha);
            y += layout.S(21f);
        }

        private void DrawRecipeReferenceLine(CraftRecipe recipe, DiscoveryJournal journal, float left, ref float y, UiLayout layout, float alpha)
        {
            bool unlocked = !recipe.RequiresUnlock || journal.IsUnlocked(recipe.Id);
            Color titleColor = unlocked ? UiTheme.StatValue : UiTheme.Hint;
            string title = unlocked ? recipe.DisplayName : $"Locked: {recipe.DisplayName}";
            _ui.DrawString(Truncate(title, 34), left + layout.S(12f), y, layout.S(UiTheme.FontBody), titleColor, alpha, semiBold: unlocked);

            string detail = unlocked
                ? RecipeBookResolver.GetGuideText(recipe)
                : RecipeBookResolver.GetUnlockHint(recipe);
            _ui.DrawString(Truncate(detail, 54), left + layout.S(12f), y + layout.S(18f),
                layout.S(UiTheme.FontCaption), UiTheme.Hint, alpha);
            y += layout.S(42f);
        }

        private static string Truncate(string text, int maxChars)
        {
            if (text.Length <= maxChars)
            {
                return text;
            }

            return text.Substring(0, Math.Max(0, maxChars - 3)) + "...";
        }
    }
}

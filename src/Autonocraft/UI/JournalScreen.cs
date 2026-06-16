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
            float panelW = layout.S(520f);
            float panelH = layout.S(420f);
            float panelX = layout.CenterX - panelW / 2f;
            float panelY = layout.CenterY - panelH / 2f + offsetY;

            _ui.DrawFullscreenBackground(UiTheme.OverlayScrim * (0.55f * alpha));
            _ui.DrawCard(panelX, panelY, panelW, panelH, alpha, UiTheme.RadiusXl);
            _ui.DrawCenteredTitle("Discovery journal", panelY + layout.S(20f), layout.S(UiTheme.FontTitle), UiTheme.Title, alpha);

            float y = panelY + layout.S(58f);
            float left = panelX + layout.S(24f);

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

            y += layout.S(14f);
            UiTheme.DrawSectionHeader(_ui, "Recipes", left, y, layout, alpha);
            y += layout.S(28f);

            foreach (var recipe in CraftRecipeRegistry.All)
            {
                if (recipe.GridSize > CraftGridSize.ThreeByThree)
                {
                    continue;
                }

                bool unlocked = journal.IsUnlocked(recipe.Id);

                if (y > panelY + panelH - layout.S(44f))
                {
                    break;
                }

                if (recipe.RequiresUnlock && !unlocked)
                {
                    _ui.DrawString("???", left + layout.S(12f), y, layout.S(UiTheme.FontBody), UiTheme.Hint, alpha);
                    y += layout.S(20f);
                    continue;
                }

                string line = unlocked ? recipe.DisplayName : "???";
                Color color = unlocked ? UiTheme.StatValue : UiTheme.Hint;
                _ui.DrawString(line, left + layout.S(12f), y, layout.S(UiTheme.FontBody), color, alpha);
                y += layout.S(20f);

                if (y > panelY + panelH - layout.S(44f))
                {
                    break;
                }
            }

            _ui.DrawCenteredText("J or Esc to close", panelY + panelH - layout.S(28f), layout.S(UiTheme.FontSmall), UiTheme.Hint, 0.9f * alpha);
        }

        private void DrawSkillLine(string label, SkillProgress progress, float left, ref float y, UiLayout layout, float alpha)
        {
            string text = $"{label} L{progress.Level} ({progress.Xp:F0}/{progress.XpForNextLevel():F0} XP)";
            _ui.DrawString(text, left + layout.S(12f), y, layout.S(UiTheme.FontBody), UiTheme.StatValue, alpha);
            y += layout.S(20f);
        }
    }
}

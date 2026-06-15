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

            _ui.DrawFullscreenBackground(UiTheme.PanelFill * (0.65f * alpha));
            _ui.DrawPanel(panelX, panelY, panelW, panelH, UiTheme.PanelBgMuted * alpha, UiTheme.PanelBorder * alpha);
            _ui.DrawCenteredText("DISCOVERY JOURNAL", panelY + layout.S(16f), layout.S(UiTheme.ScaleTitle), UiTheme.Title * alpha);

            float y = panelY + layout.S(52f);
            float left = panelX + layout.S(20f);

            _ui.DrawString("SKILLS", left, y, layout.S(UiTheme.ScaleSection), UiTheme.Section * alpha);
            y += layout.S(24f);
            DrawSkillLine("MINING", skills.Mining, left, ref y, layout, alpha);
            DrawSkillLine("WOODCUTTING", skills.Woodcutting, left, ref y, layout, alpha);
            DrawSkillLine("COMBAT", skills.Combat, left, ref y, layout, alpha);

            y += layout.S(12f);
            _ui.DrawString("SIGILS", left, y, layout.S(UiTheme.ScaleSection), UiTheme.Section * alpha);
            y += layout.S(24f);

            foreach (var sigil in SigilRegistry.All)
            {
                bool unlocked = journal.IsUnlocked(sigil.Id);
                string line = unlocked ? sigil.DisplayName.ToUpperInvariant() : "???";
                Color color = (unlocked ? UiTheme.Title : UiTheme.Hint) * alpha;
                _ui.DrawString(line, left + layout.S(12f), y, layout.S(UiTheme.ScaleNormal), color);
                y += layout.S(20f);
            }

            y += layout.S(12f);
            _ui.DrawString("RECIPES", left, y, layout.S(UiTheme.ScaleSection), UiTheme.Section * alpha);
            y += layout.S(24f);

            foreach (var recipe in CraftRecipeRegistry.All)
            {
                if (recipe.RequiresUnlock && !journal.IsUnlocked(recipe.Id))
                {
                    continue;
                }

                bool unlocked = journal.IsUnlocked(recipe.Id);
                string line = unlocked ? recipe.DisplayName.ToUpperInvariant() : "???";
                Color color = (unlocked ? UiTheme.StatValue : UiTheme.Hint) * alpha;
                _ui.DrawString(line, left + layout.S(12f), y, layout.S(UiTheme.ScaleNormal), color);
                y += layout.S(18f);

                if (y > panelY + panelH - layout.S(40f))
                {
                    break;
                }
            }

            _ui.DrawCenteredText("J OR ESC TO CLOSE", panelY + panelH - layout.S(24f), layout.S(UiTheme.ScaleNormal), UiTheme.Hint * alpha);
        }

        private void DrawSkillLine(string label, SkillProgress progress, float left, ref float y, UiLayout layout, float alpha)
        {
            string text = $"{label} L{progress.Level} ({progress.Xp:F0}/{progress.XpForNextLevel():F0} XP)";
            _ui.DrawString(text, left + layout.S(12f), y, layout.S(UiTheme.ScaleNormal), UiTheme.StatValue * alpha);
            y += layout.S(18f);
        }
    }
}

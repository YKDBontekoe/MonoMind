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

            _ui.DrawFullscreenBackground(new Color(0.02f, 0.03f, 0.06f) * (0.65f * alpha));
            _ui.DrawPanel(panelX, panelY, panelW, panelH, new Color(0.05f, 0.07f, 0.11f) * alpha, new Color(0.35f, 0.55f, 0.45f) * alpha);
            _ui.DrawCenteredText("DISCOVERY JOURNAL", panelY + layout.S(16f), layout.S(1.6f), new Color(0.8f, 0.95f, 0.85f) * alpha);

            float y = panelY + layout.S(52f);
            float left = panelX + layout.S(20f);

            _ui.DrawString("SKILLS", left, y, layout.S(1.3f), new Color(0.55f, 0.75f, 0.65f) * alpha);
            y += layout.S(24f);
            DrawSkillLine("MINING", skills.Mining, left, ref y, layout, alpha);
            DrawSkillLine("WOODCUTTING", skills.Woodcutting, left, ref y, layout, alpha);
            DrawSkillLine("COMBAT", skills.Combat, left, ref y, layout, alpha);

            y += layout.S(12f);
            _ui.DrawString("SIGILS", left, y, layout.S(1.3f), new Color(0.55f, 0.75f, 0.65f) * alpha);
            y += layout.S(24f);

            foreach (var sigil in SigilRegistry.All)
            {
                bool unlocked = journal.IsUnlocked(sigil.Id);
                string line = unlocked ? sigil.DisplayName.ToUpperInvariant() : "???";
                Color color = (unlocked ? Color.White : new Color(0.35f, 0.35f, 0.38f)) * alpha;
                _ui.DrawString(line, left + layout.S(12f), y, layout.S(1.15f), color);
                y += layout.S(20f);
            }

            y += layout.S(12f);
            _ui.DrawString("RECIPES", left, y, layout.S(1.3f), new Color(0.55f, 0.75f, 0.65f) * alpha);
            y += layout.S(24f);

            foreach (var recipe in CraftRecipeRegistry.All)
            {
                if (recipe.RequiresUnlock && !journal.IsUnlocked(recipe.Id))
                {
                    continue;
                }

                bool unlocked = journal.IsUnlocked(recipe.Id);
                string line = unlocked ? recipe.DisplayName.ToUpperInvariant() : "???";
                Color color = (unlocked ? new Color(0.85f, 0.88f, 0.92f) : new Color(0.35f, 0.35f, 0.38f)) * alpha;
                _ui.DrawString(line, left + layout.S(12f), y, layout.S(1.1f), color);
                y += layout.S(18f);

                if (y > panelY + panelH - layout.S(40f))
                {
                    break;
                }
            }

            _ui.DrawCenteredText("J OR ESC TO CLOSE", panelY + panelH - layout.S(24f), layout.S(1.0f), new Color(0.55f, 0.6f, 0.65f) * alpha);
        }

        private void DrawSkillLine(string label, SkillProgress progress, float left, ref float y, UiLayout layout, float alpha)
        {
            string text = $"{label} L{progress.Level} ({progress.Xp:F0}/{progress.XpForNextLevel():F0} XP)";
            _ui.DrawString(text, left + layout.S(12f), y, layout.S(1.05f), new Color(0.85f, 0.88f, 0.92f) * alpha);
            y += layout.S(18f);
        }
    }
}

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Core;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;
using Autonocraft.World;

namespace Autonocraft.UI
{
    public sealed class PlayerDashboardScreen
    {
        private const float PanelWidth = 620f;
        private const float PanelHeight = 540f;
        private const float ButtonWidth = 160f;
        private const float ButtonHeight = 38f;

        private readonly UiRenderer _ui;
        private readonly MenuBackdrop _backdrop = new MenuBackdrop(24);
        private readonly UiTransition _transition = new UiTransition();
        private readonly float[] _buttonHoverT = new float[2];

        private int _selectedTab;
        private int _hoveredButton = -1;
        private string _slotName = string.Empty;
        private PlayerStatistics _stats = new();
        private WorldSaveData? _saveData;
        private bool _hasData;

        public bool IsOpen { get; private set; }
        public bool CloseRequested { get; private set; }

        public PlayerDashboardScreen(UiRenderer ui)
        {
            _ui = ui;
        }

        public void Open(string? slotId, string? slotName)
        {
            _selectedTab = 0;
            _hoveredButton = -1;
            _slotName = slotName ?? string.Empty;
            _saveData = null;
            _stats = new PlayerStatistics();
            _hasData = false;

            if (!string.IsNullOrWhiteSpace(slotId))
            {
                _hasData = WorldSaveManager.TryLoadPlayerStatistics(slotId, out var stats, out var saveData);
                if (_hasData)
                {
                    _stats = stats;
                    _saveData = saveData;
                    if (string.IsNullOrWhiteSpace(_slotName))
                    {
                        _slotName = saveData?.SlotName ?? slotId;
                    }
                }
            }

            IsOpen = true;
            CloseRequested = false;
            _transition.BeginFadeIn(0.2f);
        }

        public void Close()
        {
            IsOpen = false;
            _saveData = null;
            _hoveredButton = -1;
        }

        public void Update(Viewport viewport, KeyboardState kb, KeyboardState prevKb, MouseState mouse, MouseState prevMouse, float deltaTime)
        {
            CloseRequested = false;
            if (!IsOpen)
            {
                return;
            }

            _transition.Update(deltaTime);
            _backdrop.Update(deltaTime);

            for (int i = 0; i < _buttonHoverT.Length; i++)
            {
                float target = _hoveredButton == i ? 1f : 0f;
                _buttonHoverT[i] = Tween.SmoothDamp(_buttonHoverT[i], target, 10f, deltaTime);
            }

            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                CloseRequested = true;
                return;
            }

            if (kb.IsKeyDown(Keys.Left) && !prevKb.IsKeyDown(Keys.Left))
            {
                _selectedTab = Math.Max(0, _selectedTab - 1);
            }

            if (kb.IsKeyDown(Keys.Right) && !prevKb.IsKeyDown(Keys.Right))
            {
                _selectedTab = Math.Min(3, _selectedTab + 1);
            }

            var layout = new UiLayout(viewport);
            float panelW = layout.S(PanelWidth);
            float panelH = layout.S(PanelHeight);
            float panelX = layout.CenterX - panelW / 2f;
            float panelY = layout.CenterY - panelH / 2f;
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float left = panelX + layout.S(20f);

            _hoveredButton = -1;

            string[] tabs = { "OVERVIEW", "COMBAT", "MINING", "SKILLS" };
            float tabY = panelY + layout.S(54f);
            float tabX = left;
            for (int i = 0; i < tabs.Length; i++)
            {
                float tabW = layout.S(108f);
                var tabRect = new Rectangle((int)tabX, (int)tabY, (int)tabW, (int)layout.S(22f));
                if (tabRect.Contains(mouse.X, mouse.Y)
                    && mouse.LeftButton == ButtonState.Pressed
                    && prevMouse.LeftButton == ButtonState.Released)
                {
                    _selectedTab = i;
                }

                tabX += tabW + layout.S(8f);
            }

            float closeX = panelX + panelW - layout.S(20f) - buttonW;
            float closeY = panelY + panelH - layout.S(28f);
            var closeRect = new Rectangle((int)closeX, (int)closeY, (int)buttonW, (int)buttonH);
            if (closeRect.Contains(mouse.X, mouse.Y))
            {
                _hoveredButton = 1;
                if (mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
                {
                    CloseRequested = true;
                }
            }
        }

        public void Draw(Viewport viewport, float alpha = 1f)
        {
            if (!IsOpen || alpha <= 0.01f)
            {
                return;
            }

            alpha *= _transition.Alpha;
            var layout = new UiLayout(viewport);
            float panelW = layout.S(PanelWidth);
            float panelH = layout.S(PanelHeight);
            float panelX = layout.CenterX - panelW / 2f;
            float panelY = layout.CenterY - panelH / 2f + _transition.OffsetY;
            float left = panelX + layout.S(20f);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);

            _backdrop.Draw(_ui, viewport, alpha * 0.85f);
            UiTheme.DrawMenuScrim(_ui, viewport, alpha);

            _ui.DrawFramedPanel(panelX, panelY, panelW, panelH, UiTheme.PanelFill * 0.96f, UiTheme.PanelBorder, alpha);

            string title = string.IsNullOrWhiteSpace(_slotName) ? "PLAYER DASHBOARD" : $"DASHBOARD — {_slotName.ToUpperInvariant()}";
            _ui.DrawCenteredTitle(title, panelY + layout.S(16f), layout.S(1.45f), UiTheme.Title, alpha);
            _ui.DrawHorizontalRule(panelX + layout.S(20f), panelY + layout.S(48f), panelW - layout.S(40f), UiTheme.Rule, 1f, alpha * 0.7f);

            DrawTabs(layout, panelX, panelY, panelW, left, alpha);

            if (!_hasData)
            {
                _ui.DrawCenteredText(
                    "CREATE A WORLD TO TRACK STATS",
                    panelY + panelH * 0.5f,
                    layout.S(1.2f),
                    UiTheme.Meta,
                    alpha);
            }
            else
            {
                switch (_selectedTab)
                {
                    case 1:
                        DrawCombatTab(layout, panelY, left, alpha);
                        break;
                    case 2:
                        DrawMiningTab(layout, panelY, left, alpha);
                        break;
                    case 3:
                        DrawSkillsTab(layout, panelY, left, alpha);
                        break;
                    default:
                        DrawOverviewTab(layout, panelY, left, alpha);
                        break;
                }
            }

            float closeX = panelX + panelW - layout.S(20f) - buttonW;
            float closeY = panelY + panelH - layout.S(28f);
            _ui.DrawButton(closeX, closeY, buttonW, buttonH, "BACK", _hoveredButton == 1, false, layout.S(1.2f), alpha, _buttonHoverT[1]);
            _ui.DrawCenteredText("ESC BACK  |  LEFT/RIGHT TAB", panelY + panelH - layout.S(8f), layout.S(0.95f), UiTheme.Hint, alpha);
        }

        private void DrawTabs(UiLayout layout, float panelX, float panelY, float panelW, float left, float alpha)
        {
            string[] tabs = { "OVERVIEW", "COMBAT", "MINING", "SKILLS" };
            float tabY = panelY + layout.S(54f);
            float tabX = left;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool selected = i == _selectedTab;
                Color color = selected ? UiTheme.Accent : UiTheme.Meta;
                float tabW = layout.S(108f);
                _ui.DrawString(tabs[i], tabX, tabY, layout.S(1.05f), color * alpha);
                if (selected)
                {
                    _ui.DrawFilledRect(tabX, tabY + layout.S(18f), tabW, layout.S(2f), UiTheme.Accent * alpha);
                }

                tabX += tabW + layout.S(8f);
            }
        }

        private void DrawOverviewTab(UiLayout layout, float panelY, float left, float alpha)
        {
            float y = panelY + layout.S(92f);
            DrawSectionHeader("TIME & EXPLORATION", left, ref y, layout, alpha);
            DrawStatLine("PLAY TIME", PlayerStatistics.FormatDuration(_stats.TotalPlayTimeSeconds), left, ref y, layout, alpha);
            DrawStatLine("SESSIONS", PlayerStatistics.FormatCount(_stats.SessionCount), left, ref y, layout, alpha);
            if (_saveData != null)
            {
                DrawStatLine("LAST PLAYED", FormatLastPlayed(_saveData.SavedAt), left, ref y, layout, alpha);
            }

            y += layout.S(8f);
            DrawSectionHeader("MOVEMENT", left, ref y, layout, alpha);
            DrawStatLine("DISTANCE WALKED", PlayerStatistics.FormatDistance(_stats.DistanceWalked), left, ref y, layout, alpha);
            DrawStatLine("STEPS", PlayerStatistics.FormatCount(_stats.StepsWalked), left, ref y, layout, alpha);
            DrawStatLine("DISTANCE FLOWN", PlayerStatistics.FormatDistance(_stats.DistanceFlown), left, ref y, layout, alpha);
            DrawStatLine("MAX ALTITUDE", $"{_stats.MaxAltitude:F1} m", left, ref y, layout, alpha);

            y += layout.S(8f);
            DrawSectionHeader("SURVIVAL", left, ref y, layout, alpha);
            DrawStatLine("DEATHS", PlayerStatistics.FormatCount(_stats.PlayerDeaths), left, ref y, layout, alpha);
            int villageCount = _saveData?.Villages?.Count ?? 0;
            int villagerCount = _saveData?.Villagers?.Count ?? 0;
            DrawStatLine("VILLAGES", PlayerStatistics.FormatCount(villageCount), left, ref y, layout, alpha);
            DrawStatLine("VILLAGERS", PlayerStatistics.FormatCount(villagerCount), left, ref y, layout, alpha);
        }

        private void DrawCombatTab(UiLayout layout, float panelY, float left, float alpha)
        {
            float y = panelY + layout.S(92f);
            DrawSectionHeader("COMBAT", left, ref y, layout, alpha);
            DrawStatLine("ANIMALS KILLED", PlayerStatistics.FormatCount(_stats.AnimalsKilled), left, ref y, layout, alpha);
            DrawStatLine("SHEEP", PlayerStatistics.FormatCount(_stats.SheepKilled), left, ref y, layout, alpha);
            DrawStatLine("PIGS", PlayerStatistics.FormatCount(_stats.PigKilled), left, ref y, layout, alpha);
            DrawStatLine("CHICKENS", PlayerStatistics.FormatCount(_stats.ChickenKilled), left, ref y, layout, alpha);
            y += layout.S(8f);
            DrawStatLine("DAMAGE DEALT", $"{_stats.DamageDealt:F0}", left, ref y, layout, alpha);
            DrawStatLine("DAMAGE TAKEN", $"{_stats.DamageTaken:F0}", left, ref y, layout, alpha);
        }

        private void DrawMiningTab(UiLayout layout, float panelY, float left, float alpha)
        {
            float y = panelY + layout.S(92f);
            DrawSectionHeader("MINING & BUILDING", left, ref y, layout, alpha);
            DrawStatLine("BLOCKS BROKEN", PlayerStatistics.FormatCount(_stats.BlocksBroken), left, ref y, layout, alpha);
            DrawStatLine("BLOCKS PLACED", PlayerStatistics.FormatCount(_stats.BlocksPlaced), left, ref y, layout, alpha);
            DrawStatLine("TOOLS BROKEN", PlayerStatistics.FormatCount(_stats.ToolsBroken), left, ref y, layout, alpha);
            DrawStatLine("ITEMS CRAFTED", PlayerStatistics.FormatCount(_stats.ItemsCrafted), left, ref y, layout, alpha);

            y += layout.S(8f);
            DrawSectionHeader("HAZARDS", left, ref y, layout, alpha);
            DrawStatLine("FALL DAMAGE EVENTS", PlayerStatistics.FormatCount(_stats.FallDamageEvents), left, ref y, layout, alpha);
            DrawStatLine("TIMES DROWNED", PlayerStatistics.FormatCount(_stats.TimesDrowned), left, ref y, layout, alpha);
        }

        private void DrawSkillsTab(UiLayout layout, float panelY, float left, float alpha)
        {
            float y = panelY + layout.S(92f);
            DrawSectionHeader("SKILLS", left, ref y, layout, alpha);

            if (_saveData != null)
            {
                var player = _saveData.Player;
                DrawSkillLine("MINING", player.MiningLevel, player.MiningXp, left, ref y, layout, alpha);
                DrawSkillLine("WOODCUTTING", player.WoodcuttingLevel, player.WoodcuttingXp, left, ref y, layout, alpha);
                DrawSkillLine("COMBAT", player.CombatLevel, player.CombatXp, left, ref y, layout, alpha);

                y += layout.S(12f);
                DrawSectionHeader("DISCOVERIES", left, ref y, layout, alpha);
                int unlocked = _saveData.UnlockedCraftingIds?.Count ?? 0;
                DrawStatLine("UNLOCKED RECIPES", PlayerStatistics.FormatCount(unlocked), left, ref y, layout, alpha);
            }
        }

        private void DrawSectionHeader(string label, float left, ref float y, UiLayout layout, float alpha)
        {
            UiTheme.DrawSectionHeader(_ui, label, left, y, layout, alpha);
            y += layout.S(22f);
        }

        private void DrawStatLine(string label, string value, float left, ref float y, UiLayout layout, float alpha)
        {
            _ui.DrawString(label, left + layout.S(8f), y, layout.S(1.0f), UiTheme.StatLabel * alpha);
            _ui.DrawString(value, left + layout.S(220f), y, layout.S(1.05f), UiTheme.StatValue * alpha);
            y += layout.S(20f);
        }

        private void DrawSkillLine(string name, int level, float xp, float left, ref float y, UiLayout layout, float alpha)
        {
            _ui.DrawString($"{name} LVL {level}", left + layout.S(8f), y, layout.S(1.0f), UiTheme.StatValue * alpha);
            _ui.DrawString($"XP {xp:F0}", left + layout.S(220f), y, layout.S(1.0f), UiTheme.Subtitle * alpha);
            y += layout.S(20f);
        }

        private static string FormatLastPlayed(DateTime savedAt)
        {
            var delta = DateTime.UtcNow - savedAt;
            if (delta.TotalMinutes < 1)
            {
                return "JUST NOW";
            }

            if (delta.TotalHours < 1)
            {
                return $"{(int)delta.TotalMinutes}M AGO";
            }

            if (delta.TotalDays < 1)
            {
                return $"{(int)delta.TotalHours}H AGO";
            }

            if (delta.TotalDays < 30)
            {
                return $"{(int)delta.TotalDays}D AGO";
            }

            return savedAt.ToLocalTime().ToString("MMM d, yyyy");
        }
    }
}

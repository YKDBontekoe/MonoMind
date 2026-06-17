using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Domain.Village;
using Autonocraft.Engine;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;
using Autonocraft.Village;
using Autonocraft.UI.Village;
using Autonocraft.UI.VillagePanels;
using VillageEntity = Autonocraft.Village.Village;
using Vector3 = System.Numerics.Vector3;

namespace Autonocraft.UI
{

    public sealed partial class VillageScreen
    {
        private void DrawHeader(ScreenLayout layout, float panelX, float panelY, float panelW, float alpha, Color accent)
        {
            string name = _isEditingName ? _editingNameBuffer + "_" : _village!.Name;
            Color titleColor = _hoveredButton == 90 ? UiTheme.Accent : UiTheme.Title;
            _ui.DrawCenteredTitle(name, panelY + layout.S(20f), layout.S(UiTheme.FontTitle), titleColor, alpha);

            if (_hoveredButton == 90 || _isEditingName)
            {
                float titleW = layout.S(400f);
                float titleH = layout.S(28f);
                float titleX = layout.Ui.CenterX - titleW / 2f;
                float rectY = panelY + layout.S(14f);
                _ui.DrawPanel(titleX, rectY, titleW, titleH, Color.Transparent, accent, 0.5f, alpha, UiTheme.RadiusSm);
            }

            string tier = _village!.Tier.ToString();
            int citizenCount = CountDisplayedCitizens();
            string subtitle = $"{tier} · {citizenCount}/{_village.PopulationCap} citizens · Tier {(int)_village.Tier}";
            if (_playerCreative)
            {
                subtitle += " · Creative";
            }

            _ui.DrawCenteredText(subtitle, panelY + layout.S(48f), layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha * 0.92f);

            float badgeW = layout.S(88f);
            float badgeH = layout.S(22f);
            float badgeX = panelX + panelW - layout.S(24f) - badgeW;
            float badgeY = panelY + layout.S(18f);
            _ui.DrawPanel(badgeX, badgeY, badgeW, badgeH, UiTheme.AccentSoft, UiTheme.Accent, 0.8f, alpha, UiTheme.RadiusSm);
            _ui.DrawString(tier, badgeX + layout.S(10f), badgeY + layout.S(4f), layout.S(UiTheme.FontSmall), UiTheme.Accent, alpha, semiBold: true);
        }

        private bool IsGoalsTabVisible() => _earlyGuideStage >= 3;

        private int VisibleTabCount => IsGoalsTabVisible() ? TabLabels.Length : TabLabels.Length - 1;

        private int MapVisibleTabToIndex(int visibleIndex)
        {
            if (visibleIndex < 3 || IsGoalsTabVisible())
            {
                return visibleIndex;
            }

            return 0;
        }

        private void DrawTabBar(ScreenLayout layout, float left, float panelY, float alpha, Color accent)
        {
            float tabY = panelY + layout.S(62f);
            float tabX = left;
            for (int i = 0; i < TabLabels.Length; i++)
            {
                if (i == 3 && !IsGoalsTabVisible())
                {
                    continue;
                }

                bool selected = i == _selectedTab;
                bool hovered = _hoveredButton == i;
                Color fill = selected ? UiTheme.AccentSoft : UiTheme.PanelBgMuted;
                Color border = selected || hovered ? UiTheme.Accent : UiTheme.PanelBorder;
                _ui.DrawPanel(tabX, tabY, layout.S(TabWidth), layout.S(TabHeight), fill, border, 0.8f, alpha, UiTheme.RadiusMd);
                Color textColor = selected ? UiTheme.Title : UiTheme.Meta;
                string label = TabLabels[i];
                float textW = _ui.MeasureString(label, layout.S(UiTheme.FontSmall), semiBold: selected);
                _ui.DrawString(label, tabX + (layout.S(TabWidth) - textW) / 2f, tabY + layout.S(8f), layout.S(UiTheme.FontSmall),
                    textColor, alpha, semiBold: selected);
                tabX += layout.S(TabWidth) + layout.S(8f);
            }
        }

        private void HitManualGoals(ScreenLayout layout, MouseState mouse)
        {
            float y = layout.PanelY + layout.S(ContentTop);
            float colLeft = layout.Left + layout.S(16f);

            for (int i = 0; i < GoalsPanel.GoalBlockTypes.Length; i++)
            {
                float btnX = colLeft + i * layout.S(70f);
                float btnY = y + layout.S(98f);
                HitRect(btnX, btnY, layout.S(64f), layout.S(26f), 60 + i, mouse);
            }

            for (int j = 0; j < GoalsPanel.GoalTargetCounts.Length; j++)
            {
                float btnX = colLeft + j * layout.S(70f);
                float btnY = y + layout.S(152f);
                HitRect(btnX, btnY, layout.S(64f), layout.S(26f), 70 + j, mouse);
            }

            HitRect(colLeft, y + layout.S(200f), layout.S(140f), layout.S(32f), 80, mouse);

            float rightLeft = layout.Left + layout.S(420f);
            float rightW = layout.S(PanelWidth) - layout.S(40f) - layout.S(420f) - layout.S(16f);
            float rowY = y + layout.S(82f);
            int shown = 0;
            foreach (var goal in _village!.Scheduler.Goals)
            {
                if (shown >= 6)
                {
                    break;
                }

                float remW = layout.S(72f);
                float remH = layout.S(26f);
                float remX = rightLeft + rightW - remW;
                HitRect(remX, rowY - layout.S(2f), remW, remH, 100 + goal.Id, mouse);

                if (!goal.Completed && goal.Kind == VillageGoalKind.Stock && goal.StockBlock.HasValue && goal.TargetCount > 0)
                {
                    rowY += layout.S(40f);
                }
                else
                {
                    rowY += layout.S(26f);
                }

                shown++;
            }
        }

        private void DrawStyledButton(float x, float y, float w, float h, string label, bool hovered, UiButtonStyle style, UiLayout layout, float alpha, bool disabled = false)
        {
            _ui.DrawButton(x, y, w, h, label, hovered && !disabled, false, style, layout.S(UiTheme.FontBody), alpha, hovered ? 1f : 0f, disabled);
        }

        private void HitTestTabs(ScreenLayout layout, MouseState mouse)
        {
            float tabY = layout.PanelY + layout.S(62f);
            float tabX = layout.Left;
            for (int i = 0; i < TabLabels.Length; i++)
            {
                if (i == 3 && !IsGoalsTabVisible())
                {
                    continue;
                }

                HitRect(tabX, tabY, layout.S(TabWidth), layout.S(TabHeight), i, mouse);
                tabX += layout.S(TabWidth) + layout.S(8f);
            }
        }

        private int CycleTab(int current, int delta)
        {
            int tab = current;
            for (int attempt = 0; attempt < TabLabels.Length; attempt++)
            {
                tab = (tab + delta + TabLabels.Length) % TabLabels.Length;
                if (tab != 3 || IsGoalsTabVisible())
                {
                    return tab;
                }
            }

            return current;
        }
    }
}

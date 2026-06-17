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
        private void HitBuildCatalog(ScreenLayout layout, MouseState mouse, float buttonH)
        {
            float y = layout.PanelY + layout.S(ContentTop) + layout.S(142f);
            float cardH = layout.S(58f);
            float cardW = layout.S(PanelWidth) - layout.S(64f);
            float cardY = y - _buildScroll;
            int buildIndex = 0;
            foreach (var blueprint in PlayerStructureRegistry.All)
            {
                if (blueprint.Id == "town_heart")
                {
                    continue;
                }

                HitRect(layout.Left + layout.S(12f), cardY, cardW, cardH, 20 + buildIndex, mouse);
                cardY += cardH + layout.S(8f);
                buildIndex++;
            }
        }

        private void HitPeopleTab(ScreenLayout layout, MouseState mouse, float buttonH)
        {
            float y = layout.PanelY + layout.S(ContentTop);
            float listW = layout.S(300f);
            float rowY = y + layout.S(32f) - _peopleScroll;
            float rowH = layout.S(42f);
            foreach (var villager in EnumerateCitizens())
            {
                HitRect(layout.Left + layout.S(8f), rowY, listW - layout.S(16f), rowH, 30 + villager.Id, mouse);
                rowY += rowH + layout.S(4f);
            }

            if (_selectedVillagerId >= 0)
            {
                float detailX = layout.Left + listW + layout.S(14f);
                float detailW = layout.S(PanelWidth) - layout.S(40f) - listW - layout.S(14f);
                float pad = layout.S(16f);
                float detailY = y + pad + layout.S(28f) + layout.S(24f) + layout.S(28f) + layout.S(28f) + layout.S(52f);
                HitRect(detailX + pad, detailY, layout.S(96f), layout.S(ButtonHeight), 50, mouse);

                float jobY = detailY + layout.S(48f) + layout.S(24f);
                float jobW = layout.S(96f);
                float jobH = layout.S(ButtonHeight);
                float jobGap = layout.S(10f);
                for (int i = 0; i < AssignableJobs.Length; i++)
                {
                    int row = i / 3;
                    int col = i % 3;
                    HitRect(detailX + pad + col * (jobW + jobGap), jobY + row * (jobH + jobGap), jobW, jobH, 40 + i, mouse);
                }
            }
        }

        private void HandleActivate()
        {
            if (_hoveredButton >= 0 && _hoveredButton < TabLabels.Length)
            {
                _selectedTab = _hoveredButton;
                return;
            }

            if (_hoveredButton == 10)
            {
                if (CountDisplayedCitizens() > 0 && _village!.CanRecruit(_villagers, _playerCreative))
                {
                    RecruitRequested = true;
                }
                else if (CanSummonSettlers())
                {
                    SummonSettlersRequested = true;
                }

                return;
            }

            if (_hoveredButton == 11)
            {
                CloseRequested = true;
                return;
            }

            if (_hoveredButton == 12)
            {
                ClaimRequested = true;
                return;
            }

            if (_hoveredButton == 13)
            {
                RequestWorkZonePlacement = true;
                return;
            }

            if (_hoveredButton == 16 && _takeRationsAction != null && _playerPayer is Core.Player player)
            {
                _takeRationsAction(player);
                return;
            }

            if (_hoveredButton >= 20)
            {
                int buildIndex = 0;
                foreach (var blueprint in PlayerStructureRegistry.All)
                {
                    if (blueprint.Id == "town_heart")
                    {
                        continue;
                    }

                    if (_hoveredButton == 20 + buildIndex)
                    {
                        RequestedBlueprintId = blueprint.Id;
                        RequestBlueprintPlacement = true;
                        return;
                    }

                    buildIndex++;
                }
            }

            if (_hoveredButton >= 30 && _hoveredButton < 200)
            {
                _selectedVillagerId = _hoveredButton - 30;
                return;
            }

            if (_hoveredButton >= 40 && _hoveredButton < 40 + AssignableJobs.Length && _selectedVillagerId >= 0)
            {
                RequestedAssignVillagerId = _selectedVillagerId;
                RequestedAssignJob = AssignableJobs[_hoveredButton - 40].Job;
                return;
            }

            if (_hoveredButton == 50 && _selectedVillagerId >= 0)
            {
                RequestedChatVillagerId = _selectedVillagerId;
            }

            if (!_playWithAi)
            {
                if (_hoveredButton >= 60 && _hoveredButton < 60 + GoalsPanel.GoalBlockTypes.Length)
                {
                    _selectedGoalBlockIndex = _hoveredButton - 60;
                    return;
                }

                if (_hoveredButton >= 70 && _hoveredButton < 70 + GoalsPanel.GoalTargetCounts.Length)
                {
                    _selectedGoalCountIndex = _hoveredButton - 70;
                    return;
                }

                if (_hoveredButton == 80)
                {
                    var block = GoalsPanel.GoalBlockTypes[_selectedGoalBlockIndex];
                    int count = GoalsPanel.GoalTargetCounts[_selectedGoalCountIndex];
                    string desc = $"Gather {count} {block}";
                    _village!.Scheduler.AddStockGoal(block, count, 0, desc);
                    return;
                }

                if (_hoveredButton >= 101)
                {
                    int goalId = _hoveredButton - 100;
                    _village!.Scheduler.RemoveGoal(goalId);
                    return;
                }
            }
        }
    }
}

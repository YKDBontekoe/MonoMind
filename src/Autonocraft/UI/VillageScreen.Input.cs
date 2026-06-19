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
                HitRect(layout.Left + layout.S(8f), rowY, listW - layout.S(16f), rowH,
                    PeopleVillagerButtonBase + villager.Id, mouse);
                rowY += rowH + layout.S(4f);
            }

            if (_selectedVillagerId >= 0 &&
                _villagers.TryGet(_selectedVillagerId, out var selected) &&
                _village != null)
            {
                float detailX = layout.Left + listW + layout.S(14f);
                float pad = layout.S(16f);
                var detailLayout = PeopleDetailLayout.Compute(
                    layout.Ui,
                    selected,
                    _village,
                    AssignFeedback);

                HitRect(detailX + pad, y + detailLayout.TalkButtonY, layout.S(96f), layout.S(ButtonHeight),
                    PeopleTalkButton, mouse);

                float jobW = layout.S(96f);
                float jobH = layout.S(ButtonHeight);
                float jobGap = layout.S(10f);
                for (int i = 0; i < PeopleJobButtonCount; i++)
                {
                    int row = i / 3;
                    int col = i % 3;
                    HitRect(
                        detailX + pad + col * (jobW + jobGap),
                        y + detailLayout.JobSectionY + row * (jobH + jobGap),
                        jobW,
                        jobH,
                        PeopleJobButtonBase + i,
                        mouse);
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

            if (_hoveredButton == 15 && _viewModel?.SuggestedTab is SettlementTab tab)
            {
                _selectedTab = tab switch
                {
                    SettlementTab.People => 2,
                    SettlementTab.Build => 1,
                    _ => 0
                };
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
                else if (CountDisplayedCitizens() > 0)
                {
                    RecruitRequested = true;
                }

                return;
            }

            if (_hoveredButton == 11)
            {
                CloseRequested = true;
                return;
            }

            if (_hoveredButton == 17 && _playWithAi)
            {
                RequestedStewardChat = true;
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
                RefreshVillageState();
                return;
            }

            if (_hoveredButton == DonateSlotButton && _depositSlotAction != null && _playerPayer is Core.Player depositPlayer)
            {
                _depositSlotAction(depositPlayer);
                RefreshVillageState();
                return;
            }

            if (_hoveredButton == DonateBlocksButton && _depositBlocksAction != null && _playerPayer is Core.Player bulkPlayer)
            {
                _depositBlocksAction(bulkPlayer);
                RefreshVillageState();
                return;
            }

            if (_hoveredButton >= PeopleJobButtonBase &&
                _hoveredButton < PeopleJobButtonBase + PeopleJobButtonCount &&
                _selectedVillagerId >= 0)
            {
                RequestedAssignVillagerId = _selectedVillagerId;
                RequestedAssignJob = AssignableJobs[_hoveredButton - PeopleJobButtonBase].Job;
                return;
            }

            if (_hoveredButton == PeopleTalkButton && _selectedVillagerId >= 0 && _playWithAi)
            {
                RequestedChatVillagerId = _selectedVillagerId;
                return;
            }

            if (_hoveredButton >= PeopleVillagerButtonBase && _hoveredButton < PeopleJobButtonBase)
            {
                int newVillagerId = _hoveredButton - PeopleVillagerButtonBase;
                if (newVillagerId != _selectedVillagerId)
                {
                    ClearActionFeedback();
                }

                _selectedVillagerId = newVillagerId;
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

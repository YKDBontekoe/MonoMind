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
            if (_village == null)
            {
                return;
            }

            var panelContext = new VillagePanelContext
            {
                Ui = _ui,
                UiLayout = layout.Ui,
                Village = _village,
                ViewModel = _viewModel,
                Villagers = _villagers,
                PlayerPosition = _playerPos,
                PlayerCreative = _playerCreative,
                PlayWithAi = _playWithAi,
                PlayerPayer = _playerPayer
            };

            foreach (var blueprint in BuildPanel.GetOrderedBlueprints(panelContext))
            {
                HitRect(layout.Left + layout.S(12f), cardY, cardW, cardH, 20 + buildIndex, mouse);
                cardY += cardH + layout.S(8f);
                buildIndex++;
            }
        }

        private void HitPeopleTab(ScreenLayout layout, MouseState mouse, float buttonH)
        {
            // Villager list rows
            float y     = layout.PanelY + layout.S(ContentTop);
            float listW = layout.S(VillagePanels.PeoplePanel.ListWidth);
            float rowY  = y + layout.S(32f) - _peopleScroll;
            float rowH  = layout.S(42f);
            foreach (var villager in EnumerateCitizens())
            {
                HitRect(layout.Left + layout.S(8f), rowY, listW - layout.S(16f), rowH, 1000 + villager.Id, mouse);
                rowY += rowH + layout.S(4f);
            }

            // Detail pane buttons (only when a villager is selected)
            if (_selectedVillagerId >= 0 && _villagers.TryGet(_selectedVillagerId, out var selected))
            {
                float detailX = layout.Left + listW + layout.S(14f);
                float pad     = layout.S(16f);

                // Use the shared calculator — same computation as PeoplePanel.Draw
                VillagePanels.PeoplePanel.GetDetailButtonYs(
                    layout.Ui,
                    layout.PanelY,
                    selected,
                    _village!,
                    hasFeedback: !string.IsNullOrEmpty(AssignFeedback),
                    out float talkButtonY,
                    out float jobGridY);

                // Talk button
                HitRect(detailX + pad, talkButtonY, layout.S(96f), layout.S(ButtonHeight), 50, mouse);

                // Job assignment grid
                float jobW   = layout.S(240f);
                float jobH   = layout.S(ButtonHeight);
                float jobGap = layout.S(10f);
                for (int i = 0; i < AssignableJobs.Length; i++)
                {
                    int row = i / 2;
                    int col = i % 2;
                    HitRect(
                        detailX + pad + col * (jobW + jobGap),
                        jobGridY  + row * (jobH + jobGap),
                        jobW, jobH,
                        40 + i, mouse);
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
                RefreshVillageState();
                RecruitRequested = true;
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

            // Build-tab blueprint selection — ONLY runs when on the Build tab.
            // Previously the >= 20 check ran on all tabs, which caused job buttons
            // (IDs 40-45 == blueprint indices 20-25) to silently trigger blueprint
            // placement instead of job assignment when 20+ blueprints existed.
            if (_selectedTab == 1 && _hoveredButton >= 20 && _village != null)
            {
                int buildIndex = 0;
                var panelContext = new VillagePanelContext
                {
                    Ui           = _ui,
                    UiLayout     = new UiLayout(new Viewport()),
                    Village      = _village,
                    ViewModel    = _viewModel,
                    Villagers    = _villagers,
                    PlayerPosition = _playerPos,
                    PlayerCreative = _playerCreative,
                    PlayWithAi   = _playWithAi,
                    PlayerPayer  = _playerPayer
                };

                foreach (var blueprint in BuildPanel.GetOrderedBlueprints(panelContext))
                {
                    if (blueprint.Id == "town_heart")
                        continue;

                    if (_hoveredButton == 20 + buildIndex)
                    {
                        RequestedBlueprintId = blueprint.Id;
                        RequestBlueprintPlacement = true;
                        return;
                    }

                    buildIndex++;
                }
            }

            // People Tab Logic
            if (_selectedTab == 2)
            {
                if (_hoveredButton >= 1000 && _hoveredButton < 2000)
                {
                    _selectedVillagerId = _hoveredButton - 1000;
                    return;
                }

                if (_hoveredButton >= 40 && _hoveredButton < 40 + AssignableJobs.Length && _selectedVillagerId >= 0)
                {
                    RequestedAssignVillagerId = _selectedVillagerId;
                    RequestedAssignJob = AssignableJobs[_hoveredButton - 40].Job;
                    return;
                }

                if (_hoveredButton == 50 && _selectedVillagerId >= 0 && _playWithAi)
                {
                    RequestedChatVillagerId = _selectedVillagerId;
                    return;
                }
            }

            // Goals Tab Logic
            if (_selectedTab == 3)
            {
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

                    if (_hoveredButton >= 3000)
                    {
                        int goalId = _hoveredButton - 3000;
                        _village!.Scheduler.RemoveGoal(goalId);
                        return;
                    }
                }
                else if (_hoveredButton >= 81 && _hoveredButton <= 83)
                {
                    int index = _hoveredButton - 81;
                    int current = 0;
                    foreach (var contract in VillageAgentContracts.Suggest(_village!, _villagers))
                    {
                        if (current == index)
                        {
                            bool success = VillageAgentContracts.TryAccept(_village!, _villagers, contract.Id, out string message);
                            AssignSuccess = success;
                            AssignFeedback = message;
                            _actionFeedbackFrames = 180;
                            RefreshVillageState();
                            return;
                        }

                        current++;
                    }
                }
            }
        }
    }
}

using System;
using System.Numerics;
using Autonocraft.Domain.Core;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.Village;

namespace Autonocraft.Core
{
    public readonly record struct OpeningObjective(
        string Headline,
        string Detail,
        bool Active,
        bool Dismissible);

    public sealed class EarlyGameGuide
    {
        public const string OpeningGoalHeadline = "First goal: gather wood";
        public const string OpeningGoalDetail = "Punch nearby trees for logs, then craft planks and sticks before nightfall.";
        public const string OpeningGoalToast = "First goal: gather wood. Punch nearby trees for logs, then craft planks and sticks.";
        public const string OpeningGoalCompleteToast = "First logs gathered. Craft planks and sticks, then make a wood axe or sword.";
        public const string StarterSettlementToast = "Founder's Hamlet is ready. Open the Town Board, assign settlers, then gather wood nearby.";

        private float _reminderTimer;
        private bool _firstNightToastShown;
        private bool _firstDeliveryToastShown;
        private bool _openingGoalShown;
        private bool _openingGoalDismissed;

        public bool OpeningGoalDismissed => _openingGoalDismissed;

        public void ResetForNewWorld()
        {
            _reminderTimer = 0f;
            _firstNightToastShown = false;
            _firstDeliveryToastShown = false;
            _openingGoalShown = false;
            _openingGoalDismissed = false;
        }

        public void DismissOpeningGoal(Action clearToast)
        {
            _openingGoalDismissed = true;
            clearToast();
        }

        public OpeningObjective GetOpeningObjective(Player player, Village.Village? village, VillagerManager villagers)
        {
            if (player.CreativeMode || village == null || player.Stats.HasCompletedEarlyGuide || _openingGoalDismissed)
            {
                return default;
            }

            int stage = player.Stats.EarlyGuideStage;
            bool settlementReady = VillageSettlementHealth.GetLivePopulation(village, villagers) > 0;
            var settlementGuidance = settlementReady
                ? SettlementGuidance.Compute(village, villagers, player.Position)
                : default;
            return stage switch
            {
                <= 0 => settlementReady
                    ? new OpeningObjective(
                        settlementGuidance.Headline,
                        settlementGuidance.Detail,
                        Active: true,
                        Dismissible: true)
                    : new OpeningObjective(OpeningGoalHeadline, OpeningGoalDetail, Active: true, Dismissible: true),
                1 => new OpeningObjective(
                    "Next goal: craft a tool",
                    "Use your logs to make planks, sticks, then a wood axe or sword.",
                    Active: true,
                    Dismissible: true),
                2 => new OpeningObjective(
                    "Next goal: assign settlers",
                    "Press V at the Town Heart and assign Lumber or Build so the village helps you.",
                    Active: true,
                    Dismissible: true),
                _ => settlementReady
                    ? new OpeningObjective(
                        settlementGuidance.Headline,
                        "Use the town board when you need the next settlement step.",
                        Active: true,
                        Dismissible: true)
                    : new OpeningObjective(OpeningGoalHeadline, OpeningGoalDetail, Active: true, Dismissible: true)
            };
        }

        public void Update(
            float deltaTime,
            Player player,
            Village.Village? village,
            VillagerManager villagers,
            float timeOfDay,
            bool villageScreenOpen,
            Action<string> showToast)
        {
            if (player.CreativeMode || village == null)
            {
                return;
            }

            int stage = player.Stats.EarlyGuideStage;
            bool settlementReady = VillageSettlementHealth.GetLivePopulation(village, villagers) > 0;
            if (player.Stats.HasCompletedEarlyGuide)
            {
                return;
            }

            _reminderTimer -= deltaTime;

            switch (stage)
            {
                case 0:
                    if (!_openingGoalShown && !_openingGoalDismissed)
                    {
                        showToast(settlementReady ? StarterSettlementToast : OpeningGoalToast);
                        _openingGoalShown = true;
                        _reminderTimer = 18f;
                    }
                    else if (!_openingGoalDismissed && _reminderTimer <= 0f)
                    {
                        showToast(settlementReady
                            ? "Use the Town Board to assign settlers, then gather nearby wood for the village."
                            : "Wood gets you started: logs become planks, sticks, and your first tools.");
                        _reminderTimer = 20f;
                    }

                    if (HasHarvestedWood(player))
                    {
                        player.Stats.EarlyGuideStage = 1;
                        _openingGoalDismissed = false;
                        showToast(OpeningGoalCompleteToast);
                        _reminderTimer = 0f;
                    }
                    break;

                case 1:
                    if (_reminderTimer <= 0f)
                    {
                        if (player.Hunger < SurvivalConstants.MaxHunger * 0.5f)
                        {
                            showToast("Food is the next risk: hunt animals for raw meat, then cook at a Forge when you can.");
                        }
                        else
                        {
                            showToast("Craft a wood axe or sword, then gather food and shelter materials before night.");
                        }

                        _reminderTimer = 12f;
                    }

                    if (HasBasicSurvivalTool(player) || villageScreenOpen)
                    {
                        player.Stats.EarlyGuideStage = 2;
                        _reminderTimer = 0f;
                    }
                    break;

                case 2:
                    if (_reminderTimer <= 0f)
                    {
                        showToast("Press V at the Town Heart — assign LUMBER or BUILD so settlers help secure the village.");
                        _reminderTimer = 15f;
                    }

                    if (AnyVillagerWorking(village, villagers))
                    {
                        player.Stats.EarlyGuideStage = 3;
                        _reminderTimer = 0f;
                    }
                    break;

                case 3:
                    if (!_firstNightToastShown && DayNightCycle.IsNight(timeOfDay))
                    {
                        _firstNightToastShown = true;
                        showToast("Night is dangerous outside. Build cover, keep food ready, and avoid prowling wolves.");
                    }

                    if (_reminderTimer <= 0f)
                    {
                        showToast("Queue a farm plot and keep gathering food. Rations are only for emergencies.");
                        _reminderTimer = 15f;
                    }

                    if (village.WorkQueue.Count > 0 || player.Stats.PlayerDeaths > 0)
                    {
                        player.Stats.EarlyGuideStage = 4;
                        _reminderTimer = 0f;
                    }
                    break;

                case 4:
                    if (_reminderTimer <= 0f)
                    {
                        showToast("Keep food stocked — hunt, cook, or use village rations. Build houses to grow.");
                        _reminderTimer = 20f;
                    }

                    if (village.HousingCapacity > 0 || village.PopulationCap > 2)
                    {
                        player.Stats.EarlyGuideStage = 5;
                        showToast("Tutorial complete! Use the town board (V) to grow your settlement.");
                    }
                    break;
            }
        }

        public void NotifyFirstDelivery(Action<string> showToast)
        {
            if (_firstDeliveryToastShown)
            {
                return;
            }

            _firstDeliveryToastShown = true;
            showToast("First resources delivered to village storage!");
        }

        public static string GetGuidanceHint(Player player, Village.Village? village, VillagerManager villagers)
        {
            if (village == null)
            {
                return "Find the Town Heart or place one to begin";
            }

            int stage = player.Stats.EarlyGuideStage;
            bool settlementReady = VillageSettlementHealth.GetLivePopulation(village, villagers) > 0;
            if (settlementReady)
            {
                return SettlementGuidance.Compute(village, villagers, player.Position).Headline;
            }

            if (stage <= 0)
            {
                return "First goal: gather logs from nearby trees";
            }

            if (player.Hunger < SurvivalConstants.MaxHunger * 0.5f)
            {
                return "Hunt food; use Town Heart rations only in emergencies";
            }

            if (stage == 1)
            {
                return "Craft a wood axe or sword before nightfall";
            }

            if (stage == 2)
            {
                return "Press V — assign settlers to Lumber or Build";
            }

            return SettlementGuidance.Compute(village, villagers, player.Position).Headline;
        }

        /// <summary>
        /// When HUD guidance differs from settlement priority, returns a Town Board note that keeps both in sync.
        /// </summary>
        public static string? GetTownBoardHudContextNote(Player player, Village.Village village, VillagerManager villagers)
        {
            if (player.CreativeMode || player.Stats.HasCompletedEarlyGuide)
            {
                return null;
            }

            if (player.Stats.EarlyGuideStage == 2)
            {
                return "HUD tip: Press V — assign settlers to Lumber or Build";
            }

            string hudHint = GetGuidanceHint(player, village, villagers);
            string settlementHeadline = SettlementGuidance.Compute(village, villagers, player.Position).Headline;
            if (string.Equals(hudHint, settlementHeadline, StringComparison.Ordinal))
            {
                return null;
            }

            return $"HUD tip: {hudHint}";
        }

        private static bool AnyVillagerWorking(Village.Village village, VillagerManager villagers)
        {
            foreach (var villager in villagers.All)
            {
                if (villager.VillageId == village.Id &&
                    villager.CurrentJob != Domain.Village.JobType.Idle &&
                    villager.CurrentJob != Domain.Village.JobType.Sleep)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasFarmPlotQueued(Village.Village village)
        {
            foreach (var site in village.BuildingSites)
            {
                if (site.BlueprintId == "farm_plot")
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasHarvestedWood(Player player)
        {
            foreach (var stack in player.Hotbar)
            {
                if (stack.IsBlock() && IsWoodOrPlank(stack.BlockType))
                {
                    return true;
                }
            }

            for (int i = 0; i < Player.StorageSlotCount; i++)
            {
                var stack = player.Storage.GetSlot(i);
                if (stack.IsBlock() && IsWoodOrPlank(stack.BlockType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasBasicSurvivalTool(Player player)
        {
            foreach (var stack in player.Hotbar)
            {
                if (IsBasicSurvivalTool(stack))
                {
                    return true;
                }
            }

            for (int i = 0; i < Player.StorageSlotCount; i++)
            {
                if (IsBasicSurvivalTool(player.Storage.GetSlot(i)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsBasicSurvivalTool(ItemStack stack)
        {
            return stack.IsTool() &&
                (stack.ToolId == ItemId.WoodAxe ||
                 stack.ToolId == ItemId.WoodSword ||
                 stack.ToolId == ItemId.WoodPickaxe ||
                 stack.ToolId == ItemId.StoneAxe ||
                 stack.ToolId == ItemId.StoneSword ||
                 stack.ToolId == ItemId.StonePickaxe);
        }

        private static bool IsWoodOrPlank(BlockType blockType)
        {
            return blockType.IsLog() || blockType.IsPlank();
        }
    }
}

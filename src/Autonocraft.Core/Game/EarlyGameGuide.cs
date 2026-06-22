using System;
using System.Numerics;
using Autonocraft.Domain.Core;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.Village;

namespace Autonocraft.Core
{
    public sealed class EarlyGameGuide
    {
        private float _reminderTimer;
        private bool _firstNightToastShown;
        private bool _firstDeliveryToastShown;

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
            if (stage >= 5)
            {
                return;
            }

            _reminderTimer -= deltaTime;

            switch (stage)
            {
                case 0:
                    if (_reminderTimer <= 0f)
                    {
                        showToast("Survival start: punch trees for logs, then craft planks and sticks before nightfall.");
                        _reminderTimer = 10f;
                    }

                    if (HasHarvestedWood(player))
                    {
                        player.Stats.EarlyGuideStage = 1;
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
                return "Found or claim a settlement to begin.";
            }

            int stage = player.Stats.EarlyGuideStage;
            if (stage <= 0)
            {
                return "Punch trees for logs; craft planks, sticks, then tools";
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
            if (player.CreativeMode || player.Stats.EarlyGuideStage >= 5)
            {
                return null;
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

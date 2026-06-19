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
                        showToast(player.HasAnyInventoryItems()
                            ? "Press I to craft — B opens the recipe book."
                            : "Punch trees for wood. Press I when you have logs.");
                        _reminderTimer = 10f;
                    }

                    if (player.HasAnyInventoryItems())
                    {
                        player.Stats.EarlyGuideStage = 1;
                        _reminderTimer = 0f;
                    }
                    break;

                case 1:
                    if (_reminderTimer <= 0f)
                    {
                        if (!player.Stats.HasCraftedPlank)
                        {
                            showToast("Craft planks from logs, then sticks — B shows all recipes.");
                        }
                        else if (!player.Stats.HasCraftedTool)
                        {
                            showToast("Craft a wood tool at the bench — check the recipe book (B).");
                        }
                        else
                        {
                            showToast("Hunt animals or gather food before nightfall.");
                        }

                        _reminderTimer = 12f;
                    }

                    if (player.Stats.HasCraftedTool || player.Stats.HasSecuredFood)
                    {
                        player.Stats.EarlyGuideStage = 2;
                        _reminderTimer = 0f;
                    }
                    break;

                case 2:
                    if (_reminderTimer <= 0f)
                    {
                        if (player.Hunger < SurvivalConstants.MaxHunger * 0.5f)
                        {
                            showToast("Hunt for raw meat and cook at a Forge, or forage berries.");
                        }
                        else
                        {
                            showToast("Press V for the Town Board when you're ready to grow your settlement.");
                        }

                        _reminderTimer = 14f;
                    }

                    if (villageScreenOpen || player.Stats.HasSecuredFood)
                    {
                        player.Stats.EarlyGuideStage = 3;
                        _reminderTimer = 0f;
                    }
                    break;

                case 3:
                    if (!_firstNightToastShown && DayNightCycle.IsNight(timeOfDay))
                    {
                        _firstNightToastShown = true;
                        showToast("Night falls — keep food stocked and assign settlers to work.");
                    }

                    if (_reminderTimer <= 0f)
                    {
                        showToast("Open PEOPLE tab — assign LUMBER or BUILD so settlers work.");
                        _reminderTimer = 15f;
                    }

                    if (AnyVillagerWorking(village, villagers))
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

            if (player.Stats.EarlyGuideStage >= 5)
            {
                return SettlementGuidance.Compute(village, villagers, player.Position).Headline;
            }

            if (!player.HasAnyInventoryItems())
            {
                return "Punch trees for wood to begin";
            }

            int stage = player.Stats.EarlyGuideStage;
            if (stage <= 1 && !player.Stats.HasCraftedPlank)
            {
                return "Press I — craft planks from logs (B for recipes)";
            }

            if (stage <= 2 && !player.Stats.HasCraftedTool)
            {
                return "Craft a wood tool at the bench (B for recipe book)";
            }

            if (player.Hunger < SurvivalConstants.MaxHunger * 0.5f)
            {
                return "Hunt/cook food before nightfall";
            }

            if (stage <= 2)
            {
                return "Press V — open Town Board when ready";
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
    }
}

using System;
using System.Numerics;
using Autonocraft.Domain.Core;
using Autonocraft.Entities;
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
                        showToast("Gather food or take rations at Town Heart. Press V for the Town Board.");
                        _reminderTimer = 10f;
                    }

                    if (villageScreenOpen)
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
                            showToast("Hunt animals for raw meat, then cook at a Forge or take rations (V → Overview).");
                        }
                        else
                        {
                            showToast("Open PEOPLE tab — assign LUMBER or BUILD so settlers work.");
                        }

                        _reminderTimer = 12f;
                    }

                    if (AnyVillagerWorking(village, villagers))
                    {
                        player.Stats.EarlyGuideStage = 2;
                        _reminderTimer = 0f;
                    }
                    break;

                case 2:
                    if (_reminderTimer <= 0f)
                    {
                        showToast("Press I for inventory — craft sticks, then a wood sword at the Bench (B for recipe book).");
                        _reminderTimer = 15f;
                    }

                    if (HasFarmPlotQueued(village))
                    {
                        player.Stats.EarlyGuideStage = 3;
                        _reminderTimer = 0f;
                    }
                    break;

                case 3:
                    if (!_firstNightToastShown && DayNightCycle.IsNight(timeOfDay))
                    {
                        _firstNightToastShown = true;
                        showToast("Settlers sleep at night. Your food stock lasts ~6 days — queue a farm.");
                    }

                    if (_reminderTimer <= 0f)
                    {
                        showToast("Shift+click trees near your village to mark lumber. Recruit more workers when ready.");
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
                return "Press V — take rations or open Town Board";
            }

            if (player.Hunger < SurvivalConstants.MaxHunger * 0.5f)
            {
                return "Hunt/cook food or take rations at Town Heart";
            }

            if (stage == 2)
            {
                return "Press I — craft sticks/planks, then tools at Bench";
            }

            return VillageGuidance.GetNextBestAction(village, villagers, player.Position);
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
    }
}

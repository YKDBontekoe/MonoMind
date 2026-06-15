using System;
using Autonocraft.Crafting;
using Autonocraft.Domain.Core;
using Autonocraft.Village;

namespace Autonocraft.Core
{
    public enum EarlyGameGuideStep
    {
        Welcome,
        OpenVillage,
        AssignWork,
        FirstFood,
        AwakenBench,
        SurviveNight,
        Done
    }

    public sealed class EarlyGameGuide
    {
        public EarlyGameGuideStep Step { get; private set; } = EarlyGameGuideStep.Welcome;
        public bool IsActive { get; private set; }
        public bool AutoOpenVillagePending { get; private set; }
        public bool VillageUiWasOpen { get; private set; }
        public int CompletedGoals { get; private set; }
        public bool SurvivedFirstNight { get; private set; }

        private float _warmupTimer;
        private float _playTime;
        private bool _wasNight;
        private bool _firstDuskWarned;
        private bool _stoneMined;

        public void BeginNewWorld()
        {
            Step = EarlyGameGuideStep.Welcome;
            IsActive = true;
            AutoOpenVillagePending = false;
            VillageUiWasOpen = false;
            CompletedGoals = 0;
            SurvivedFirstNight = false;
            _warmupTimer = 0f;
            _playTime = 0f;
            _wasNight = false;
            _firstDuskWarned = false;
            _stoneMined = false;
        }

        public void Skip()
        {
            Step = EarlyGameGuideStep.Done;
            IsActive = false;
            AutoOpenVillagePending = false;
        }

        public void NotifyVillageUiOpened()
        {
            VillageUiWasOpen = true;
            if (Step == EarlyGameGuideStep.OpenVillage)
            {
                Step = EarlyGameGuideStep.AssignWork;
            }
        }

        public void NotifyVillageUiClosed()
        {
            if (Step == EarlyGameGuideStep.OpenVillage)
            {
                Step = EarlyGameGuideStep.AssignWork;
            }
        }

        public void NotifyBlockMined(string blockName)
        {
            if (blockName.Contains("Stone", StringComparison.OrdinalIgnoreCase))
            {
                _stoneMined = true;
            }
        }

        public void Update(
            float deltaTime,
            Player player,
            CraftingSystem crafting,
            VillageManager villages,
            float timeOfDay,
            bool villageUiOpen,
            Action<string>? showToast,
            Action? requestOpenVillage)
        {
            if (!IsActive || player.FlyingMode)
            {
                return;
            }

            _playTime += deltaTime;
            bool isNight = DayNightCycle.IsNight(timeOfDay);

            if (Step == EarlyGameGuideStep.Welcome)
            {
                _warmupTimer += deltaTime;
                if (_warmupTimer >= 3f)
                {
                    Step = EarlyGameGuideStep.OpenVillage;
                    AutoOpenVillagePending = true;
                    showToast?.Invoke("Your settlers need direction — press V");
                }
            }

            if (AutoOpenVillagePending && !villageUiOpen)
            {
                requestOpenVillage?.Invoke();
                AutoOpenVillagePending = false;
            }

            if (villageUiOpen)
            {
                VillageUiWasOpen = true;
            }

            if (player.Hunger < 12f && Step is EarlyGameGuideStep.AssignWork or EarlyGameGuideStep.OpenVillage)
            {
                Step = EarlyGameGuideStep.FirstFood;
                showToast?.Invoke("Eat berries, cook meat, or take a village ration");
            }

            if ((_playTime >= 120f || _stoneMined) && Step is EarlyGameGuideStep.AssignWork or EarlyGameGuideStep.FirstFood or EarlyGameGuideStep.OpenVillage)
            {
                Step = EarlyGameGuideStep.AwakenBench;
                crafting.ShowCraftingHint = true;
            }

            if (isNight && !_wasNight && !_firstDuskWarned)
            {
                _firstDuskWarned = true;
                if (Step != EarlyGameGuideStep.Done)
                {
                    Step = EarlyGameGuideStep.SurviveNight;
                    showToast?.Invoke("Night falls — stay near the Town Heart or build shelter.");
                }
            }

            if (_wasNight && !isNight)
            {
                SurvivedFirstNight = true;
            }

            _wasNight = isNight;

            var village = villages.GetPrimaryVillage();
            if (village != null)
            {
                int completed = 0;
                foreach (var goal in village.Scheduler.Goals)
                {
                    if (goal.Completed)
                    {
                        completed++;
                    }
                }

                CompletedGoals = completed;
            }

            if (SurvivedFirstNight || CompletedGoals >= 2)
            {
                Step = EarlyGameGuideStep.Done;
                IsActive = false;
            }
        }

        public string? GetHudHint()
        {
            if (!IsActive)
            {
                return null;
            }

            return Step switch
            {
                EarlyGameGuideStep.OpenVillage => "PRESS V — MANAGE YOUR SETTLEMENT",
                EarlyGameGuideStep.AssignWork => "ASSIGN GATHER OR QUEUE A FARM PLOT",
                EarlyGameGuideStep.FirstFood => "EAT BERRIES, COOK MEAT, OR TAKE A RATION",
                EarlyGameGuideStep.AwakenBench => "BUILD PATTERNS  SHIFT+CLICK TO AWAKEN",
                EarlyGameGuideStep.SurviveNight => "STAY NEAR TOWN HEART OR BUILD SHELTER",
                _ => null
            };
        }

        public static string GetOfflineStewardReply(string message)
        {
            string lower = message.ToLowerInvariant();
            if (lower.Contains("food") || lower.Contains("hungry") || lower.Contains("eat"))
            {
                return "Queue a farm plot in BUILDINGS, or take a ration from OVERVIEW when food stock allows.";
            }

            if (lower.Contains("recruit") || lower.Contains("villager") || lower.Contains("worker"))
            {
                return "Press R in the village screen when you have 4 planks in storage.";
            }

            if (lower.Contains("build") || lower.Contains("farm") || lower.Contains("bench"))
            {
                return "Open BUILDINGS to queue plots and camps. Awaken a bench with shift+click on a dirt sigil.";
            }

            if (lower.Contains("night") || lower.Contains("wolf") || lower.Contains("safe"))
            {
                return "Stay within the hamlet radius or near a crafting bench at night.";
            }

            if (lower.Contains("help") || lower.Contains("start") || lower.Contains("goal"))
            {
                return "Check the GOALS tab. Food and a bench come first — assign GATHER on a villager.";
            }

            return "I can advise on food, recruits, building, and surviving the night. What do you need?";
        }
    }
}

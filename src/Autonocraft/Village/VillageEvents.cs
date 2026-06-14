using System;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;

namespace Autonocraft.Village
{
    public sealed class VillageEvents
    {
        public Action<string>? ShowToast { get; set; }
        public Action<string>? PlaySfx { get; set; }

        private float _lastFoodCriticalToast;
        private bool _wasHamlet = true;
        private bool _wasVillage;
        private bool _wasTown;

        public void OnRecruit(Villager villager) =>
            Notify($"{villager.Name} joined the settlement!", "recruit");

        public void OnBuildingCompleted(string buildingName) =>
            Notify($"{buildingName} completed!", "build");

        public void OnGoalCompleted(string description) =>
            Notify($"Goal complete: {description}", "goal");

        public void OnTierUp(VillageTier tier)
        {
            string label = tier switch
            {
                VillageTier.Village => "Your hamlet grew into a Village!",
                VillageTier.Town => "Your village became a Town!",
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(label))
            {
                Notify(label, "tier");
            }
        }

        public void CheckFoodCritical(Village village, float timeOfDay)
        {
            if (village.FoodStock > 0f || village.Population == 0)
            {
                return;
            }

            if (timeOfDay - _lastFoodCriticalToast < 60f)
            {
                return;
            }

            _lastFoodCriticalToast = timeOfDay;
            Notify("Food stock empty — villagers are slowing down!", "food");
        }

        public void CheckTierChange(Village village)
        {
            if (village.Tier == VillageTier.Village && !_wasVillage)
            {
                OnTierUp(VillageTier.Village);
            }
            else if (village.Tier == VillageTier.Town && !_wasTown)
            {
                OnTierUp(VillageTier.Town);
            }

            _wasHamlet = village.Tier == VillageTier.Hamlet;
            _wasVillage = village.Tier >= VillageTier.Village;
            _wasTown = village.Tier >= VillageTier.Town;
        }

        public void MorningDigest(Village village, VillagerManager villagers, int buildsYesterday, float foodDelta)
        {
            int idle = 0;
            foreach (int id in village.VillagerIds)
            {
                if (villagers.TryGet(id, out var v) && v.CurrentJob == JobType.Idle)
                {
                    idle++;
                }
            }

            Notify(
                $"Morning report: food {(foodDelta >= 0 ? "+" : "")}{foodDelta:0.#}, {buildsYesterday} build(s), {idle} idle.",
                "digest");
        }

        private void Notify(string message, string sfxKey)
        {
            ShowToast?.Invoke(message);
            PlaySfx?.Invoke(sfxKey);
        }
    }
}

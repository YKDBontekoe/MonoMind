using System;
using Autonocraft.Entities;

namespace Autonocraft.Village
{
    public sealed class VillagerNeeds
    {
        public float Food { get; set; } = 1f;
        public float Rest { get; set; } = 1f;
        public float Social { get; set; } = 1f;

        public void Tick(float deltaTime, bool isWorking, bool isNight, float villageHappiness)
        {
            float foodDrain = isWorking ? 0.015f : 0.008f;
            Food = Math.Clamp(Food - foodDrain * deltaTime, 0f, 1f);
            Rest = isNight
                ? Math.Clamp(Rest + 0.04f * deltaTime, 0f, 1f)
                : Math.Clamp(Rest - 0.01f * deltaTime, 0f, 1f);
            Social = Math.Clamp(Social + (villageHappiness - Social) * 0.005f * deltaTime, 0.1f, 1f);
        }

        public float GetHappinessModifier() =>
            (Food + Rest + Social) / 3f;
    }

    public static class VillagerNeedsTracker
    {
        public static void ApplyNeeds(Villager villager, VillagerNeeds needs, float deltaTime, bool isNight)
        {
            bool isWorking = villager.CurrentJob is Domain.Village.JobType.Lumber
                or Domain.Village.JobType.Mine
                or Domain.Village.JobType.Farm
                or Domain.Village.JobType.Build
                or Domain.Village.JobType.Haul
                or Domain.Village.JobType.Craft
                or Domain.Village.JobType.Hunt
                or Domain.Village.JobType.Mason
                or Domain.Village.JobType.Cook;

            needs.Tick(deltaTime, isWorking, isNight, villager.Happiness);
            float mod = needs.GetHappinessModifier();
            villager.Happiness = Math.Clamp(villager.Happiness * 0.99f + mod * 0.01f, 0.1f, 1.25f);
        }
    }
}

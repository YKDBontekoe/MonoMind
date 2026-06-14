using System.Collections.Generic;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Village;
using VillageEntity = Autonocraft.Village.Village;

namespace Autonocraft.UI.Village
{
    public sealed class VillageViewModel
    {
        public string VillageName { get; init; } = string.Empty;
        public string StatusLine { get; init; } = string.Empty;
        public string NextAction { get; init; } = string.Empty;
        public int Population { get; init; }
        public int PopulationCap { get; init; }
        public float FoodStock { get; init; }
        public float Happiness { get; init; }
        public int HousingCapacity { get; init; }
        public VillageTier Tier { get; init; }
        public IReadOnlyList<VillagerRowViewModel> Villagers { get; init; } = new List<VillagerRowViewModel>();
        public IReadOnlyList<VillageGoal> Goals { get; init; } = new List<VillageGoal>();
        public int PendingBuildCount { get; init; }
        public int WorkQueueCount { get; init; }
        public string RecruitHint { get; init; } = string.Empty;

        public static VillageViewModel Build(
            VillageEntity village,
            VillageManager manager,
            VillagerManager villagers,
            bool playerCreative)
        {
            var rows = new List<VillagerRowViewModel>();
            foreach (int id in village.VillagerIds)
            {
                if (!villagers.TryGet(id, out var villager))
                {
                    continue;
                }

                rows.Add(new VillagerRowViewModel
                {
                    Id = villager.Id,
                    Name = villager.Name,
                    Role = villager.Role.ToString(),
                    Activity = VillagerActivityText.Describe(villager),
                    Progress = VillagerActivityText.DescribeProgress(villager),
                    Happiness = villager.Happiness,
                    NeedsAttention = VillagerActivityText.NeedsAttention(villager)
                });
            }

            int pending = 0;
            foreach (var site in village.BuildingSites)
            {
                if (!site.IsComplete)
                {
                    pending++;
                }
            }

            return new VillageViewModel
            {
                VillageName = village.Name,
                StatusLine = $"{village.Tier} · Pop {village.Population}/{village.PopulationCap} · Food {village.FoodStock:0.#}",
                NextAction = VillageGuidance.GetNextBestAction(village, villagers),
                Population = village.Population,
                PopulationCap = village.PopulationCap,
                FoodStock = village.FoodStock,
                Happiness = village.Happiness,
                HousingCapacity = village.HousingCapacity,
                Tier = village.Tier,
                Villagers = rows,
                Goals = village.Scheduler.Goals,
                PendingBuildCount = pending,
                WorkQueueCount = village.WorkQueue.Count,
                RecruitHint = village.CanRecruit(playerCreative)
                    ? $"Recruit costs {VillageEntity.RecruitFoodCost} oak planks"
                    : "At cap or missing rations"
            };
        }
    }

    public sealed class VillagerRowViewModel
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public string Activity { get; init; } = string.Empty;
        public string Progress { get; init; } = string.Empty;
        public float Happiness { get; init; }
        public bool NeedsAttention { get; init; }
    }
}

using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Autonocraft.Core;
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
        public SettlementActionKind NextActionKind { get; init; }
        public SettlementTab? SuggestedTab { get; init; }
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
        public int IdleWorkerCount { get; init; }
        public FoodRiskLevel FoodRiskLevel { get; init; }
        public string ActiveWorkSummary { get; init; } = string.Empty;
        public string RecruitHint { get; init; } = string.Empty;
        public string RecruitPreview { get; init; } = string.Empty;
        public VillagePulseStatus Pulse { get; init; } = new();
        public string? HudContextNote { get; init; }

        public static VillageViewModel Build(
            VillageEntity village,
            VillageManager manager,
            VillagerManager villagers,
            bool playerCreative,
            Vector3? playerPos = null,
            Player? guidePlayer = null)
        {
            manager.SyncCitizensForVillage(village);

            var rows = new List<VillagerRowViewModel>();
            int idle = 0;
            foreach (var villager in VillageSettlementHealth.EnumerateLiveCitizens(village, villagers))
            {
                if (villager.CurrentJob == JobType.Idle)
                {
                    idle++;
                }

                rows.Add(CreateVillagerRow(villager, village));
            }

            int pending = 0;
            foreach (var site in village.BuildingSites)
            {
                if (!site.IsComplete)
                {
                    pending++;
                }
            }

            int livePopulation = VillageSettlementHealth.CountLiveCitizens(village, villagers);
            var guidance = SettlementGuidance.Compute(village, villagers, playerPos, playerCreative);
            string? hudContextNote = guidePlayer != null
                ? EarlyGameGuide.GetTownBoardHudContextNote(guidePlayer, village, villagers)
                : null;

            return new VillageViewModel
            {
                VillageName = village.Name,
                StatusLine = $"{village.Tier} · Pop {livePopulation}/{village.PopulationCap} · Food {village.FoodStock:0.#}",
                NextAction = guidance.Detail,
                NextActionKind = guidance.NextActionKind,
                SuggestedTab = guidance.SuggestedTab,
                Population = livePopulation,
                PopulationCap = village.PopulationCap,
                FoodStock = village.FoodStock,
                Happiness = village.Happiness,
                HousingCapacity = village.HousingCapacity,
                Tier = village.Tier,
                Villagers = rows,
                Goals = village.Scheduler.Goals,
                PendingBuildCount = pending,
                WorkQueueCount = village.WorkQueue.Count,
                IdleWorkerCount = idle,
                FoodRiskLevel = guidance.FoodRisk,
                ActiveWorkSummary = BuildActiveWorkSummary(village, villagers),
                RecruitHint = livePopulation == 0
                    ? "First settler arrives when you found or claim"
                    : village.CanRecruit(villagers, playerCreative)
                        ? $"Recruit costs {VillageEntity.RecruitFoodCost} oak planks"
                        : livePopulation >= village.PopulationCap
                            ? "Build a Peasant House to raise housing cap"
                            : $"Need {VillageEntity.RecruitFoodCost} oak planks in village storage",
                RecruitPreview = BuildRecruitPreview(village, villagers, playerCreative, livePopulation),
                Pulse = VillagePulse.Read(village, villagers, playerCreative),
                HudContextNote = hudContextNote
            };
        }

        private static VillagerRowViewModel CreateVillagerRow(Villager villager, VillageEntity village) =>
            new()
            {
                Id = villager.Id,
                Name = villager.Name,
                Role = villager.Role.ToString(),
                Activity = VillagerActivityText.Describe(villager, village, null),
                Progress = VillagerActivityText.DescribeProgress(villager, village),
                Happiness = villager.Happiness,
                NeedsAttention = VillagerActivityText.NeedsAttention(villager, village),
                Trait = villager.Persona.Trait
            };

        private static string BuildActiveWorkSummary(VillageEntity village, VillagerManager villagers)
        {
            var parts = new List<string>();
            foreach (var site in village.BuildingSites)
            {
                if (!site.IsComplete)
                {
                    parts.Add($"{site.BlueprintId} {site.CompletionRatio:P0}");
                }
            }

            int working = 0;
            foreach (var villager in VillageSettlementHealth.EnumerateLiveCitizens(village, villagers))
            {
                if (villager.CurrentJob is JobType.Farm or JobType.Haul or JobType.Lumber or JobType.Mine)
                {
                    working++;
                }
            }

            if (working > 0)
            {
                parts.Add($"{working} gathering/hauling");
            }

            if (parts.Count == 0)
            {
                return "No active work";
            }

            var sb = new StringBuilder();
            for (int i = 0; i < parts.Count && i < 3; i++)
            {
                if (i > 0)
                {
                    sb.Append(" · ");
                }

                sb.Append(parts[i]);
            }

            return sb.ToString();
        }

        private static string BuildRecruitPreview(
            VillageEntity village,
            VillagerManager villagers,
            bool playerCreative,
            int livePopulation)
        {
            if (livePopulation == 0)
            {
                return "Summon settlers first — recruit adds extra workers after your first citizen.";
            }

            int planks = village.Storage.CountBlock(VillageEntity.RationBlock);
            string housing = $"{livePopulation}/{village.PopulationCap} housing";
            if (playerCreative)
            {
                return $"Housing: {housing} · Cost: free in creative";
            }

            if (livePopulation >= village.PopulationCap)
            {
                return $"Housing full ({housing}) — queue Peasant House on Build tab";
            }

            bool canAfford = planks >= VillageEntity.RecruitFoodCost;
            return $"Housing: {housing} · Cost: {VillageEntity.RecruitFoodCost} oak planks ({planks} in storage)" +
                   (canAfford ? " · Ready to recruit" : " · Need more planks");
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
        public string Trait { get; init; } = string.Empty;
    }
}

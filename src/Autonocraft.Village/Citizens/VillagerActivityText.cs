using System;
using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.World;
using VillageEntity = Autonocraft.Village.Village;

namespace Autonocraft.Village
{
    public static class VillagerActivityText
    {
        public static string Describe(Entities.Villager villager) =>
            Describe(villager, null, null);

        public static string Describe(Entities.Villager villager, VillageEntity? village, VoxelWorld? world)
        {
            if (villager.CurrentJob == JobType.Idle)
            {
                return "Idle — needs a job";
            }

            if (villager.CurrentJob == JobType.Sleep)
            {
                return "Sleeping";
            }

            return villager.CurrentJob switch
            {
                JobType.Lumber or JobType.Gather => FormatGatherActivity("Chopping", villager, village),
                JobType.Mine => FormatGatherActivity("Mining", villager, village),
                JobType.Farm => FormatFarmActivity(villager, village),
                JobType.Build => FormatBuildActivity(villager, village),
                JobType.Haul => villager.HaulIsDelivering
                    ? "Delivering goods to storage"
                    : FormatHaulPickup(villager, village),
                JobType.Craft => villager.Role == VillagerRole.Smith ? "Smithing at workshop" : "Crafting",
                JobType.Hunt => "Hunting",
                JobType.Mason => FormatGatherActivity("Cutting stone", villager, village),
                JobType.Cook => "Cooking meals",
                _ => villager.CurrentJob.ToString()
            };
        }

        public static string DescribeProgress(Entities.Villager villager) =>
            DescribeProgress(villager, null);

        public static string DescribeProgress(Entities.Villager villager, VillageEntity? village)
        {
            if (villager.CurrentJob is JobType.Lumber or JobType.Mine or JobType.Gather or JobType.Mason)
            {
                if (villager.BreakProgress > 0f)
                {
                    return $"{(int)(villager.BreakProgress * 100f)}%";
                }
            }

            if (villager.CurrentJob == JobType.Build)
            {
                if (villager.AssignedBuildingSiteId.HasValue && village != null &&
                    village.TryGetBuildingSite(villager.AssignedBuildingSiteId.Value, out var site))
                {
                    return $"{site.BlueprintId} — {site.CompletionRatio:P0}";
                }

                if (villager.WorkTimer > 0f)
                {
                    return "Placing blocks";
                }
            }

            if (villager.CurrentJob == JobType.Haul && !villager.HaulIsDelivering)
            {
                return "Picking up";
            }

            return string.Empty;
        }

        public static bool NeedsAttention(Entities.Villager villager, VillageEntity? village = null) =>
            villager.CurrentJob == JobType.Idle ||
            villager.Happiness < 0.4f ||
            (village != null && village.ConsecutiveDaysWithoutFood >= 2);

        private static string FormatGatherActivity(string verb, Entities.Villager villager, VillageEntity? village)
        {
            if (villager.JobTarget.HasValue)
            {
                int x = (int)MathF.Floor(villager.JobTarget.Value.X);
                int z = (int)MathF.Floor(villager.JobTarget.Value.Z);
                return $"{verb} at ({x}, {z})";
            }

            return verb switch
            {
                "Chopping" => "Chopping wood",
                "Mining" => "Mining stone",
                _ => verb
            };
        }

        private static string FormatFarmActivity(Entities.Villager villager, VillageEntity? village)
        {
            if (villager.AssignedBuildingId.HasValue && village != null &&
                village.TryGetBuilding(villager.AssignedBuildingId.Value, out var farm))
            {
                return $"Working farm plot at ({farm.AnchorX}, {farm.AnchorZ})";
            }

            return "Working the farm";
        }

        private static string FormatBuildActivity(Entities.Villager villager, VillageEntity? village)
        {
            if (villager.AssignedBuildingSiteId.HasValue && village != null &&
                village.TryGetBuildingSite(villager.AssignedBuildingSiteId.Value, out var site))
            {
                string name = site.BlueprintId.Replace('_', ' ');
                return $"Building {name} — {site.CompletionRatio:P0}";
            }

            return "Building";
        }

        private static string FormatHaulPickup(Entities.Villager villager, VillageEntity? village)
        {
            if (villager.HaulSourceChestId.HasValue)
            {
                return "Picking up from output chest";
            }

            return "Picking up goods";
        }
    }
}

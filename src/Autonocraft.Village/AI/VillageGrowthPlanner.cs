using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.World;

namespace Autonocraft.Village
{
    internal static class VillageGrowthPlanner
    {
        public static void EnsureOrganicGrowthPlan(Village village, VillagerManager villagers)
        {
            int population = VillageSettlementHealth.GetLivePopulation(village, villagers);
            if (population <= 0)
            {
                return;
            }

            int farmCount = village.CountCompletedBuildings("farm_plot") + village.CountPendingSites("farm_plot");
            int houseCount = village.CountCompletedBuildings("peasant_house") + village.CountPendingSites("peasant_house");
            int storageCount = village.CountCompletedBuildings("storage_crate") + village.CountPendingSites("storage_crate");
            int lumberCount = village.CountCompletedBuildings("lumber_camp") + village.CountPendingSites("lumber_camp");
            int quarryCount = village.CountCompletedBuildings("quarry") + village.CountPendingSites("quarry");
            int workshopCount = village.CountCompletedBuildings("workshop") + village.CountPendingSites("workshop");
            int marketCount = village.CountCompletedBuildings("market") + village.CountPendingSites("market");
            int wellCount = village.CountCompletedBuildings("well") + village.CountPendingSites("well");
            int kitchenCount = village.CountCompletedBuildings("kitchen") + village.CountPendingSites("kitchen");

            if (farmCount == 0)
            {
                QueueBuild(village, "farm_plot", 26, "Steward plan: establish Farm Plot", farmCount + 1);
            }

            if (lumberCount == 0)
            {
                QueueBuild(village, "lumber_camp", 24, "Steward plan: establish Lumber Camp", lumberCount + 1);
            }

            if (population >= village.EffectiveRecruitmentCap - 1 || village.HousingCapacity - population < 2)
            {
                QueueBuild(village, "peasant_house", 22, "Steward plan: expand housing", houseCount + 1);
            }

            if (population >= 3 && storageCount == 0)
            {
                QueueBuild(village, "storage_crate", 18, "Steward plan: expand storage", storageCount + 1);
            }

            if (population >= 3 && quarryCount == 0)
            {
                QueueBuild(village, "quarry", 17, "Steward plan: open quarry", quarryCount + 1);
            }

            if (farmCount > 0 && wellCount == 0)
            {
                QueueBuild(village, "well", 16, "Steward plan: build well", wellCount + 1);
            }

            if (population >= 4 && marketCount == 0)
            {
                QueueBuild(village, "market", 15, "Steward plan: build market", marketCount + 1);
            }

            if (population >= 4 && workshopCount == 0)
            {
                QueueBuild(village, "workshop", 14, "Steward plan: open workshop", workshopCount + 1);
            }

            if (population >= 5 && kitchenCount == 0)
            {
                QueueBuild(village, "kitchen", 13, "Steward plan: build kitchen", kitchenCount + 1);
            }

            if (farmCount < System.Math.Max(1, population / 2))
            {
                QueueBuild(village, "farm_plot", 12, "Steward plan: expand cropland", farmCount + 1);
            }

            if (houseCount < System.Math.Max(1, population / 3))
            {
                QueueBuild(village, "peasant_house", 11, "Steward plan: add neighborhood housing", houseCount + 1);
            }

            QueueStock(village, BlockType.OakPlank, System.Math.Max(32, population * 24), 8, "Steward plan: stock oak planks");
            if (population >= 3)
            {
                QueueStock(village, BlockType.Stone, System.Math.Max(24, population * 14), 7, "Steward plan: stock stone");
            }
        }

        private static void QueueBuild(Village village, string blueprintId, int priority, string description, int targetCount)
        {
            if (village.Scheduler.HasOpenBuildGoal(blueprintId))
            {
                return;
            }

            if (village.CountCompletedBuildings(blueprintId) + village.CountPendingSites(blueprintId) >= targetCount)
            {
                return;
            }

            village.Scheduler.AddBuildGoal(blueprintId, priority, description, targetCount);
        }

        private static void QueueStock(Village village, BlockType blockType, int targetCount, int priority, string description)
        {
            if (village.Storage.CountBlock(blockType) >= targetCount || village.Scheduler.HasOpenStockGoal(blockType, targetCount))
            {
                return;
            }

            village.Scheduler.AddStockGoal(blockType, targetCount, priority, description);
        }
    }
}

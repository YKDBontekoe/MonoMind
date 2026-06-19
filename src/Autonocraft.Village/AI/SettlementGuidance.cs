using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using VillageEntity = Autonocraft.Village.Village;

namespace Autonocraft.Village
{
    public enum FoodRiskLevel
    {
        Ok,
        Low,
        Critical
    }

    public enum SettlementActionKind
    {
        None,
        AssignJobs,
        AddressFood,
        QueueHousing,
        Recruit,
        SummonSettlers
    }

    public enum SettlementTab
    {
        Overview,
        People,
        Build
    }

    public readonly struct SettlementGuidance
    {
        public string Headline { get; }
        public string Detail { get; }
        public int Priority { get; }
        public SettlementTab? SuggestedTab { get; }
        public SettlementActionKind NextActionKind { get; }
        public FoodRiskLevel FoodRisk { get; }

        private SettlementGuidance(
            string headline,
            string detail,
            int priority,
            SettlementTab? suggestedTab,
            SettlementActionKind nextActionKind,
            FoodRiskLevel foodRisk)
        {
            Headline = headline;
            Detail = detail;
            Priority = priority;
            SuggestedTab = suggestedTab;
            NextActionKind = nextActionKind;
            FoodRisk = foodRisk;
        }

        public static SettlementGuidance Compute(
            VillageEntity village,
            VillagerManager villagers,
            Vector3? playerPos = null,
            bool creative = false)
        {
            VillageSettlementHealth.SyncPopulationRegistry(village, villagers);
            int livePopulation = VillageSettlementHealth.GetLivePopulation(village, villagers);
            var foodRisk = GetFoodRisk(village, livePopulation);

            if (livePopulation == 0)
            {
                if (VillageSettlementHealth.HasEstablishedSettlement(village))
                {
                    bool nearHeart = playerPos.HasValue &&
                        VillageSettlementHealth.IsPlayerNearTownHeart(village, playerPos.Value);
                    string headline = nearHeart
                        ? "Summon settlers at Town Heart"
                        : "Walk to Town Heart to summon settlers";
                    string detail = nearHeart
                        ? "Settlers missing — click SUMMON SETTLERS, then open People tab"
                        : $"Settlers missing — walk to Town Heart ({village.AnchorX}, {village.AnchorZ})";
                    return new SettlementGuidance(
                        headline,
                        detail,
                        100,
                        SettlementTab.People,
                        SettlementActionKind.SummonSettlers,
                        foodRisk);
                }

                return new SettlementGuidance(
                    "Found or claim a settlement",
                    "Found or claim a settlement to welcome your first settler",
                    100,
                    SettlementTab.Overview,
                    SettlementActionKind.None,
                    foodRisk);
            }

            if (foodRisk == FoodRiskLevel.Critical)
            {
                return new SettlementGuidance(
                    "Food critical — assign farming",
                    "Food is critical — build a farm plot (Build tab) or assign Farm jobs on People tab",
                    90,
                    SettlementTab.Build,
                    SettlementActionKind.AddressFood,
                    foodRisk);
            }

            if (foodRisk == FoodRiskLevel.Low)
            {
                return new SettlementGuidance(
                    "Food low — grow or farm",
                    "Food is low — build a farm plot (Build tab) or assign Farm jobs",
                    80,
                    SettlementTab.Build,
                    SettlementActionKind.AddressFood,
                    foodRisk);
            }

            int idle = CountIdle(village, villagers);
            if (idle > 0)
            {
                return new SettlementGuidance(
                    $"{idle} idle worker(s) — assign jobs",
                    $"{idle} villager(s) idle — open People tab and assign Lumber, Build, or Farm",
                    70,
                    SettlementTab.People,
                    SettlementActionKind.AssignJobs,
                    foodRisk);
            }

            foreach (var site in village.BuildingSites)
            {
                if (!site.IsComplete)
                {
                    return new SettlementGuidance(
                        "Construction needs workers",
                        "Construction queued — assign Build on People tab if nobody is working",
                        60,
                        SettlementTab.People,
                        SettlementActionKind.AssignJobs,
                        foodRisk);
                }
            }

            if (livePopulation < village.PopulationCap && village.CanRecruit(villagers, creative))
            {
                return new SettlementGuidance(
                    "Recruit another worker",
                    $"Press R or Recruit to add another worker (needs {VillageEntity.RecruitFoodCost} oak planks in storage)",
                    50,
                    SettlementTab.Overview,
                    SettlementActionKind.Recruit,
                    foodRisk);
            }

            if (livePopulation >= village.PopulationCap)
            {
                return new SettlementGuidance(
                    "At housing cap — queue housing",
                    "At housing cap — queue a Peasant House on Build tab, then recruit",
                    40,
                    SettlementTab.Build,
                    SettlementActionKind.QueueHousing,
                    foodRisk);
            }

            return new SettlementGuidance(
                "Settlement running smoothly",
                "Settlement running — use Build and People tabs to grow",
                0,
                SettlementTab.Overview,
                SettlementActionKind.None,
                foodRisk);
        }

        public static FoodRiskLevel GetFoodRisk(VillageEntity village, int livePopulation)
        {
            if (livePopulation == 0)
            {
                return FoodRiskLevel.Ok;
            }

            if (village.FoodStock <= 0f || village.ConsecutiveDaysWithoutFood >= 2)
            {
                return FoodRiskLevel.Critical;
            }

            if (village.FoodStock <= livePopulation * 0.5f)
            {
                return FoodRiskLevel.Low;
            }

            return FoodRiskLevel.Ok;
        }

        private static int CountIdle(VillageEntity village, VillagerManager villagers)
        {
            int idle = 0;
            foreach (var villager in villagers.All)
            {
                if (villager.VillageId == village.Id && villager.CurrentJob == JobType.Idle)
                {
                    idle++;
                }
            }

            return idle;
        }
    }
}

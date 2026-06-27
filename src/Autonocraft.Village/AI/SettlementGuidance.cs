using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Items;
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
        RepairRoster
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

        public static SettlementOnboardingState ComputeOnboardingState(
            VillageEntity village,
            VillagerManager villagers,
            bool creative = false,
            Vector3? playerPos = null)
        {
            VillageSettlementHealth.SyncPopulationRegistry(village, villagers);
            int livePopulation = VillageSettlementHealth.GetLivePopulation(village, villagers);
            var guidance = Compute(village, villagers, playerPos, creative);

            if (livePopulation == 0)
            {
                if (!VillageSettlementHealth.HasEstablishedSettlement(village))
                {
                    return new SettlementOnboardingState(
                        "Found settlement",
                        true,
                        "No Town Heart has been established.",
                        "Place or claim a Town Heart to welcome your first settlers.");
                }

                return new SettlementOnboardingState(
                    "Repair village roster",
                    true,
                    "The village has no linked villagers.",
                    "Close and reopen the Town Board to restore the starter villagers automatically.");
            }

            int effectiveRecruitmentCap = village.EffectiveRecruitmentCap;
            if (livePopulation >= effectiveRecruitmentCap)
            {
                return new SettlementOnboardingState(
                    "Build housing",
                    true,
                    "Housing is full.",
                    "Queue a Peasant House on the Build tab, then recruit again.");
            }

            if (!creative && village.Storage.CountBlock(VillageEntity.RationBlock) < VillageEntity.RecruitFoodCost)
            {
                return new SettlementOnboardingState(
                    "Stock planks",
                    true,
                    $"Need {VillageEntity.RecruitFoodCost} oak planks in village storage.",
                    "Assign Lumber or deposit planks into village storage.");
            }

            string starterStep = guidance.NextActionKind switch
            {
                SettlementActionKind.AssignJobs => "Assign jobs",
                SettlementActionKind.AddressFood => "Secure food",
                SettlementActionKind.QueueHousing => "Build housing",
                SettlementActionKind.Recruit => "Recruit worker",
                SettlementActionKind.RepairRoster => "Repair roster",
                _ => "Manage settlement"
            };

            return new SettlementOnboardingState(starterStep, false, string.Empty, string.Empty);
        }

        public static SettlementGuidance Compute(
            VillageEntity village,
            VillagerManager villagers,
            Vector3? playerPos = null,
            bool creative = false)
        {
            VillageSettlementHealth.SyncPopulationRegistry(village, villagers);
            int livePopulation = VillageSettlementHealth.GetLivePopulation(village, villagers);
            int effectiveRecruitmentCap = village.EffectiveRecruitmentCap;
            var foodRisk = GetFoodRisk(village, livePopulation);

            if (livePopulation == 0)
            {
                if (VillageSettlementHealth.HasEstablishedSettlement(village))
                {
                    return new SettlementGuidance(
                        "Village roster needs repair",
                        "Reopen the Town Board to restore the starter villagers",
                        100,
                        SettlementTab.Overview,
                        SettlementActionKind.RepairRoster,
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

            if (livePopulation < effectiveRecruitmentCap && village.CanRecruit(villagers, creative))
            {
                return new SettlementGuidance(
                    "Recruit another worker",
                    $"Press R or Recruit to add another worker (needs {VillageEntity.RecruitFoodCost} oak planks in storage)",
                    50,
                    SettlementTab.Overview,
                    SettlementActionKind.Recruit,
                    foodRisk);
            }

            if (livePopulation >= effectiveRecruitmentCap)
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

    public readonly struct SettlementOnboardingState
    {
        public string StarterStep { get; }
        public bool IsBlocked { get; }
        public string BlockedReason { get; }
        public string Remediation { get; }

        public SettlementOnboardingState(string starterStep, bool isBlocked, string blockedReason, string remediation)
        {
            StarterStep = starterStep;
            IsBlocked = isBlocked;
            BlockedReason = blockedReason;
            Remediation = remediation;
        }
    }
}

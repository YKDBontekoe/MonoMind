using System;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;

namespace Autonocraft.Village
{
    public enum VillagePulseTone
    {
        Settling,
        Busy,
        Growing,
        Trading,
        Delegating,
        Thriving
    }

    public sealed class VillagePulseStatus
    {
        public VillagePulseTone Tone { get; init; }
        public string Mood { get; init; } = string.Empty;
        public string Focus { get; init; } = string.Empty;
        public string Opportunity { get; init; } = string.Empty;
        public string ManualHook { get; init; } = string.Empty;
        public string RecruitHook { get; init; } = string.Empty;
        public string GrowthHook { get; init; } = string.Empty;
        public string TradeHook { get; init; } = string.Empty;
        public string DelegationHook { get; init; } = string.Empty;
        public int FavorBalance { get; init; }
        public int AgentWorkOrderCost { get; init; }
        public float FamilyGrowthProgress { get; init; }
        public bool CanGrowFamily { get; init; }
        public bool CanTrade { get; init; }
        public bool CanDelegate { get; init; }
        public float Momentum { get; init; }
    }

    public static class VillagePulse
    {
        public const int BaseAgentWorkOrderCost = 6;

        public static VillagePulseStatus Read(Village village, VillagerManager villagers, bool creative = false)
        {
            VillageSettlementHealth.SyncPopulationRegistry(village, villagers);
            int population = VillageSettlementHealth.GetLivePopulation(village, villagers);
            int pendingBuilds = CountPendingBuilds(village);
            int completedBuildings = CountCompletedBuildings(village);
            int idle = CountIdleWorkers(village, villagers);
            bool hasHouse = village.HasBuilding(BuildingKind.House);
            bool hasFarm = village.HasBuilding(BuildingKind.FarmPlot);
            bool hasMarket = village.HasBuilding(BuildingKind.Market);
            bool hasWorkshop = village.HasBuilding(BuildingKind.Workshop);
            bool hasStorage = village.HasBuilding(BuildingKind.Storage);
            bool foodComfort = village.FoodStock >= Math.Max(4f, population * 1.5f);
            bool familyGrowthReady = population > 0 && population < village.PopulationCap && foodComfort && village.Happiness >= 0.72f;
            int favor = EstimateFavor(village, population, completedBuildings, foodComfort);
            int orderCost = EstimateAgentWorkOrderCost(population, completedBuildings);
            bool canTrade = hasMarket && population >= 3;
            bool canDelegate = hasWorkshop && population >= 4 && favor >= orderCost;

            string focus = PickFocus(village, population, idle, pendingBuilds, hasHouse, hasFarm, foodComfort);
            string opportunity = PickOpportunity(population, hasHouse, hasFarm, hasMarket, hasWorkshop, hasStorage, foodComfort, familyGrowthReady, canDelegate);
            VillagePulseTone tone = PickTone(population, pendingBuilds, canTrade, canDelegate, foodComfort, idle);

            return new VillagePulseStatus
            {
                Tone = tone,
                Mood = PickMood(tone, population, idle, foodComfort),
                Focus = focus,
                Opportunity = opportunity,
                ManualHook = PickManualHook(population, pendingBuilds, idle),
                RecruitHook = PickRecruitHook(village, population, creative),
                GrowthHook = PickGrowthHook(village, population, familyGrowthReady),
                TradeHook = canTrade
                    ? $"Market open: {favor} favor to spend on trades and contracts."
                    : "Trade emerges when a market has people and surplus to bargain with.",
                DelegationHook = canDelegate
                    ? $"Agent work order ready: {orderCost} favor."
                    : $"Agent work order preview: {orderCost} favor once workshop, workers, and budget line up.",
                FavorBalance = favor,
                AgentWorkOrderCost = orderCost,
                FamilyGrowthProgress = Math.Clamp(village.FamilyGrowthProgress, 0f, 1f),
                CanGrowFamily = familyGrowthReady,
                CanTrade = canTrade,
                CanDelegate = canDelegate,
                Momentum = EstimateMomentum(population, completedBuildings, pendingBuilds, foodComfort, idle)
            };
        }

        private static string PickFocus(
            Village village,
            int population,
            int idle,
            int pendingBuilds,
            bool hasHouse,
            bool hasFarm,
            bool foodComfort)
        {
            if (population == 0)
            {
                return "Light the Town Heart and bring the first settlers in.";
            }

            if (idle > 0)
            {
                return $"{idle} citizen(s) are waiting for useful work.";
            }

            if (!hasHouse && population >= village.PopulationCap)
            {
                return "Housing is the bottleneck for the next family.";
            }

            if (!hasFarm || !foodComfort)
            {
                return "Food and hauling decide how fast the village can grow.";
            }

            if (pendingBuilds > 0)
            {
                return $"{pendingBuilds} build site(s) are shaping the next district.";
            }

            return "The town is stable; choose a bigger ambition.";
        }

        private static string PickOpportunity(
            int population,
            bool hasHouse,
            bool hasFarm,
            bool hasMarket,
            bool hasWorkshop,
            bool hasStorage,
            bool foodComfort,
            bool familyGrowthReady,
            bool canDelegate)
        {
            if (population == 0)
            {
                return "Start with a tiny crew, then let the town become a place people want to join.";
            }

            if (!hasHouse)
            {
                return "A Peasant House turns one camp into a real settlement.";
            }

            if (!hasFarm || !foodComfort)
            {
                return "A farm plus haulers creates the buffer for recruiting and family growth.";
            }

            if (familyGrowthReady)
            {
                return "Open housing and surplus food are attracting another family.";
            }

            if (!hasMarket)
            {
                return "A market turns surplus into favor, your budget for trades and agent help.";
            }

            if (!hasWorkshop)
            {
                return "A workshop makes tools, repairs gear, and prepares agent-run work orders.";
            }

            if (!hasStorage)
            {
                return "Storage lets large projects run without every worker returning to the Town Heart.";
            }

            return canDelegate
                ? "Delegate a stockpile, build, or supply goal and watch citizens execute it."
                : "Earn more favor, then spend it on an agent work order.";
        }

        private static string PickManualHook(int population, int pendingBuilds, int idle)
        {
            if (population == 0)
            {
                return "Manual start: place heart, gather planks, protect the site.";
            }

            if (idle > 0)
            {
                return "Manual control: assign jobs directly on People.";
            }

            if (pendingBuilds > 0)
            {
                return "Manual boost: deliver missing materials to active build sites.";
            }

            return "Manual play becomes steering: mark zones, choose buildings, tune jobs.";
        }

        private static string PickRecruitHook(Village village, int population, bool creative)
        {
            if (population == 0)
            {
                return "Recruiting starts once settlers are linked to the Town Heart.";
            }

            if (population >= village.PopulationCap)
            {
                return "Families need open beds; build housing before adding more citizens.";
            }

            return creative
                ? "Recruiting is free in creative."
                : $"Recruit fast with {Village.RecruitFoodCost} oak planks, or let families arrive when food and housing feel secure.";
        }

        private static string PickGrowthHook(Village village, int population, bool familyGrowthReady)
        {
            if (population == 0)
            {
                return "No families can settle until the first citizens are linked.";
            }

            if (population >= village.PopulationCap)
            {
                return "Growth paused: build housing.";
            }

            if (village.Happiness < 0.72f)
            {
                return "Growth paused: raise happiness.";
            }

            float comfortFood = MathF.Max(4f, population * 1.5f);
            if (village.FoodStock < comfortFood + Village.FamilyArrivalFoodCost)
            {
                return "Growth paused: build food surplus.";
            }

            int pct = (int)MathF.Round(Math.Clamp(village.FamilyGrowthProgress, 0f, 1f) * 100f);
            return familyGrowthReady
                ? $"New family interest {pct}%."
                : "Open beds and surplus food restart growth.";
        }

        private static VillagePulseTone PickTone(
            int population,
            int pendingBuilds,
            bool canTrade,
            bool canDelegate,
            bool foodComfort,
            int idle)
        {
            if (canDelegate)
            {
                return foodComfort ? VillagePulseTone.Thriving : VillagePulseTone.Delegating;
            }

            if (canTrade)
            {
                return VillagePulseTone.Trading;
            }

            if (population >= 3)
            {
                return VillagePulseTone.Growing;
            }

            if (pendingBuilds > 0 || idle == 0)
            {
                return VillagePulseTone.Busy;
            }

            return VillagePulseTone.Settling;
        }

        private static string PickMood(VillagePulseTone tone, int population, int idle, bool foodComfort)
            => tone switch
            {
                VillagePulseTone.Thriving => "Thriving town",
                VillagePulseTone.Delegating => "Ready for contracts",
                VillagePulseTone.Trading => "Market town",
                VillagePulseTone.Growing => foodComfort ? "Growing village" : "Hungry growth",
                VillagePulseTone.Busy => "Busy settlement",
                _ => population == 0 ? "Unsettled" : idle > 0 ? "Waiting for orders" : "Settling in"
            };

        private static int EstimateFavor(Village village, int population, int completedBuildings, bool foodComfort)
            => village.Favor;

        private static int EstimateAgentWorkOrderCost(int population, int completedBuildings)
            => BaseAgentWorkOrderCost + Math.Max(0, population - 4) + Math.Max(0, completedBuildings / 3);

        private static float EstimateMomentum(int population, int completedBuildings, int pendingBuilds, bool foodComfort, int idle)
        {
            float score = population * 0.08f + completedBuildings * 0.10f + pendingBuilds * 0.05f;
            if (foodComfort)
            {
                score += 0.18f;
            }

            if (idle > 0)
            {
                score -= Math.Min(0.18f, idle * 0.04f);
            }

            return Math.Clamp(score, 0.08f, 1f);
        }

        private static int CountCompletedBuildings(Village village)
        {
            int count = 0;
            foreach (var building in village.Buildings)
            {
                if (building.IsComplete)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountPendingBuilds(Village village)
        {
            int count = 0;
            foreach (var site in village.BuildingSites)
            {
                if (!site.IsComplete)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountIdleWorkers(Village village, VillagerManager villagers)
        {
            int count = 0;
            foreach (var villager in VillageSettlementHealth.EnumerateLiveCitizens(village, villagers))
            {
                if (villager.CurrentJob == JobType.Idle)
                {
                    count++;
                }
            }

            return count;
        }
    }
}

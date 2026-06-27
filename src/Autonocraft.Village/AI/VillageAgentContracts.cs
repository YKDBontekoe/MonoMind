using System;
using System.Collections.Generic;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.World;

namespace Autonocraft.Village
{
    public sealed class VillageAgentContract
    {
        public string Id { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public int FavorCost { get; init; }
        public bool CanAfford { get; init; }
        public bool AlreadyActive { get; init; }
        public bool CanAccept => CanAfford && !AlreadyActive;
        public string StatusText { get; init; } = string.Empty;
        public Action<Village>? Apply { get; init; }
    }

    public static class VillageAgentContracts
    {
        public static IReadOnlyList<VillageAgentContract> Suggest(Village village, VillagerManager villagers)
        {
            var pulse = VillagePulse.Read(village, villagers);
            int baseCost = pulse.AgentWorkOrderCost;
            int population = VillageSettlementHealth.GetLivePopulation(village, villagers);
            var contracts = new List<VillageAgentContract>();

            if (population >= village.PopulationCap)
            {
                Add(contracts, village, "housing", "Commission housing",
                    "Steward queues a Peasant House goal for the next family.",
                    baseCost,
                    village.Scheduler.HasOpenBuildGoal("peasant_house"),
                    v => v.Scheduler.AddBuildGoal("peasant_house", 20, "Contract: build Peasant House", v.CountCompletedBuildings("peasant_house") + v.CountPendingSites("peasant_house") + 1));
            }

            if (village.FoodStock <= Math.Max(2f, population))
            {
                Add(contracts, village, "food", "Stabilize food",
                    "Steward makes a farm plot the top village priority.",
                    baseCost,
                    village.Scheduler.HasOpenBuildGoal("farm_plot"),
                    v => v.Scheduler.AddBuildGoal("farm_plot", 18, "Contract: build Farm Plot", v.CountCompletedBuildings("farm_plot") + v.CountPendingSites("farm_plot") + 1));
            }

            if (!village.HasBuilding(BuildingKind.Storage) && population >= 3)
            {
                Add(contracts, village, "storage", "Fix logistics",
                    "Steward prioritizes storage so large projects stop jamming.",
                    baseCost + 1,
                    village.Scheduler.HasOpenBuildGoal("storage_crate"),
                    v => v.Scheduler.AddBuildGoal("storage_crate", 14, "Contract: build Storage Crate", v.CountCompletedBuildings("storage_crate") + v.CountPendingSites("storage_crate") + 1));
            }

            if (!village.HasBuilding(BuildingKind.Market) && population >= 3)
            {
                Add(contracts, village, "market", "Build market",
                    "Steward commissions a market stall to trade surplus goods.",
                    baseCost + 1,
                    village.Scheduler.HasOpenBuildGoal("market"),
                    v => v.Scheduler.AddBuildGoal("market", 15, "Contract: build Market Stall", v.CountCompletedBuildings("market") + v.CountPendingSites("market") + 1));
            }

            if (!village.HasBuilding(BuildingKind.LumberCamp) && population >= 3)
            {
                Add(contracts, village, "lumber_camp", "Build lumber camp",
                    "Steward commissions a lumber camp to gather wood resources autonomously.",
                    baseCost + 1,
                    village.Scheduler.HasOpenBuildGoal("lumber_camp"),
                    v => v.Scheduler.AddBuildGoal("lumber_camp", 12, "Contract: build Lumber Camp", v.CountCompletedBuildings("lumber_camp") + v.CountPendingSites("lumber_camp") + 1));
            }

            if (!village.HasBuilding(BuildingKind.Quarry) && population >= 3)
            {
                Add(contracts, village, "quarry", "Build quarry",
                    "Steward commissions a quarry to mine stone resources autonomously.",
                    baseCost + 1,
                    village.Scheduler.HasOpenBuildGoal("quarry"),
                    v => v.Scheduler.AddBuildGoal("quarry", 13, "Contract: build Quarry", v.CountCompletedBuildings("quarry") + v.CountPendingSites("quarry") + 1));
            }

            if (!village.HasBuilding(BuildingKind.Workshop) && population >= 4)
            {
                Add(contracts, village, "workshop", "Prepare agents",
                    "Steward prioritizes a workshop for tools and deeper delegation.",
                    baseCost + 2,
                    village.Scheduler.HasOpenBuildGoal("workshop"),
                    v => v.Scheduler.AddBuildGoal("workshop", 16, "Contract: build Workshop", v.CountCompletedBuildings("workshop") + v.CountPendingSites("workshop") + 1));
            }

            Add(contracts, village, "planks", "Stock planks",
                "Steward maintains a reserve for homes, tools, and recruits.",
                Math.Max(3, baseCost - 2),
                village.Scheduler.HasOpenStockGoal(BlockType.OakPlank, 64),
                v => v.Scheduler.AddStockGoal(BlockType.OakPlank, 64, 10, "Contract: stock 64 OakPlank"));

            if (village.HasBuilding(BuildingKind.Market))
            {
                if (village.Storage.CountBlock(BlockType.OakLog) >= 128)
                {
                    Add(contracts, village, "trade_lumber", "Trade surplus lumber",
                        "Steward exports 64 Oak Logs to neighboring towns for 10 Favor.",
                        0,
                        false,
                        v => {
                            if (v.Storage.TryConsumeBlock(BlockType.OakLog, 64))
                            {
                                v.AddFavor(10);
                            }
                        });
                }

                if (village.Storage.CountBlock(BlockType.Stone) >= 128)
                {
                    Add(contracts, village, "trade_stone", "Trade surplus stone",
                        "Steward exports 64 Stone to neighboring towns for 10 Favor.",
                        0,
                        false,
                        v => {
                            if (v.Storage.TryConsumeBlock(BlockType.Stone, 64))
                            {
                                v.AddFavor(10);
                            }
                        });
                }
                
                if (village.Storage.CountBlock(BlockType.OakPlank) >= 128)
                {
                    Add(contracts, village, "trade_planks", "Trade surplus planks",
                        "Steward exports 64 Oak Planks to neighboring towns for 15 Favor.",
                        0,
                        false,
                        v => {
                            if (v.Storage.TryConsumeBlock(BlockType.OakPlank, 64))
                            {
                                v.AddFavor(15);
                            }
                        });
                }
            }

            return contracts;
        }

        public static bool TryAccept(Village village, VillagerManager villagers, string contractId, out string message)
        {
            foreach (var contract in Suggest(village, villagers))
            {
                if (!string.Equals(contract.Id, contractId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (contract.AlreadyActive)
                {
                    message = $"{contract.Label} is already queued.";
                    return false;
                }

                if (!village.TrySpendFavor(contract.FavorCost))
                {
                    message = $"Need {contract.FavorCost} favor for {contract.Label}.";
                    return false;
                }

                contract.Apply?.Invoke(village);
                message = $"{contract.Label} accepted for {contract.FavorCost} favor.";
                return true;
            }

            message = "Contract no longer available.";
            return false;
        }

        private static void Add(
            List<VillageAgentContract> contracts,
            Village village,
            string id,
            string label,
            string description,
            int favorCost,
            bool alreadyActive,
            Action<Village> apply)
        {
            string status = alreadyActive
                ? "Queued"
                : village.Favor >= favorCost
                    ? $"{favorCost} favor"
                    : $"Need {favorCost - village.Favor} more";

            contracts.Add(new VillageAgentContract
            {
                Id = id,
                Label = label,
                Description = description,
                FavorCost = favorCost,
                CanAfford = village.Favor >= favorCost,
                AlreadyActive = alreadyActive,
                StatusText = status,
                Apply = apply
            });
        }
    }
}

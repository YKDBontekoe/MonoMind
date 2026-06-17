using System.Collections.Generic;
using Autonocraft.Domain.Village;
using Autonocraft.World;

namespace Autonocraft.Village
{
    public sealed class VillageEconomy
    {
        private readonly Dictionary<BlockType, int> _demand = new();
        private readonly Dictionary<BlockType, int> _supply = new();

        public void Clear()
        {
            _demand.Clear();
            _supply.Clear();
        }

        public void RecordDemand(BlockType blockType, int amount = 1)
        {
            _demand.TryGetValue(blockType, out int current);
            _demand[blockType] = current + amount;
        }

        public void RecordSupply(BlockType blockType, int amount = 1)
        {
            _supply.TryGetValue(blockType, out int current);
            _supply[blockType] = current + amount;
        }

        public BlockType? GetHighestDemandBlock()
        {
            BlockType? best = null;
            int bestGap = 0;
            foreach (var pair in _demand)
            {
                _supply.TryGetValue(pair.Key, out int have);
                int gap = pair.Value - have;
                if (gap > bestGap)
                {
                    bestGap = gap;
                    best = pair.Key;
                }
            }

            return best;
        }

        public void SyncFromStorage(VillageStorage storage)
        {
            _supply.Clear();
            for (int i = 0; i < storage.SlotCount; i++)
            {
                var stack = storage.GetSlot(i);
                if (stack.IsBlock())
                {
                    RecordSupply(stack.BlockType, stack.Count);
                }
            }
        }
    }

    public static class VillageTierProgression
    {
        public static readonly (VillageTier Tier, int MinPopulation, int MinHouses, float MinFoodSurplus)[] Thresholds =
        {
            (VillageTier.Hamlet, 0, 0, 0f),
            (VillageTier.Village, 5, 2, 4f),
            (VillageTier.Town, 13, 4, 12f)
        };

        public static VillageTier EvaluateTier(Village village)
        {
            VillageTier result = VillageTier.Hamlet;
            int houses = village.CountBuildings(BuildingKind.House);
            float surplus = village.FoodStock - village.Population;

            foreach (var (tier, minPop, minHouses, minFood) in Thresholds)
            {
                if (village.Population >= minPop && houses >= minHouses && surplus >= minFood)
                {
                    result = tier;
                }
            }

            return result;
        }
    }
}

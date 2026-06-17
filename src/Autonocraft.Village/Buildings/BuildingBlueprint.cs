using System;
using Autonocraft.Domain.Village;
using Autonocraft.Items;
using Autonocraft.World;
using Autonocraft.World.Structures;

namespace Autonocraft.Village
{
    public sealed class BuildingBlueprint
    {
        public string Id { get; init; } = string.Empty;
        public BuildingKind Kind { get; init; }
        public StructureTemplate Template { get; init; } = new StructureTemplate();
        public BlockCost[] Costs { get; init; } = Array.Empty<BlockCost>();
        public int HousingProvided { get; init; }
        public int PopulationCapBonus { get; init; }
        public int StorageSlots { get; init; }
        public string DisplayName { get; init; } = string.Empty;

        public bool CanAfford(IItemContainer container)
        {
            foreach (var cost in Costs)
            {
                if (container.CountBlock(cost.BlockType) < cost.Count)
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryConsumeCosts(IItemContainer container)
        {
            if (!CanAfford(container))
            {
                return false;
            }

            foreach (var cost in Costs)
            {
                container.TryConsumeBlock(cost.BlockType, cost.Count);
            }

            return true;
        }
    }

    public readonly struct BlockCost
    {
        public BlockCost(BlockType blockType, int count)
        {
            BlockType = blockType;
            Count = count;
        }

        public BlockType BlockType { get; }
        public int Count { get; }
    }
}

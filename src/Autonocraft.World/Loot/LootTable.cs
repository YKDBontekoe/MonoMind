using Autonocraft.Domain.Items;
using Autonocraft.Domain.World;
using Autonocraft.Items;

namespace Autonocraft.World.Loot
{
    public enum LootRewardKind : byte
    {
        Block,
        Food,
        Tool
    }

    public sealed class LootEntry
    {
        public LootRarity Rarity { get; init; }
        public int Weight { get; init; }
        public LootRewardKind Kind { get; init; }
        public BlockType BlockType { get; init; }
        public ItemId ItemId { get; init; }
        public int MinCount { get; init; } = 1;
        public int MaxCount { get; init; } = 1;
        public float DurabilityFraction { get; init; } = 1f;
    }

    public sealed class LootTable
    {
        public string Id { get; init; } = string.Empty;
        public int MinRolls { get; init; } = 1;
        public int MaxRolls { get; init; } = 2;
        public LootEntry[] Entries { get; init; } = Array.Empty<LootEntry>();
    }
}

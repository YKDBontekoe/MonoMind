using Autonocraft.Domain.Items;
using Autonocraft.Domain.World;
using Autonocraft.Items;
using Autonocraft.World.Structures;

namespace Autonocraft.World.Loot
{
    public static class LootRoller
    {
        public static List<ItemStack> Roll(string tableId, int seed)
        {
            var table = LootTableRegistry.Get(tableId);
            var rng = new StructureRng(seed);
            int rolls = rng.Range(table.MinRolls, table.MaxRolls + 1);
            var results = new List<ItemStack>(rolls);

            for (int i = 0; i < rolls; i++)
            {
                var entry = PickEntry(table, rng);
                if (entry == null)
                {
                    continue;
                }

                var stack = CreateStack(entry, rng);
                if (!stack.IsEmpty)
                {
                    results.Add(stack);
                }
            }

            return results;
        }

        public static LootRarity? PeekHighestRarity(IReadOnlyList<ItemStack> stacks)
        {
            LootRarity? best = null;
            foreach (var stack in stacks)
            {
                var rarity = DescribeStack(stack);
                if (rarity == null)
                {
                    continue;
                }

                if (best == null || rarity > best)
                {
                    best = rarity;
                }
            }

            return best;
        }

        public static LootRarity? DescribeStack(ItemStack stack)
        {
            if (stack.Kind == ItemKind.Tool && ToolRegistry.TryGet(stack.ToolId, out var tool))
            {
                return tool.Tier switch
                {
                    ToolTier.Wood or ToolTier.Stone => LootRarity.Common,
                    ToolTier.Iron or ToolTier.Copper => LootRarity.Uncommon,
                    ToolTier.Gold or ToolTier.Silver => LootRarity.Rare,
                    ToolTier.Diamond or ToolTier.Emerald => LootRarity.Epic,
                    ToolTier.Relic => LootRarity.Legendary,
                    _ => LootRarity.Common
                };
            }

            if (stack.Kind == ItemKind.Block)
            {
                return stack.BlockType switch
                {
                    BlockType.GoldBlock or BlockType.DiamondBlock or BlockType.EmeraldBlock or BlockType.RubyBlock
                        => LootRarity.Rare,
                    BlockType.Obsidian or BlockType.Amethyst => LootRarity.Epic,
                    _ => LootRarity.Common
                };
            }

            return LootRarity.Common;
        }

        private static LootEntry? PickEntry(LootTable table, StructureRng rng)
        {
            int total = 0;
            foreach (var entry in table.Entries)
            {
                total += entry.Weight;
            }

            if (total <= 0)
            {
                return null;
            }

            int roll = rng.NextInt(total);
            foreach (var entry in table.Entries)
            {
                roll -= entry.Weight;
                if (roll < 0)
                {
                    return entry;
                }
            }

            return table.Entries[^1];
        }

        private static ItemStack CreateStack(LootEntry entry, StructureRng rng)
        {
            int count = entry.MinCount == entry.MaxCount
                ? entry.MinCount
                : rng.Range(entry.MinCount, entry.MaxCount + 1);

            return entry.Kind switch
            {
                LootRewardKind.Block => ItemStack.CreateBlock(entry.BlockType, count),
                LootRewardKind.Food => ItemStack.CreateFood(entry.ItemId, count),
                LootRewardKind.Tool => CreateToolStack(entry),
                _ => ItemStack.Empty
            };
        }

        private static ItemStack CreateToolStack(LootEntry entry)
        {
            var def = ToolRegistry.Get(entry.ItemId);
            int durability = Math.Max(1, (int)MathF.Round(def.MaxDurability * entry.DurabilityFraction));
            return ItemStack.CreateTool(entry.ItemId, durability);
        }
    }
}

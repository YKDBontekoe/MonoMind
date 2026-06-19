using Autonocraft.Domain.Items;
using Autonocraft.Domain.World;
using Autonocraft.Items;

namespace Autonocraft.World.Loot
{
    public static class LootTableRegistry
    {
        private static readonly Dictionary<string, LootTable> Tables = BuildTables();

        public static LootTable Get(string id)
        {
            if (!Tables.TryGetValue(id, out var table))
            {
                return Tables[LootTableIds.Small];
            }

            return table;
        }

        private static Dictionary<string, LootTable> BuildTables()
        {
            return new Dictionary<string, LootTable>(StringComparer.Ordinal)
            {
                [LootTableIds.Small] = new LootTable
                {
                    Id = LootTableIds.Small,
                    MinRolls = 1,
                    MaxRolls = 2,
                    Entries = new[]
                    {
                        Entry(LootRarity.Common, 40, BlockType.Cobblestone, 2, 8),
                        Entry(LootRarity.Common, 35, BlockType.OakPlank, 2, 6),
                        Entry(LootRarity.Common, 25, BlockType.HayBale, 1, 2),
                        Entry(LootRarity.Uncommon, 12, BlockType.IronOre, 1, 3),
                        Entry(LootRarity.Uncommon, 10, ItemId.Bread, 1, 2),
                        Entry(LootRarity.Rare, 4, ItemId.IronPickaxe),
                        Entry(LootRarity.Rare, 4, ItemId.IronAxe)
                    }
                },
                [LootTableIds.Medium] = new LootTable
                {
                    Id = LootTableIds.Medium,
                    MinRolls = 2,
                    MaxRolls = 3,
                    Entries = new[]
                    {
                        Entry(LootRarity.Common, 30, BlockType.Cobblestone, 4, 12),
                        Entry(LootRarity.Common, 25, BlockType.Brick, 2, 8),
                        Entry(LootRarity.Uncommon, 18, BlockType.IronBlock, 1, 2),
                        Entry(LootRarity.Uncommon, 15, ItemId.CookedMeat, 1, 3),
                        Entry(LootRarity.Rare, 8, ItemId.GoldPickaxe),
                        Entry(LootRarity.Rare, 8, ItemId.GoldSword),
                        Entry(LootRarity.Epic, 3, ItemId.CopperPickaxe),
                        Entry(LootRarity.Epic, 3, ItemId.CopperSword)
                    }
                },
                [LootTableIds.Treasury] = new LootTable
                {
                    Id = LootTableIds.Treasury,
                    MinRolls = 2,
                    MaxRolls = 4,
                    Entries = new[]
                    {
                        Entry(LootRarity.Common, 20, BlockType.GoldBlock, 1, 2),
                        Entry(LootRarity.Uncommon, 18, BlockType.IronBlock, 2, 4),
                        Entry(LootRarity.Uncommon, 15, BlockType.RubyBlock, 1, 2),
                        Entry(LootRarity.Rare, 10, ItemId.GoldPickaxe),
                        Entry(LootRarity.Rare, 10, ItemId.GoldSword),
                        Entry(LootRarity.Epic, 5, ItemId.SilverPickaxe),
                        Entry(LootRarity.Epic, 5, ItemId.SilverSword),
                        Entry(LootRarity.Legendary, 2, ItemId.DiamondPickaxe, durability: 0.85f)
                    }
                },
                [LootTableIds.Dungeon] = new LootTable
                {
                    Id = LootTableIds.Dungeon,
                    MinRolls = 2,
                    MaxRolls = 4,
                    Entries = new[]
                    {
                        Entry(LootRarity.Common, 25, BlockType.MossStone, 3, 10),
                        Entry(LootRarity.Uncommon, 18, BlockType.Obsidian, 1, 3),
                        Entry(LootRarity.Uncommon, 15, ItemId.RawMeat, 2, 4),
                        Entry(LootRarity.Rare, 10, ItemId.IronSword),
                        Entry(LootRarity.Rare, 8, BlockType.Amethyst, 1, 2),
                        Entry(LootRarity.Epic, 5, ItemId.SilverPickaxe),
                        Entry(LootRarity.Epic, 4, ItemId.EmeraldSword, durability: 0.75f),
                        Entry(LootRarity.Legendary, 2, ItemId.RelicShovel, durability: 0.9f)
                    }
                },
                [LootTableIds.Castle] = new LootTable
                {
                    Id = LootTableIds.Castle,
                    MinRolls = 3,
                    MaxRolls = 5,
                    Entries = new[]
                    {
                        Entry(LootRarity.Common, 18, BlockType.Brick, 4, 12),
                        Entry(LootRarity.Uncommon, 16, BlockType.GoldBlock, 1, 3),
                        Entry(LootRarity.Rare, 12, ItemId.GoldPickaxe),
                        Entry(LootRarity.Rare, 10, ItemId.GoldAxe),
                        Entry(LootRarity.Epic, 7, ItemId.DiamondPickaxe, durability: 0.8f),
                        Entry(LootRarity.Epic, 6, ItemId.DiamondSword, durability: 0.8f),
                        Entry(LootRarity.Legendary, 3, ItemId.RelicPickaxe, durability: 0.95f),
                        Entry(LootRarity.Legendary, 2, ItemId.RelicSword, durability: 0.95f)
                    }
                },
                [LootTableIds.Citadel] = new LootTable
                {
                    Id = LootTableIds.Citadel,
                    MinRolls = 4,
                    MaxRolls = 6,
                    Entries = new[]
                    {
                        Entry(LootRarity.Common, 12, BlockType.Obsidian, 2, 6),
                        Entry(LootRarity.Uncommon, 14, BlockType.EmeraldBlock, 1, 2),
                        Entry(LootRarity.Rare, 12, ItemId.DiamondPickaxe, durability: 0.85f),
                        Entry(LootRarity.Epic, 8, ItemId.EmeraldSword, durability: 0.9f),
                        Entry(LootRarity.Epic, 7, ItemId.EmeraldPickaxe, durability: 0.9f),
                        Entry(LootRarity.Legendary, 4, ItemId.RelicPickaxe),
                        Entry(LootRarity.Legendary, 4, ItemId.RelicAxe),
                        Entry(LootRarity.Legendary, 3, ItemId.RelicSword),
                        Entry(LootRarity.Legendary, 2, ItemId.RelicShovel)
                    }
                }
            };
        }

        private static LootEntry Entry(
            LootRarity rarity,
            int weight,
            BlockType block,
            int minCount = 1,
            int maxCount = 1)
        {
            return new LootEntry
            {
                Rarity = rarity,
                Weight = weight,
                Kind = LootRewardKind.Block,
                BlockType = block,
                MinCount = minCount,
                MaxCount = maxCount
            };
        }

        private static LootEntry Entry(
            LootRarity rarity,
            int weight,
            ItemId foodId,
            int minCount,
            int maxCount)
        {
            return new LootEntry
            {
                Rarity = rarity,
                Weight = weight,
                Kind = LootRewardKind.Food,
                ItemId = foodId,
                MinCount = minCount,
                MaxCount = maxCount
            };
        }

        private static LootEntry Entry(
            LootRarity rarity,
            int weight,
            ItemId toolId,
            float durability = 1f)
        {
            return new LootEntry
            {
                Rarity = rarity,
                Weight = weight,
                Kind = LootRewardKind.Tool,
                ItemId = toolId,
                MinCount = 1,
                MaxCount = 1,
                DurabilityFraction = durability
            };
        }
    }
}

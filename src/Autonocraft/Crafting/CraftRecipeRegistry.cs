using Autonocraft.Domain.Core;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Crafting
{
    public static class CraftRecipeRegistry
    {
        public static IReadOnlyList<CraftRecipe> All { get; } = BuildAll();

        public static IReadOnlyList<CraftRecipe> ForStation(BlockType stationType)
        {
            return All.Where(r => r.StationType == stationType).ToList();
        }

        public static IEnumerable<CraftRecipe> AvailableForStation(BlockType stationType, DiscoveryJournal journal)
        {
            return ForStation(stationType).Where(r => !r.RequiresUnlock || journal.IsUnlocked(r.Id));
        }

        private static IReadOnlyList<CraftRecipe> BuildAll()
        {
            return new[]
            {
                new CraftRecipe
                {
                    Id = "recipe:plank",
                    DisplayName = "Timber Split",
                    StationType = BlockType.StationBench,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.OakLog, Count = 1 } },
                    Output = BlockType.OakPlank,
                    OutputCount = 2
                },
                new CraftRecipe
                {
                    Id = "recipe:sandstone",
                    DisplayName = "Sand Compression",
                    StationType = BlockType.StationBench,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.Sand, Count = 3 } },
                    Output = BlockType.Sandstone,
                    OutputCount = 1
                },
                ToolRecipe("recipe:wood_pickaxe", "Wood Pickaxe", BlockType.StationBench,
                    new[] { new CraftInput { ExactBlock = BlockType.OakPlank, Count = 2 }, new CraftInput { Tag = MaterialTag.Wood, Count = 1 } },
                    ItemId.WoodPickaxe),
                ToolRecipe("recipe:wood_axe", "Wood Axe", BlockType.StationBench,
                    new[] { new CraftInput { ExactBlock = BlockType.OakPlank, Count = 2 }, new CraftInput { Tag = MaterialTag.Wood, Count = 1 } },
                    ItemId.WoodAxe),
                ToolRecipe("recipe:wood_shovel", "Wood Shovel", BlockType.StationBench,
                    new[] { new CraftInput { ExactBlock = BlockType.OakPlank, Count = 2 }, new CraftInput { Tag = MaterialTag.Wood, Count = 1 } },
                    ItemId.WoodShovel),
                ToolRecipe("recipe:stone_pickaxe", "Stone Pickaxe", BlockType.StationBench,
                    new[] { new CraftInput { ExactBlock = BlockType.OakPlank, Count = 2 }, new CraftInput { ExactBlock = BlockType.Stone, Count = 3 } },
                    ItemId.StonePickaxe, requiresUnlock: true),
                ToolRecipe("recipe:stone_axe", "Stone Axe", BlockType.StationBench,
                    new[] { new CraftInput { ExactBlock = BlockType.OakPlank, Count = 2 }, new CraftInput { ExactBlock = BlockType.Stone, Count = 3 } },
                    ItemId.StoneAxe, requiresUnlock: true),
                ToolRecipe("recipe:stone_shovel", "Stone Shovel", BlockType.StationBench,
                    new[] { new CraftInput { ExactBlock = BlockType.OakPlank, Count = 2 }, new CraftInput { ExactBlock = BlockType.Stone, Count = 3 } },
                    ItemId.StoneShovel, requiresUnlock: true),
                new CraftRecipe
                {
                    Id = "recipe:iron_block",
                    DisplayName = "Iron Smelt",
                    StationType = BlockType.StationForge,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.IronOre, Count = 1 } },
                    Output = BlockType.IronBlock,
                    OutputCount = 1,
                    RequiresHeat = true
                },
                new CraftRecipe
                {
                    Id = "recipe:glass",
                    DisplayName = "Sand Melt",
                    StationType = BlockType.StationForge,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.Sand, Count = 1 } },
                    Output = BlockType.Glass,
                    OutputCount = 1,
                    RequiresHeat = true
                },
                new CraftRecipe
                {
                    Id = "recipe:gold_block",
                    DisplayName = "Gold Refine",
                    StationType = BlockType.StationForge,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.GoldOre, Count = 1 } },
                    Output = BlockType.GoldBlock,
                    OutputCount = 1,
                    RequiresHeat = true,
                    RequiredTimePhase = TimePhase.Night
                },
                ToolRecipe("recipe:iron_pickaxe", "Iron Pickaxe", BlockType.StationForge,
                    new[] { new CraftInput { ExactBlock = BlockType.IronBlock, Count = 1 }, new CraftInput { ExactBlock = BlockType.OakPlank, Count = 2 } },
                    ItemId.IronPickaxe, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:iron_axe", "Iron Axe", BlockType.StationForge,
                    new[] { new CraftInput { ExactBlock = BlockType.IronBlock, Count = 1 }, new CraftInput { ExactBlock = BlockType.OakPlank, Count = 2 } },
                    ItemId.IronAxe, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:iron_shovel", "Iron Shovel", BlockType.StationForge,
                    new[] { new CraftInput { ExactBlock = BlockType.IronBlock, Count = 1 }, new CraftInput { ExactBlock = BlockType.OakPlank, Count = 2 } },
                    ItemId.IronShovel, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:iron_sword", "Iron Sword", BlockType.StationForge,
                    new[] { new CraftInput { ExactBlock = BlockType.IronBlock, Count = 1 }, new CraftInput { ExactBlock = BlockType.OakPlank, Count = 2 } },
                    ItemId.IronSword, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:gold_sword", "Gold Sword", BlockType.StationForge,
                    new[] { new CraftInput { ExactBlock = BlockType.GoldBlock, Count = 1 }, new CraftInput { ExactBlock = BlockType.OakPlank, Count = 2 } },
                    ItemId.GoldSword, requiresUnlock: true, requiresHeat: true, requiredTimePhase: TimePhase.Night),
                new CraftRecipe
                {
                    Id = "recipe:birch_plank",
                    DisplayName = "Birch Split",
                    StationType = BlockType.StationBench,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.BirchLog, Count = 1 } },
                    Output = BlockType.BirchPlank,
                    OutputCount = 2
                },
                new CraftRecipe
                {
                    Id = "recipe:pine_plank",
                    DisplayName = "Pine Split",
                    StationType = BlockType.StationBench,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.PineLog, Count = 1 } },
                    Output = BlockType.PinePlank,
                    OutputCount = 2
                },
                new CraftRecipe
                {
                    Id = "recipe:cobblestone",
                    DisplayName = "Cobble Cut",
                    StationType = BlockType.StationBench,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.Stone, Count = 1 } },
                    Output = BlockType.Cobblestone,
                    OutputCount = 1
                },
                new CraftRecipe
                {
                    Id = "recipe:brick",
                    DisplayName = "Clay Brick",
                    StationType = BlockType.StationForge,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.Clay, Count = 1 } },
                    Output = BlockType.Brick,
                    OutputCount = 1,
                    RequiresHeat = true
                },
                new CraftRecipe
                {
                    Id = "recipe:moss_stone",
                    DisplayName = "Moss Bind",
                    StationType = BlockType.StationBench,
                    Inputs = new[]
                    {
                        new CraftInput { ExactBlock = BlockType.Cobblestone, Count = 1 },
                        new CraftInput { Tag = MaterialTag.Organic, Count = 1 }
                    },
                    Output = BlockType.MossStone,
                    OutputCount = 1,
                    RequiresUnlock = true
                },
                new CraftRecipe
                {
                    Id = "recipe:grass",
                    DisplayName = "Soil Enrich",
                    StationType = BlockType.StationCrucible,
                    Inputs = new[]
                    {
                        new CraftInput { ExactBlock = BlockType.Dirt, Count = 1 },
                        new CraftInput { Tag = MaterialTag.Organic, Count = 1 }
                    },
                    Output = BlockType.Grass,
                    OutputCount = 1,
                    RequiresWater = true,
                    AllowedBiomes = new[] { BiomeType.Plains, BiomeType.Forest }
                },
                new CraftRecipe
                {
                    Id = "recipe:clay",
                    DisplayName = "Clay Settle",
                    StationType = BlockType.StationCrucible,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.Gravel, Count = 2 } },
                    Output = BlockType.Clay,
                    OutputCount = 1,
                    RequiresWater = true
                },
                new CraftRecipe
                {
                    Id = "recipe:sand_extract",
                    DisplayName = "Desert Fiber",
                    StationType = BlockType.StationCrucible,
                    Inputs = new[]
                    {
                        new CraftInput { ExactBlock = BlockType.Cactus, Count = 1 },
                        new CraftInput { ExactBlock = BlockType.Sand, Count = 1 }
                    },
                    Output = BlockType.Sand,
                    OutputCount = 2,
                    RequiresWater = true,
                    RequiredBiome = BiomeType.Desert
                },
                new CraftRecipe
                {
                    Id = "recipe:cooked_meat",
                    DisplayName = "Roast Meat",
                    StationType = BlockType.StationCrucible,
                    Inputs = new[] { new CraftInput { Tag = MaterialTag.Organic, Count = 2 } },
                    OutputKind = ItemKind.Consumable,
                    OutputItem = ItemId.CookedMeat,
                    OutputCount = 1,
                    RequiresHeat = true,
                    RequiresUnlock = true
                }
            };
        }

        private static CraftRecipe ToolRecipe(
            string id,
            string displayName,
            BlockType station,
            CraftInput[] inputs,
            ItemId outputItem,
            bool requiresUnlock = false,
            bool requiresHeat = false,
            TimePhase? requiredTimePhase = null)
        {
            return new CraftRecipe
            {
                Id = id,
                DisplayName = displayName,
                StationType = station,
                Inputs = inputs,
                OutputKind = ItemKind.Tool,
                OutputItem = outputItem,
                OutputCount = 1,
                RequiresUnlock = requiresUnlock,
                RequiresHeat = requiresHeat,
                RequiredTimePhase = requiredTimePhase
            };
        }
    }
}

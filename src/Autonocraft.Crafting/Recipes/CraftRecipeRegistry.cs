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
                    Id = "recipe:sticks",
                    DisplayName = "Sticks",
                    StationType = BlockType.StationBench,
                    ShapedPattern = new[] { "P", "P" },
                    GridSize = CraftGridSize.TwoByTwo,
                    OutputKind = ItemKind.Material,
                    OutputItem = ItemId.Stick,
                    OutputCount = 4
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
                    WoodPickPattern(), ItemId.WoodPickaxe),
                ToolRecipe("recipe:wood_axe", "Wood Axe", BlockType.StationBench,
                    WoodAxePattern(), ItemId.WoodAxe),
                ToolRecipe("recipe:wood_shovel", "Wood Shovel", BlockType.StationBench,
                    WoodShovelPattern(), ItemId.WoodShovel),
                ToolRecipe("recipe:wood_sword", "Wood Sword", BlockType.StationBench,
                    WoodSwordPattern(), ItemId.WoodSword),
                ToolRecipe("recipe:stone_pickaxe", "Stone Pickaxe", BlockType.StationBench,
                    StonePickPattern(), ItemId.StonePickaxe, requiresUnlock: true),
                ToolRecipe("recipe:stone_axe", "Stone Axe", BlockType.StationBench,
                    StoneAxePattern(), ItemId.StoneAxe, requiresUnlock: true),
                ToolRecipe("recipe:stone_shovel", "Stone Shovel", BlockType.StationBench,
                    StoneShovelPattern(), ItemId.StoneShovel, requiresUnlock: true),
                ToolRecipe("recipe:stone_sword", "Stone Sword", BlockType.StationBench,
                    StoneSwordPattern(), ItemId.StoneSword, requiresUnlock: true),
                new CraftRecipe
                {
                    Id = "recipe:bread",
                    DisplayName = "Bake Bread",
                    StationType = BlockType.StationBench,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.Wheat, Count = 2 } },
                    OutputKind = ItemKind.Food,
                    OutputItem = ItemId.Bread,
                    OutputCount = 1,
                    RequiresUnlock = true
                },
                FoodRecipe("recipe:cooked_meat", "Cook Meat", BlockType.StationForge,
                    ItemId.RawMeat, ItemId.CookedMeat, requiresHeat: true),
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
                    IronPickPattern(), ItemId.IronPickaxe, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:iron_axe", "Iron Axe", BlockType.StationForge,
                    IronAxePattern(), ItemId.IronAxe, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:iron_shovel", "Iron Shovel", BlockType.StationForge,
                    IronShovelPattern(), ItemId.IronShovel, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:iron_sword", "Iron Sword", BlockType.StationForge,
                    IronSwordPattern(), ItemId.IronSword, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:gold_sword", "Gold Sword", BlockType.StationForge,
                    GoldSwordPattern(), ItemId.GoldSword, requiresUnlock: true, requiresHeat: true, requiredTimePhase: TimePhase.Night),
                ToolRecipe("recipe:gold_pickaxe", "Gold Pickaxe", BlockType.StationForge,
                    GoldPickPattern(), ItemId.GoldPickaxe, requiresUnlock: true, requiresHeat: true, requiredTimePhase: TimePhase.Night),
                ToolRecipe("recipe:gold_axe", "Gold Axe", BlockType.StationForge,
                    GoldAxePattern(), ItemId.GoldAxe, requiresUnlock: true, requiresHeat: true, requiredTimePhase: TimePhase.Night),
                ToolRecipe("recipe:gold_shovel", "Gold Shovel", BlockType.StationForge,
                    GoldShovelPattern(), ItemId.GoldShovel, requiresUnlock: true, requiresHeat: true, requiredTimePhase: TimePhase.Night),
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
                    AllowedBiomes = new[] { BiomeType.Plains, BiomeType.Forest, BiomeType.Jungle }
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
                    Id = "recipe:cherry_plank",
                    DisplayName = "Cherry Split",
                    StationType = BlockType.StationBench,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.CherryLog, Count = 1 } },
                    Output = BlockType.CherryPlank,
                    OutputCount = 2
                },
                new CraftRecipe
                {
                    Id = "recipe:mahogany_plank",
                    DisplayName = "Mahogany Split",
                    StationType = BlockType.StationBench,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.MahoganyLog, Count = 1 } },
                    Output = BlockType.MahoganyPlank,
                    OutputCount = 2
                },
                new CraftRecipe
                {
                    Id = "recipe:maple_plank",
                    DisplayName = "Maple Split",
                    StationType = BlockType.StationBench,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.MapleLog, Count = 1 } },
                    Output = BlockType.MaplePlank,
                    OutputCount = 2
                },
                new CraftRecipe
                {
                    Id = "recipe:copper_block",
                    DisplayName = "Copper Smelt",
                    StationType = BlockType.StationForge,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.CopperOre, Count = 1 } },
                    Output = BlockType.CopperBlock,
                    OutputCount = 1,
                    RequiresHeat = true
                },
                new CraftRecipe
                {
                    Id = "recipe:silver_block",
                    DisplayName = "Silver Refine",
                    StationType = BlockType.StationForge,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.SilverOre, Count = 1 } },
                    Output = BlockType.SilverBlock,
                    OutputCount = 1,
                    RequiresHeat = true
                },
                new CraftRecipe
                {
                    Id = "recipe:diamond_block",
                    DisplayName = "Diamond Compress",
                    StationType = BlockType.StationForge,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.DiamondOre, Count = 1 } },
                    Output = BlockType.DiamondBlock,
                    OutputCount = 1,
                    RequiresHeat = true
                },
                new CraftRecipe
                {
                    Id = "recipe:emerald_block",
                    DisplayName = "Emerald Refine",
                    StationType = BlockType.StationForge,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.EmeraldOre, Count = 1 } },
                    Output = BlockType.EmeraldBlock,
                    OutputCount = 1,
                    RequiresHeat = true
                },
                new CraftRecipe
                {
                    Id = "recipe:ruby_block",
                    DisplayName = "Ruby Refine",
                    StationType = BlockType.StationForge,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.RubyOre, Count = 1 } },
                    Output = BlockType.RubyBlock,
                    OutputCount = 1,
                    RequiresHeat = true
                },
                new CraftRecipe
                {
                    Id = "recipe:quartz_block",
                    DisplayName = "Quartz Refine",
                    StationType = BlockType.StationForge,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.QuartzOre, Count = 1 } },
                    Output = BlockType.QuartzBlock,
                    OutputCount = 1,
                    RequiresHeat = true
                },
                new CraftRecipe
                {
                    Id = "recipe:station_smoker",
                    DisplayName = "Smoker",
                    StationType = BlockType.StationBench,
                    Inputs = new[]
                    {
                        new CraftInput { ExactBlock = BlockType.Cobblestone, Count = 4 },
                        new CraftInput { Tag = MaterialTag.Wood, Count = 2 }
                    },
                    Output = BlockType.StationSmoker,
                    OutputCount = 1
                },
                new CraftRecipe
                {
                    Id = "recipe:station_stonecutter",
                    DisplayName = "Stonecutter",
                    StationType = BlockType.StationBench,
                    Inputs = new[]
                    {
                        new CraftInput { ExactBlock = BlockType.Cobblestone, Count = 3 },
                        new CraftInput { ExactBlock = BlockType.IronBlock, Count = 1 }
                    },
                    Output = BlockType.StationStonecutter,
                    OutputCount = 1
                },
                new CraftRecipe
                {
                    Id = "recipe:marble_brick",
                    DisplayName = "Marble Brick",
                    StationType = BlockType.StationStonecutter,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.Marble, Count = 1 } },
                    Output = BlockType.MarbleBrick,
                    OutputCount = 1
                },
                new CraftRecipe
                {
                    Id = "recipe:basalt_brick",
                    DisplayName = "Basalt Brick",
                    StationType = BlockType.StationStonecutter,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.Basalt, Count = 1 } },
                    Output = BlockType.BasaltBrick,
                    OutputCount = 1
                },
                new CraftRecipe
                {
                    Id = "recipe:slate_brick",
                    DisplayName = "Slate Brick",
                    StationType = BlockType.StationStonecutter,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.Slate, Count = 1 } },
                    Output = BlockType.SlateBrick,
                    OutputCount = 1
                },
                new CraftRecipe
                {
                    Id = "recipe:polished_marble",
                    DisplayName = "Polished Marble",
                    StationType = BlockType.StationStonecutter,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.Marble, Count = 1 } },
                    Output = BlockType.PolishedMarble,
                    OutputCount = 1
                },
                new CraftRecipe
                {
                    Id = "recipe:polished_granite",
                    DisplayName = "Polished Granite",
                    StationType = BlockType.StationStonecutter,
                    Inputs = new[] { new CraftInput { ExactBlock = BlockType.Granite, Count = 1 } },
                    Output = BlockType.PolishedGranite,
                    OutputCount = 1
                },
                new CraftRecipe
                {
                    Id = "recipe:red_stained_glass",
                    DisplayName = "Red Glass",
                    StationType = BlockType.StationBench,
                    Inputs = new[]
                    {
                        new CraftInput { ExactBlock = BlockType.Glass, Count = 1 },
                        new CraftInput { ExactBlock = BlockType.Flower, Count = 1 }
                    },
                    Output = BlockType.RedStainedGlass,
                    OutputCount = 1
                },
                new CraftRecipe
                {
                    Id = "recipe:blue_stained_glass",
                    DisplayName = "Blue Glass",
                    StationType = BlockType.StationBench,
                    Inputs = new[]
                    {
                        new CraftInput { ExactBlock = BlockType.Glass, Count = 1 },
                        new CraftInput { ExactBlock = BlockType.Glowshroom, Count = 1 }
                    },
                    Output = BlockType.BlueStainedGlass,
                    OutputCount = 1
                },
                new CraftRecipe
                {
                    Id = "recipe:rope",
                    DisplayName = "Rope",
                    StationType = BlockType.StationBench,
                    Inputs = new[]
                    {
                        new CraftInput { ExactBlock = BlockType.Bamboo, Count = 1 }
                    },
                    Output = BlockType.Rope,
                    OutputCount = 3
                },
                ToolRecipe("recipe:copper_pickaxe", "Copper Pickaxe", BlockType.StationForge,
                    CopperPickPattern(), ItemId.CopperPickaxe, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:copper_axe", "Copper Axe", BlockType.StationForge,
                    CopperAxePattern(), ItemId.CopperAxe, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:copper_shovel", "Copper Shovel", BlockType.StationForge,
                    CopperShovelPattern(), ItemId.CopperShovel, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:copper_sword", "Copper Sword", BlockType.StationForge,
                    CopperSwordPattern(), ItemId.CopperSword, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:silver_pickaxe", "Silver Pickaxe", BlockType.StationForge,
                    SilverPickPattern(), ItemId.SilverPickaxe, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:silver_axe", "Silver Axe", BlockType.StationForge,
                    SilverAxePattern(), ItemId.SilverAxe, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:silver_shovel", "Silver Shovel", BlockType.StationForge,
                    SilverShovelPattern(), ItemId.SilverShovel, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:silver_sword", "Silver Sword", BlockType.StationForge,
                    SilverSwordPattern(), ItemId.SilverSword, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:diamond_pickaxe", "Diamond Pickaxe", BlockType.StationForge,
                    DiamondPickPattern(), ItemId.DiamondPickaxe, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:diamond_axe", "Diamond Axe", BlockType.StationForge,
                    DiamondAxePattern(), ItemId.DiamondAxe, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:diamond_shovel", "Diamond Shovel", BlockType.StationForge,
                    DiamondShovelPattern(), ItemId.DiamondShovel, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:diamond_sword", "Diamond Sword", BlockType.StationForge,
                    DiamondSwordPattern(), ItemId.DiamondSword, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:emerald_pickaxe", "Emerald Pickaxe", BlockType.StationForge,
                    EmeraldPickPattern(), ItemId.EmeraldPickaxe, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:emerald_axe", "Emerald Axe", BlockType.StationForge,
                    EmeraldAxePattern(), ItemId.EmeraldAxe, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:emerald_shovel", "Emerald Shovel", BlockType.StationForge,
                    EmeraldShovelPattern(), ItemId.EmeraldShovel, requiresUnlock: true, requiresHeat: true),
                ToolRecipe("recipe:emerald_sword", "Emerald Sword", BlockType.StationForge,
                    EmeraldSwordPattern(), ItemId.EmeraldSword, requiresUnlock: true, requiresHeat: true)
            };
        }

        private static CraftRecipe ToolRecipe(
            string id,
            string displayName,
            BlockType station,
            string[] shapedPattern,
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
                ShapedPattern = shapedPattern,
                GridSize = CraftGridSize.ThreeByThree,
                OutputKind = ItemKind.Tool,
                OutputItem = outputItem,
                OutputCount = 1,
                RequiresUnlock = requiresUnlock,
                RequiresHeat = requiresHeat,
                RequiredTimePhase = requiredTimePhase
            };
        }

        private static string[] WoodPickPattern() => new[] { "PPP", " T ", " T " };
        private static string[] WoodAxePattern() => new[] { "PP", "PT", " T" };
        private static string[] WoodShovelPattern() => new[] { " P", " T", " T" };
        private static string[] WoodSwordPattern() => new[] { " P", " P", " T" };
        private static string[] StonePickPattern() => new[] { "SSS", " T ", " T " };
        private static string[] StoneAxePattern() => new[] { "SS", "ST", " T" };
        private static string[] StoneShovelPattern() => new[] { " S", " T", " T" };
        private static string[] StoneSwordPattern() => new[] { " S", " S", " T" };
        private static string[] IronPickPattern() => new[] { "III", " T ", " T " };
        private static string[] IronAxePattern() => new[] { "II", "IT", " T" };
        private static string[] IronShovelPattern() => new[] { " I", " T", " T" };
        private static string[] IronSwordPattern() => new[] { " I", " I", " T" };
        private static string[] GoldPickPattern() => new[] { "GGG", " T ", " T " };
        private static string[] GoldAxePattern() => new[] { "GG", "GT", " T" };
        private static string[] GoldShovelPattern() => new[] { " G", " T", " T" };
        private static string[] GoldSwordPattern() => new[] { " G", " G", " T" };
        private static string[] CopperPickPattern() => new[] { "UUU", " T ", " T " };
        private static string[] CopperAxePattern() => new[] { "UU", "UT", " T" };
        private static string[] CopperShovelPattern() => new[] { " U", " T", " T" };
        private static string[] CopperSwordPattern() => new[] { " U", " U", " T" };
        private static string[] SilverPickPattern() => new[] { "VVV", " T ", " T " };
        private static string[] SilverAxePattern() => new[] { "VV", "VT", " T" };
        private static string[] SilverShovelPattern() => new[] { " V", " T", " T" };
        private static string[] SilverSwordPattern() => new[] { " V", " V", " T" };
        private static string[] DiamondPickPattern() => new[] { "HHH", " T ", " T " };
        private static string[] DiamondAxePattern() => new[] { "HH", "HT", " T" };
        private static string[] DiamondShovelPattern() => new[] { " H", " T", " T" };
        private static string[] DiamondSwordPattern() => new[] { " H", " H", " T" };
        private static string[] EmeraldPickPattern() => new[] { "EEE", " T ", " T " };
        private static string[] EmeraldAxePattern() => new[] { "EE", "ET", " T" };
        private static string[] EmeraldShovelPattern() => new[] { " E", " T", " T" };
        private static string[] EmeraldSwordPattern() => new[] { " E", " E", " T" };

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

        private static CraftRecipe FoodRecipe(
            string id,
            string displayName,
            BlockType station,
            ItemId inputFood,
            ItemId outputFood,
            bool requiresUnlock = false,
            bool requiresHeat = false)
        {
            return new CraftRecipe
            {
                Id = id,
                DisplayName = displayName,
                StationType = station,
                InputFood = inputFood,
                InputFoodCount = 1,
                OutputKind = ItemKind.Food,
                OutputItem = outputFood,
                OutputCount = 1,
                RequiresUnlock = requiresUnlock,
                RequiresHeat = requiresHeat
            };
        }
    }
}

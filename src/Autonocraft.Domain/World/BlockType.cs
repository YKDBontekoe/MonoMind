namespace Autonocraft.Domain.World
{
    public enum BlockType : byte
    {
        Air = 0,
        Grass = 1,
        OakLog = 2,
        Stone = 3,
        Dirt = 4,
        OakLeaves = 5,
        BirchLog = 6,
        BirchLeaves = 7,
        PineLog = 8,
        PineLeaves = 9,
        Water = 10,
        Sand = 11,
        Snow = 12,
        Gravel = 13,
        CoalOre = 14,
        IronOre = 15,
        GoldOre = 16,
        TallGrass = 17,
        Flower = 18,
        Cactus = 19,
        StationBench = 20,
        StationForge = 21,
        StationCrucible = 22,
        OakPlank = 23,
        Glass = 24,
        Clay = 25,
        IronBlock = 26,
        Sandstone = 27,
        GoldBlock = 28,
        WillowLog = 29,
        WillowLeaves = 30,
        PalmLog = 31,
        PalmLeaves = 32,
        BirchPlank = 33,
        PinePlank = 34,
        Cobblestone = 35,
        Brick = 36,
        MossStone = 37,
        Mud = 38,
        Reed = 39,
        Sunflower = 40,
        HayBale = 41,
        Ice = 42,
        WheatSprout = 43,
        Wheat = 44,
        CarrotSprout = 45,
        Carrot = 46,
        Fern = 47,
        MushroomRed = 48,
        MushroomBrown = 49,
        DeadBush = 50,
        LilyPad = 51,
        Vine = 52,
        BerryBush = 53,
        Seagrass = 54,

        // Custom blocks
        Marble = 55,
        Basalt = 56,
        Slate = 57,
        Obsidian = 58,
        Amethyst = 59,
        MagmaBlock = 60,
        CherryLog = 61,
        CherryLeaves = 62,
        CherryPlank = 63,
        MahoganyLog = 64,
        MahoganyLeaves = 65,
        MahoganyPlank = 66,
        Glowshroom = 67,
        DiamondOre = 68,
        DiamondBlock = 69,
        CopperOre = 70,
        CopperBlock = 71,
        RubyOre = 72,
        RubyBlock = 73,
        MarbleBrick = 74,
        BasaltBrick = 75,
        SlateBrick = 76,
        RedStainedGlass = 77,
        BlueStainedGlass = 78,
        StationSmoker = 79,
        StationStonecutter = 80,
        Limestone = 81,
        Granite = 82,
        QuartzOre = 83,
        QuartzBlock = 84,
        EmeraldOre = 85,
        EmeraldBlock = 86,
        SilverOre = 87,
        SilverBlock = 88,
        Lavender = 89,
        Bamboo = 90,
        Lantern = 91,
        MapleLog = 92,
        MapleLeaves = 93,
        MaplePlank = 94,
        PolishedMarble = 95,
        PolishedGranite = 96,
        Lava = 97,
        Quicksand = 98,
        Rope = 99,
        Kelp = 100,
        GrassSlab = 101,
        DirtSlab = 102,
        StoneSlab = 103,
        SandSlab = 104,
        SnowSlab = 105,
        SnowSide = 106,
        Shrub = 107,
        Heather = 108,
        Moss = 109,
        Juniper = 110,
        Poppy = 111,
        Daisy = 112,
        BlueFlax = 113,
        Tulip = 114,
        WildRose = 115,
        MossCarpet = 116,
        Lichen = 117,
        RedSand = 118,
        Dripstone = 119,
        Chest = 120,
        OakSapling = 121,
        BirchSapling = 122,
        PineSapling = 123,
        WillowSapling = 124,
        PalmSapling = 125,
        CherrySapling = 126,
        MahoganySapling = 127,
        MapleSapling = 128,

        // Snow layers (1/10 block increments). SnowSlab (105) == 5/10, Snow (12) == 10/10.
        SnowLayer1 = 129,
        SnowLayer2 = 130,
        SnowLayer3 = 131,
        SnowLayer4 = 132,
        SnowLayer6 = 133,
        SnowLayer7 = 134,
        SnowLayer8 = 135,
        SnowLayer9 = 136
    }

    public static class BlockTypeExtensions
    {
        public static bool IsTransparent(this BlockType type)
        {
            return type.IsSlab()
                || type.IsSnowLayer()
                || type == BlockType.SnowSide
                || type == BlockType.Air
                || type.IsFluid()
                || type == BlockType.Quicksand
                || type.IsLeaf()
                || type.IsFloraModel()
                || type.IsGlass()
                || type == BlockType.Bamboo
                || type == BlockType.Lantern;
        }

        public static bool IsPassable(this BlockType type)
        {
            return type.IsTransparent()
                && !type.IsSlab()
                && !type.IsSnowLayer()
                && !type.IsGlass()
                && type is not BlockType.Cactus
                && type is not BlockType.Bamboo
                && type is not BlockType.Lantern;
        }

        public static bool IsFluid(this BlockType type)
        {
            return type == BlockType.Water || type == BlockType.Lava;
        }

        public static bool IsWater(this BlockType type)
        {
            return type == BlockType.Water;
        }

        public static bool IsLava(this BlockType type)
        {
            return type == BlockType.Lava;
        }

        public static bool IsClimbable(this BlockType type)
        {
            return type == BlockType.Vine || type == BlockType.Rope;
        }

        public static bool IsGlass(this BlockType type)
        {
            return type is BlockType.Glass or BlockType.RedStainedGlass or BlockType.BlueStainedGlass;
        }

        public static bool IsLeaf(this BlockType type)
        {
            return type is BlockType.OakLeaves or BlockType.BirchLeaves or BlockType.PineLeaves
                or BlockType.WillowLeaves or BlockType.PalmLeaves or BlockType.CherryLeaves
                or BlockType.MahoganyLeaves or BlockType.MapleLeaves;
        }

        public static bool IsLog(this BlockType type)
        {
            return type is BlockType.OakLog or BlockType.BirchLog or BlockType.PineLog
                or BlockType.WillowLog or BlockType.PalmLog or BlockType.CherryLog
                or BlockType.MahoganyLog or BlockType.MapleLog;
        }

        public static bool IsPlank(this BlockType type)
        {
            return type is BlockType.OakPlank or BlockType.BirchPlank or BlockType.PinePlank
                or BlockType.CherryPlank or BlockType.MahoganyPlank or BlockType.MaplePlank;
        }

        public static bool IsWoodMaterial(this BlockType type)
        {
            return type.IsLog() || type.IsPlank() || type == BlockType.Bamboo;
        }

        public static bool IsSapling(this BlockType type)
        {
            return type is BlockType.OakSapling or BlockType.BirchSapling or BlockType.PineSapling
                or BlockType.WillowSapling or BlockType.PalmSapling or BlockType.CherrySapling
                or BlockType.MahoganySapling or BlockType.MapleSapling;
        }

        public static bool IsCrop(this BlockType type)
        {
            return type is BlockType.WheatSprout or BlockType.Wheat or BlockType.CarrotSprout or BlockType.Carrot;
        }

        public static bool IsDecorativeFlora(this BlockType type)
        {
            return type is BlockType.TallGrass or BlockType.Sunflower or BlockType.Flower or BlockType.Reed
                or BlockType.Cactus or BlockType.Fern or BlockType.MushroomRed or BlockType.MushroomBrown
                or BlockType.DeadBush or BlockType.LilyPad or BlockType.Vine or BlockType.BerryBush
                or BlockType.Seagrass or BlockType.Glowshroom or BlockType.Lavender or BlockType.Rope
                or BlockType.Kelp or BlockType.Shrub or BlockType.Heather or BlockType.Juniper
                or BlockType.Poppy or BlockType.Daisy or BlockType.BlueFlax or BlockType.Tulip
                or BlockType.WildRose or BlockType.MossCarpet or BlockType.Lichen;
        }

        public static bool IsFloraModel(this BlockType type)
        {
            return type.IsDecorativeFlora() || type.IsCrop() || type.IsSapling();
        }

        public static bool IsAlphaCutout(this BlockType type)
        {
            return type.IsLeaf();
        }

        public static bool IsCollidable(this BlockType type)
        {
            return type != BlockType.Air && !type.IsPassable();
        }

        public static bool IsSolidForSpawn(this BlockType type)
        {
            return type != BlockType.Air && type != BlockType.Water && type != BlockType.Lava
                && (type.IsSlab() || !type.IsTransparent());
        }

        public static bool IsStation(this BlockType type)
        {
            return type is BlockType.StationBench or BlockType.StationForge or BlockType.StationCrucible
                or BlockType.StationSmoker or BlockType.StationStonecutter;
        }

        public static bool IsChest(this BlockType type) => type == BlockType.Chest;

        public static float GetBreakTime(this BlockType type)
        {
            return type switch
            {
                BlockType.Air => 0f,
                BlockType.TallGrass => 0.1f,
                BlockType.Flower => 0.1f,
                BlockType.Fern or BlockType.MushroomRed or BlockType.MushroomBrown
                    or BlockType.DeadBush or BlockType.LilyPad or BlockType.Vine
                    or BlockType.BerryBush or BlockType.Seagrass or BlockType.Glowshroom
                    or BlockType.Lavender or BlockType.Rope or BlockType.Kelp
                    or BlockType.Shrub or BlockType.Heather or BlockType.Moss or BlockType.Juniper
                    or BlockType.Poppy or BlockType.Daisy or BlockType.BlueFlax or BlockType.Tulip
                    or BlockType.WildRose or BlockType.MossCarpet or BlockType.Lichen
                    or BlockType.OakSapling or BlockType.BirchSapling or BlockType.PineSapling
                    or BlockType.WillowSapling or BlockType.PalmSapling or BlockType.CherrySapling
                    or BlockType.MahoganySapling or BlockType.MapleSapling => 0.1f,
                BlockType.WheatSprout or BlockType.CarrotSprout => 0.1f,
                BlockType.Wheat or BlockType.Carrot => 0.15f,
                BlockType.OakLeaves => 0.15f,
                BlockType.BirchLeaves => 0.15f,
                BlockType.PineLeaves => 0.15f,
                BlockType.WillowLeaves => 0.15f,
                BlockType.PalmLeaves => 0.15f,
                BlockType.CherryLeaves => 0.15f,
                BlockType.MahoganyLeaves => 0.15f,
                BlockType.MapleLeaves => 0.15f,
                BlockType.Reed => 0.1f,
                BlockType.Sunflower => 0.1f,
                BlockType.Dirt => 0.4f,
                BlockType.Sand => 0.35f,
                BlockType.RedSand => 0.35f,
                BlockType.Quicksand => 0.6f,
                BlockType.Snow => 0.35f,
                BlockType.SnowLayer1 or BlockType.SnowLayer2 or BlockType.SnowLayer3 or BlockType.SnowLayer4
                    or BlockType.SnowLayer6 or BlockType.SnowLayer7 or BlockType.SnowLayer8 or BlockType.SnowLayer9 => 0.1f,
                BlockType.Grass => 0.5f,
                BlockType.OakLog => 0.7f,
                BlockType.BirchLog => 0.7f,
                BlockType.PineLog => 0.7f,
                BlockType.WillowLog => 0.7f,
                BlockType.PalmLog => 0.7f,
                BlockType.CherryLog => 0.7f,
                BlockType.MahoganyLog => 0.7f,
                BlockType.MapleLog => 0.7f,
                BlockType.Bamboo => 0.4f,
                BlockType.OakPlank => 0.5f,
                BlockType.BirchPlank => 0.5f,
                BlockType.PinePlank => 0.5f,
                BlockType.CherryPlank => 0.5f,
                BlockType.MahoganyPlank => 0.5f,
                BlockType.MaplePlank => 0.5f,
                BlockType.HayBale => 0.45f,
                BlockType.Mud => 0.45f,
                BlockType.Ice => 0.4f,
                BlockType.Cobblestone => 1.0f,
                BlockType.Brick => 0.95f,
                BlockType.MossStone => 1.1f,
                BlockType.MarbleBrick => 1.0f,
                BlockType.BasaltBrick => 1.0f,
                BlockType.SlateBrick => 1.0f,
                BlockType.Gravel => 0.6f,
                BlockType.Clay => 0.55f,
                BlockType.Sandstone => 0.9f,
                BlockType.Stone => 1.2f,
                BlockType.Dripstone => 1.1f,
                BlockType.Marble => 1.2f,
                BlockType.Basalt => 1.2f,
                BlockType.Slate => 1.2f,
                BlockType.Limestone => 1.2f,
                BlockType.Granite => 1.2f,
                BlockType.PolishedMarble => 1.0f,
                BlockType.PolishedGranite => 1.0f,
                BlockType.Obsidian => 2.5f,
                BlockType.Amethyst => 1.1f,
                BlockType.MagmaBlock => 1.0f,
                BlockType.CoalOre => 1.3f,
                BlockType.IronOre => 1.4f,
                BlockType.GoldOre => 1.5f,
                BlockType.CopperOre => 1.4f,
                BlockType.SilverOre => 1.5f,
                BlockType.RubyOre => 1.6f,
                BlockType.EmeraldOre => 1.7f,
                BlockType.DiamondOre => 1.8f,
                BlockType.QuartzOre => 1.5f,
                BlockType.Cactus => 0.8f,
                BlockType.Glass => 0.3f,
                BlockType.RedStainedGlass => 0.3f,
                BlockType.BlueStainedGlass => 0.3f,
                BlockType.IronBlock => 1.6f,
                BlockType.GoldBlock => 1.7f,
                BlockType.CopperBlock => 1.6f,
                BlockType.SilverBlock => 1.7f,
                BlockType.QuartzBlock => 1.7f,
                BlockType.RubyBlock => 1.8f,
                BlockType.EmeraldBlock => 1.9f,
                BlockType.DiamondBlock => 2.0f,
                BlockType.Lantern => 0.4f,
                BlockType.StationBench => 2.0f,
                BlockType.StationForge => 2.0f,
                BlockType.StationCrucible => 2.0f,
                BlockType.StationSmoker => 2.0f,
                BlockType.StationStonecutter => 2.0f,
                BlockType.Chest => 1.2f,
                BlockType.Water => 0f,
                BlockType.Lava => 0f,
                _ => type.IsSlab() ? 0.5f * type.GetBaseBlockType().GetBreakTime() : 0.6f
            };
        }

        public static bool IsSlab(this BlockType type)
        {
            return type is BlockType.GrassSlab or BlockType.DirtSlab or BlockType.StoneSlab or BlockType.SandSlab or BlockType.SnowSlab;
        }

        /// <summary>Returns true for SnowLayer1-4 and SnowLayer6-9 (fractional snow accumulation).</summary>
        public static bool IsSnowLayer(this BlockType type)
        {
            return type is BlockType.SnowLayer1 or BlockType.SnowLayer2 or BlockType.SnowLayer3 or BlockType.SnowLayer4
                or BlockType.SnowLayer6 or BlockType.SnowLayer7 or BlockType.SnowLayer8 or BlockType.SnowLayer9;
        }

        /// <summary>
        /// Returns 1-10 for snow layers (1 = thinnest, 10 = full Snow block).
        /// SnowLayer1-4 = 1-4, SnowSlab = 5, SnowLayer6-9 = 6-9, Snow = 10.
        /// Returns 0 for non-snow blocks.
        /// </summary>
        public static int GetSnowLevel(this BlockType type)
        {
            return type switch
            {
                BlockType.SnowLayer1 => 1,
                BlockType.SnowLayer2 => 2,
                BlockType.SnowLayer3 => 3,
                BlockType.SnowLayer4 => 4,
                BlockType.SnowSlab => 5,
                BlockType.SnowLayer6 => 6,
                BlockType.SnowLayer7 => 7,
                BlockType.SnowLayer8 => 8,
                BlockType.SnowLayer9 => 9,
                BlockType.Snow => 10,
                _ => 0
            };
        }

        /// <summary>Returns the BlockType for a given snow level (1-10).</summary>
        public static BlockType GetSnowBlockTypeForLevel(int level)
        {
            return level switch
            {
                1 => BlockType.SnowLayer1,
                2 => BlockType.SnowLayer2,
                3 => BlockType.SnowLayer3,
                4 => BlockType.SnowLayer4,
                5 => BlockType.SnowSlab,
                6 => BlockType.SnowLayer6,
                7 => BlockType.SnowLayer7,
                8 => BlockType.SnowLayer8,
                9 => BlockType.SnowLayer9,
                _ => BlockType.Snow
            };
        }

        /// <summary>
        /// Returns the rendered/collision height of a block in the range [0, 1].
        /// Full blocks = 1.0, slabs = 0.5, snow layers = 0.1 * level.
        /// </summary>
        public static float GetBlockHeight(this BlockType type)
        {
            if (type.IsSnowLayer())
                return type.GetSnowLevel() * 0.1f;
            if (type == BlockType.SnowSlab)
                return 0.5f;
            if (type.IsSlab())
                return 0.5f;
            return 1.0f;
        }

        public static BlockType GetBaseBlockType(this BlockType type)
        {
            return type switch
            {
                BlockType.GrassSlab => BlockType.Grass,
                BlockType.DirtSlab => BlockType.Dirt,
                BlockType.StoneSlab => BlockType.Stone,
                BlockType.SandSlab => BlockType.Sand,
                BlockType.SnowSlab => BlockType.Snow,
                BlockType.SnowLayer1 or BlockType.SnowLayer2 or BlockType.SnowLayer3 or BlockType.SnowLayer4
                    or BlockType.SnowLayer6 or BlockType.SnowLayer7 or BlockType.SnowLayer8 or BlockType.SnowLayer9 => BlockType.Snow,
                _ => type
            };
        }

        public static bool CanSupportBleeding(this BlockType type)
        {
            return type is BlockType.Dirt or BlockType.Stone or BlockType.Gravel or BlockType.Clay
                or BlockType.Mud or BlockType.Limestone or BlockType.Granite or BlockType.Basalt
                or BlockType.Marble or BlockType.Slate or BlockType.Cobblestone or BlockType.MossStone;
        }
    }
}

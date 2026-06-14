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
        Ice = 42
    }

    public static class BlockTypeExtensions
    {
        public static bool IsTransparent(this BlockType type)
        {
            return type == BlockType.Air
                || type == BlockType.Water
                || type == BlockType.OakLeaves
                || type == BlockType.BirchLeaves
                || type == BlockType.PineLeaves
                || type == BlockType.WillowLeaves
                || type == BlockType.PalmLeaves
                || type == BlockType.TallGrass
                || type == BlockType.Flower
                || type == BlockType.Reed
                || type == BlockType.Sunflower
                || type == BlockType.Cactus
                || type == BlockType.Glass;
        }

        public static bool IsPassable(this BlockType type)
        {
            return type.IsTransparent()
                && type is not BlockType.Glass
                && type is not BlockType.Cactus;
        }

        public static bool IsFluid(this BlockType type)
        {
            return type == BlockType.Water;
        }

        public static bool IsWater(this BlockType type)
        {
            return type == BlockType.Water;
        }

        public static bool IsFloraModel(this BlockType type)
        {
            return type is BlockType.TallGrass or BlockType.Sunflower or BlockType.Flower or BlockType.Reed
                or BlockType.Cactus;
        }

        public static bool IsAlphaCutout(this BlockType type)
        {
            return type is BlockType.OakLeaves or BlockType.BirchLeaves or BlockType.PineLeaves
                or BlockType.WillowLeaves or BlockType.PalmLeaves;
        }

        public static bool IsCollidable(this BlockType type)
        {
            return type != BlockType.Air && !type.IsPassable();
        }

        public static bool IsSolidForSpawn(this BlockType type)
        {
            return type != BlockType.Air && type != BlockType.Water && !type.IsTransparent();
        }

        public static bool IsStation(this BlockType type)
        {
            return type is BlockType.StationBench or BlockType.StationForge or BlockType.StationCrucible;
        }

        public static float GetBreakTime(this BlockType type)
        {
            return type switch
            {
                BlockType.Air => 0f,
                BlockType.TallGrass => 0.1f,
                BlockType.Flower => 0.1f,
                BlockType.OakLeaves => 0.15f,
                BlockType.BirchLeaves => 0.15f,
                BlockType.PineLeaves => 0.15f,
                BlockType.WillowLeaves => 0.15f,
                BlockType.PalmLeaves => 0.15f,
                BlockType.Reed => 0.1f,
                BlockType.Sunflower => 0.1f,
                BlockType.Dirt => 0.4f,
                BlockType.Sand => 0.35f,
                BlockType.Snow => 0.35f,
                BlockType.Grass => 0.5f,
                BlockType.OakLog => 0.7f,
                BlockType.BirchLog => 0.7f,
                BlockType.PineLog => 0.7f,
                BlockType.WillowLog => 0.7f,
                BlockType.PalmLog => 0.7f,
                BlockType.OakPlank => 0.5f,
                BlockType.BirchPlank => 0.5f,
                BlockType.PinePlank => 0.5f,
                BlockType.HayBale => 0.45f,
                BlockType.Mud => 0.45f,
                BlockType.Ice => 0.4f,
                BlockType.Cobblestone => 1.0f,
                BlockType.Brick => 0.95f,
                BlockType.MossStone => 1.1f,
                BlockType.Gravel => 0.6f,
                BlockType.Clay => 0.55f,
                BlockType.Sandstone => 0.9f,
                BlockType.Stone => 1.2f,
                BlockType.CoalOre => 1.3f,
                BlockType.IronOre => 1.4f,
                BlockType.GoldOre => 1.5f,
                BlockType.Cactus => 0.8f,
                BlockType.Glass => 0.3f,
                BlockType.IronBlock => 1.6f,
                BlockType.GoldBlock => 1.7f,
                BlockType.StationBench => 2.0f,
                BlockType.StationForge => 2.0f,
                BlockType.StationCrucible => 2.0f,
                BlockType.Water => 0f,
                _ => 0.6f
            };
        }
    }
}

namespace Autonocraft.World
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
        Cactus = 19
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
                || type == BlockType.TallGrass
                || type == BlockType.Flower;
        }

        public static bool IsSolidForSpawn(this BlockType type)
        {
            return type != BlockType.Air && type != BlockType.Water && !type.IsTransparent();
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
                BlockType.Dirt => 0.4f,
                BlockType.Sand => 0.35f,
                BlockType.Snow => 0.35f,
                BlockType.Grass => 0.5f,
                BlockType.OakLog => 0.7f,
                BlockType.BirchLog => 0.7f,
                BlockType.PineLog => 0.7f,
                BlockType.Gravel => 0.6f,
                BlockType.Stone => 1.2f,
                BlockType.CoalOre => 1.3f,
                BlockType.IronOre => 1.4f,
                BlockType.GoldOre => 1.5f,
                BlockType.Cactus => 0.8f,
                BlockType.Water => 0f,
                _ => 0.6f
            };
        }
    }
}

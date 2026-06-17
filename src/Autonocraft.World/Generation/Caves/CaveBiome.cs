namespace Autonocraft.World.Generation.Caves
{
    public enum CaveBiomeType
    {
        Stone,
        Lush,
        Dripstone,
        Crystal,
        Mushroom,
        DeepDark
    }

    public readonly struct CaveBiomeProfile
    {
        public CaveBiomeType Type { get; init; }
        public BlockType WallBlock { get; init; }
        public BlockType FloorBlock { get; init; }
        public float DecorationDensity { get; init; }

        public static CaveBiomeProfile For(CaveBiomeType type) => type switch
        {
            CaveBiomeType.Lush => new CaveBiomeProfile
            {
                Type = type,
                WallBlock = BlockType.MossStone,
                FloorBlock = BlockType.MossStone,
                DecorationDensity = 0.55f
            },
            CaveBiomeType.Dripstone => new CaveBiomeProfile
            {
                Type = type,
                WallBlock = BlockType.Limestone,
                FloorBlock = BlockType.Stone,
                DecorationDensity = 0.42f
            },
            CaveBiomeType.Crystal => new CaveBiomeProfile
            {
                Type = type,
                WallBlock = BlockType.Stone,
                FloorBlock = BlockType.Stone,
                DecorationDensity = 0.38f
            },
            CaveBiomeType.Mushroom => new CaveBiomeProfile
            {
                Type = type,
                WallBlock = BlockType.MossStone,
                FloorBlock = BlockType.Dirt,
                DecorationDensity = 0.48f
            },
            CaveBiomeType.DeepDark => new CaveBiomeProfile
            {
                Type = type,
                WallBlock = BlockType.Stone,
                FloorBlock = BlockType.Stone,
                DecorationDensity = 0.08f
            },
            _ => new CaveBiomeProfile
            {
                Type = CaveBiomeType.Stone,
                WallBlock = BlockType.Stone,
                FloorBlock = BlockType.Stone,
                DecorationDensity = 0.12f
            }
        };
    }
}

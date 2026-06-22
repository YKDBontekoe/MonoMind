namespace Autonocraft.World
{
    public enum BiomeType
    {
        Ocean,
        Beach,
        Plains,
        Forest,
        Jungle,
        Desert,
        Mountains,
        SnowyPeaks,
        Swamp,
        Badlands,
        Mangrove,
        MushroomForest,
        Volcanic,
        BorealTaiga
    }

    public readonly struct BiomeProfile
    {
        private static readonly BiomeProfile Default = Create(
            BiomeType.Plains,
            WorldConstants.SeaLevel + 6,
            7f,
            0f,
            BlockType.Grass,
            BlockType.Dirt,
            BlockType.Stone,
            0.16f,
            0.92f,
            allowTallGrass: true,
            allowFlowers: true);

        private static readonly BiomeProfile[] Catalog =
        {
            Create(BiomeType.Ocean, WorldConstants.SeaLevel - 18, 6f, 0f, BlockType.Sand, BlockType.Sand, BlockType.Stone),
            Create(BiomeType.Beach, WorldConstants.SeaLevel + 1, 2f, 0f, BlockType.Sand, BlockType.Sand, BlockType.Stone, 0.05f, 0.35f),
            Default,
            Create(BiomeType.Forest, WorldConstants.SeaLevel + 8, 10f, 0f, BlockType.Grass, BlockType.Dirt, BlockType.Stone, 0.58f, 0.95f, allowTallGrass: true, allowFlowers: true, allowUnderstory: true),
            Create(BiomeType.Jungle, WorldConstants.SeaLevel + 7, 12f, 0f, BlockType.Grass, BlockType.Dirt, BlockType.Stone, 0.82f, 1.0f, allowTallGrass: true, allowUnderstory: true),
            Create(BiomeType.Desert, WorldConstants.SeaLevel + 5, 6f, 0f, BlockType.Sand, BlockType.Sand, BlockType.Stone, 0.03f, 0.45f, allowCactus: true),
            Create(BiomeType.Mountains, WorldConstants.SeaLevel + 24, 78f, 0.56f, BlockType.Stone, BlockType.Stone, BlockType.Stone, 0.12f, 0.30f),
            Create(BiomeType.SnowyPeaks, WorldConstants.SeaLevel + 40, 96f, 0.62f, BlockType.Snow, BlockType.Stone, BlockType.Stone, 0.07f, 0.30f),
            Create(BiomeType.Swamp, WorldConstants.SeaLevel + 2, 3f, 0f, BlockType.Mud, BlockType.Dirt, BlockType.Stone, 0.45f, 0.82f, allowTallGrass: true, allowUnderstory: true),
            Create(BiomeType.Badlands, WorldConstants.SeaLevel + 9, 11f, 0.26f, BlockType.RedSand, BlockType.Sandstone, BlockType.Stone, 0.02f, 0.38f, allowCactus: true),
            Create(BiomeType.Mangrove, WorldConstants.SeaLevel + 1, 3f, 0f, BlockType.Mud, BlockType.Clay, BlockType.Stone, 0.38f, 0.82f, allowUnderstory: true),
            Create(BiomeType.MushroomForest, WorldConstants.SeaLevel + 10, 8f, 0f, BlockType.MossStone, BlockType.Dirt, BlockType.Stone, 0.22f, 1.0f, allowUnderstory: true),
            Create(BiomeType.Volcanic, WorldConstants.SeaLevel + 24, 42f, 0.48f, BlockType.Basalt, BlockType.Basalt, BlockType.Obsidian, 0f, 0.12f),
            Create(BiomeType.BorealTaiga, WorldConstants.SeaLevel + 7, 7f, 0f, BlockType.Grass, BlockType.Dirt, BlockType.Stone, 0.55f, 0.76f, allowTallGrass: true)
        };

        public BiomeType Type { get; init; }
        public float BaseHeight { get; init; }
        public float HeightAmplitude { get; init; }
        public float RidgeWeight { get; init; }
        public BlockType SurfaceBlock { get; init; }
        public BlockType SubsurfaceBlock { get; init; }
        public BlockType FillerBlock { get; init; }
        public float TreeDensity { get; init; }
        public float FloraDensity { get; init; }
        public bool AllowTallGrass { get; init; }
        public bool AllowFlowers { get; init; }
        public bool AllowCactus { get; init; }
        public bool AllowUnderstory { get; init; }

        public static BiomeProfile For(BiomeType type)
        {
            int index = (int)type;
            return index >= 0 && index < Catalog.Length ? Catalog[index] : Default;
        }

        private static BiomeProfile Create(
            BiomeType type,
            float baseHeight,
            float heightAmplitude,
            float ridgeWeight,
            BlockType surfaceBlock,
            BlockType subsurfaceBlock,
            BlockType fillerBlock,
            float treeDensity = 0f,
            float floraDensity = 0f,
            bool allowTallGrass = false,
            bool allowFlowers = false,
            bool allowCactus = false,
            bool allowUnderstory = false)
        {
            return new BiomeProfile
            {
                Type = type,
                BaseHeight = baseHeight,
                HeightAmplitude = heightAmplitude,
                RidgeWeight = ridgeWeight,
                SurfaceBlock = surfaceBlock,
                SubsurfaceBlock = subsurfaceBlock,
                FillerBlock = fillerBlock,
                TreeDensity = treeDensity,
                FloraDensity = floraDensity,
                AllowTallGrass = allowTallGrass,
                AllowFlowers = allowFlowers,
                AllowCactus = allowCactus,
                AllowUnderstory = allowUnderstory
            };
        }
    }

    public readonly struct BiomeSample
    {
        public BiomeType Primary { get; init; }
        public float Temperature { get; init; }
        public float Moisture { get; init; }
        public float Continentalness { get; init; }
        public float Erosion { get; init; }
        public BiomeProfile Profile { get; init; }
    }
}

namespace Autonocraft.World
{
    public enum BiomeType
    {
        Ocean,
        Beach,
        Plains,
        Forest,
        Desert,
        Mountains,
        SnowyPeaks,
        Swamp
    }

    public readonly struct BiomeProfile
    {
        public BiomeType Type { get; init; }
        public float BaseHeight { get; init; }
        public float HeightAmplitude { get; init; }
        public float RidgeWeight { get; init; }
        public BlockType SurfaceBlock { get; init; }
        public BlockType SubsurfaceBlock { get; init; }
        public BlockType FillerBlock { get; init; }
        public float TreeDensity { get; init; }
        public bool AllowTallGrass { get; init; }
        public bool AllowFlowers { get; init; }
        public bool AllowCactus { get; init; }

        public static BiomeProfile For(BiomeType type) => type switch
        {
            BiomeType.Ocean => new BiomeProfile
            {
                Type = type,
                BaseHeight = WorldConstants.SeaLevel - 18,
                HeightAmplitude = 6f,
                SurfaceBlock = BlockType.Sand,
                SubsurfaceBlock = BlockType.Sand,
                FillerBlock = BlockType.Stone
            },
            BiomeType.Beach => new BiomeProfile
            {
                Type = type,
                BaseHeight = WorldConstants.SeaLevel + 1,
                HeightAmplitude = 2f,
                SurfaceBlock = BlockType.Sand,
                SubsurfaceBlock = BlockType.Sand,
                FillerBlock = BlockType.Stone,
                TreeDensity = 0.05f
            },
            BiomeType.Plains => new BiomeProfile
            {
                Type = type,
                BaseHeight = WorldConstants.SeaLevel + 6,
                HeightAmplitude = 8f,
                SurfaceBlock = BlockType.Grass,
                SubsurfaceBlock = BlockType.Dirt,
                FillerBlock = BlockType.Stone,
                TreeDensity = 0.12f,
                AllowTallGrass = true,
                AllowFlowers = true
            },
            BiomeType.Forest => new BiomeProfile
            {
                Type = type,
                BaseHeight = WorldConstants.SeaLevel + 8,
                HeightAmplitude = 12f,
                SurfaceBlock = BlockType.Grass,
                SubsurfaceBlock = BlockType.Dirt,
                FillerBlock = BlockType.Stone,
                TreeDensity = 0.55f,
                AllowTallGrass = true
            },
            BiomeType.Desert => new BiomeProfile
            {
                Type = type,
                BaseHeight = WorldConstants.SeaLevel + 5,
                HeightAmplitude = 6f,
                SurfaceBlock = BlockType.Sand,
                SubsurfaceBlock = BlockType.Sand,
                FillerBlock = BlockType.Stone,
                AllowCactus = true
            },
            BiomeType.Mountains => new BiomeProfile
            {
                Type = type,
                BaseHeight = WorldConstants.SeaLevel + 18,
                HeightAmplitude = 42f,
                RidgeWeight = 0.75f,
                SurfaceBlock = BlockType.Stone,
                SubsurfaceBlock = BlockType.Stone,
                FillerBlock = BlockType.Stone,
                TreeDensity = 0.08f
            },
            BiomeType.SnowyPeaks => new BiomeProfile
            {
                Type = type,
                BaseHeight = WorldConstants.SeaLevel + 28,
                HeightAmplitude = 52f,
                RidgeWeight = 0.9f,
                SurfaceBlock = BlockType.Snow,
                SubsurfaceBlock = BlockType.Stone,
                FillerBlock = BlockType.Stone,
                TreeDensity = 0.04f
            },
            BiomeType.Swamp => new BiomeProfile
            {
                Type = type,
                BaseHeight = WorldConstants.SeaLevel + 2,
                HeightAmplitude = 4f,
                SurfaceBlock = BlockType.Mud,
                SubsurfaceBlock = BlockType.Dirt,
                FillerBlock = BlockType.Stone,
                TreeDensity = 0.2f,
                AllowTallGrass = true
            },
            _ => BiomeProfile.For(BiomeType.Plains)
        };
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

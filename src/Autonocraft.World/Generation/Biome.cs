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
                TreeDensity = 0.05f,
                FloraDensity = 0.35f
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
                FloraDensity = 0.92f,
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
                FloraDensity = 0.95f,
                AllowTallGrass = true,
                AllowFlowers = true,
                AllowUnderstory = true
            },
            BiomeType.Jungle => new BiomeProfile
            {
                Type = type,
                BaseHeight = WorldConstants.SeaLevel + 7,
                HeightAmplitude = 14f,
                SurfaceBlock = BlockType.Grass,
                SubsurfaceBlock = BlockType.Dirt,
                FillerBlock = BlockType.Stone,
                TreeDensity = 0.78f,
                FloraDensity = 1.0f,
                AllowTallGrass = true,
                AllowUnderstory = true
            },
            BiomeType.Desert => new BiomeProfile
            {
                Type = type,
                BaseHeight = WorldConstants.SeaLevel + 5,
                HeightAmplitude = 6f,
                SurfaceBlock = BlockType.Sand,
                SubsurfaceBlock = BlockType.Sand,
                FillerBlock = BlockType.Stone,
                TreeDensity = 0.03f,
                FloraDensity = 0.45f,
                AllowCactus = true
            },
            BiomeType.Mountains => new BiomeProfile
            {
                Type = type,
                BaseHeight = WorldConstants.SeaLevel + 28,
                HeightAmplitude = 95f,
                RidgeWeight = 0.68f,
                SurfaceBlock = BlockType.Stone,
                SubsurfaceBlock = BlockType.Stone,
                FillerBlock = BlockType.Stone,
                TreeDensity = 0.08f,
                FloraDensity = 0.20f
            },
            BiomeType.SnowyPeaks => new BiomeProfile
            {
                Type = type,
                BaseHeight = WorldConstants.SeaLevel + 48,
                HeightAmplitude = 125f,
                RidgeWeight = 0.78f,
                SurfaceBlock = BlockType.Snow,
                SubsurfaceBlock = BlockType.Stone,
                FillerBlock = BlockType.Stone,
                TreeDensity = 0.04f,
                FloraDensity = 0.22f
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
                FloraDensity = 0.75f,
                AllowTallGrass = true,
                AllowUnderstory = true
            },
            BiomeType.Badlands => new BiomeProfile
            {
                Type = type,
                BaseHeight = WorldConstants.SeaLevel + 9,
                HeightAmplitude = 14f,
                RidgeWeight = 0.35f,
                SurfaceBlock = BlockType.RedSand,
                SubsurfaceBlock = BlockType.Sandstone,
                FillerBlock = BlockType.Stone,
                TreeDensity = 0.02f,
                FloraDensity = 0.38f,
                AllowCactus = true
            },
            BiomeType.Mangrove => new BiomeProfile
            {
                Type = type,
                BaseHeight = WorldConstants.SeaLevel + 1,
                HeightAmplitude = 3f,
                SurfaceBlock = BlockType.Mud,
                SubsurfaceBlock = BlockType.Clay,
                FillerBlock = BlockType.Stone,
                TreeDensity = 0.38f,
                FloraDensity = 0.82f,
                AllowUnderstory = true
            },
            BiomeType.MushroomForest => new BiomeProfile
            {
                Type = type,
                BaseHeight = WorldConstants.SeaLevel + 10,
                HeightAmplitude = 9f,
                SurfaceBlock = BlockType.MossStone,
                SubsurfaceBlock = BlockType.Dirt,
                FillerBlock = BlockType.Stone,
                TreeDensity = 0.14f,
                FloraDensity = 0.90f,
                AllowUnderstory = true
            },
            BiomeType.Volcanic => new BiomeProfile
            {
                Type = type,
                BaseHeight = WorldConstants.SeaLevel + 32,
                HeightAmplitude = 58f,
                RidgeWeight = 0.70f,
                SurfaceBlock = BlockType.Basalt,
                SubsurfaceBlock = BlockType.Basalt,
                FillerBlock = BlockType.Obsidian,
                TreeDensity = 0f,
                FloraDensity = 0.12f
            },
            BiomeType.BorealTaiga => new BiomeProfile
            {
                Type = type,
                BaseHeight = WorldConstants.SeaLevel + 7,
                HeightAmplitude = 11f,
                SurfaceBlock = BlockType.Grass,
                SubsurfaceBlock = BlockType.Dirt,
                FillerBlock = BlockType.Stone,
                TreeDensity = 0.48f,
                FloraDensity = 0.68f,
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

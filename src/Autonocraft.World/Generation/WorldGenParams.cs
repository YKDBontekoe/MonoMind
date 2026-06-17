namespace Autonocraft.World
{
    public sealed class WorldGenParams
    {
        public WorldType WorldType { get; init; } = WorldType.Default;
        public float HeightScale { get; init; } = 1.25f;
        public float HeightOffset { get; init; } = 0f;
        public float ContinentalnessBias { get; init; } = 0f;
        public float MountainWeight { get; init; } = 1f;
        public float TreeDensityScale { get; init; } = 1f;
        public float FloraDensityScale { get; init; } = 1f;
        public bool EnableCaves { get; init; } = true;
        public bool EnableOres { get; init; } = true;
        public bool EnableRivers { get; init; } = true;
        public bool EnableStructures { get; init; } = true;
        public float StructureDensityScale { get; init; } = 1f;

        public static WorldGenParams ForType(WorldType type) => type switch
        {
            WorldType.Mountains => new WorldGenParams
            {
                WorldType = type,
                HeightScale = 1.65f,
                HeightOffset = 12f,
                MountainWeight = 2.2f,
                TreeDensityScale = 0.7f
            },
            WorldType.Islands => new WorldGenParams
            {
                WorldType = type,
                ContinentalnessBias = -0.18f,
                HeightScale = 0.85f,
                TreeDensityScale = 1.2f
            },
            WorldType.Flat => new WorldGenParams
            {
                WorldType = type,
                HeightScale = 0.15f,
                HeightOffset = -4f,
                MountainWeight = 0.1f,
                TreeDensityScale = 0.5f,
                EnableCaves = false,
                EnableRivers = false,
                EnableStructures = false
            },
            WorldType.StructureGallery => new WorldGenParams
            {
                WorldType = type,
                HeightScale = 0f,
                HeightOffset = 0f,
                MountainWeight = 0f,
                TreeDensityScale = 0f,
                EnableCaves = false,
                EnableRivers = false,
                EnableStructures = true
            },
            _ => new WorldGenParams
            {
                WorldType = WorldType.Default,
                MountainWeight = 1.10f
            }
        };
    }
}

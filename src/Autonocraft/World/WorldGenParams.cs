namespace Autonocraft.World
{
    public sealed class WorldGenParams
    {
        public WorldType WorldType { get; init; } = WorldType.Default;
        public float HeightScale { get; init; } = 1f;
        public float HeightOffset { get; init; } = 0f;
        public float ContinentalnessBias { get; init; } = 0f;
        public float MountainWeight { get; init; } = 1f;
        public float TreeDensityScale { get; init; } = 1f;
        public bool EnableCaves { get; init; } = true;
        public bool EnableOres { get; init; } = true;
        public bool EnableRivers { get; init; } = true;

        public static WorldGenParams ForType(WorldType type) => type switch
        {
            WorldType.Mountains => new WorldGenParams
            {
                WorldType = type,
                HeightScale = 1.45f,
                HeightOffset = 8f,
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
                EnableRivers = false
            },
            _ => new WorldGenParams { WorldType = WorldType.Default }
        };
    }
}

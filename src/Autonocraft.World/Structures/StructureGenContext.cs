namespace Autonocraft.World.Structures
{
    public readonly struct StructureGenContext
    {
        public int WorldSeed { get; init; }
        public int AnchorX { get; init; }
        public int AnchorZ { get; init; }
        public int VariantSalt { get; init; }
        public BiomeType Biome { get; init; }
        public StructureRng Rng { get; init; }
        public BiomeStructurePalette Palette { get; init; }

        public static StructureGenContext Create(
            int worldSeed,
            int anchorX,
            int anchorZ,
            int variantSalt,
            BiomeType biome)
        {
            int mixed = StructurePlacementKeys.MixSeed(worldSeed, 0, 0, variantSalt);
            return new StructureGenContext
            {
                WorldSeed = worldSeed,
                AnchorX = anchorX,
                AnchorZ = anchorZ,
                VariantSalt = variantSalt,
                Biome = biome,
                Rng = new StructureRng(mixed),
                Palette = BiomeStructurePalette.For(biome)
            };
        }
    }
}

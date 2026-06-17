using System;
using System.Collections.Concurrent;

namespace Autonocraft.World.Structures
{
    public delegate StructureTemplate StructureTemplateGenerator(in StructureGenContext context);

    public static class StructurePlacementKeys
    {
        private static readonly ConcurrentDictionary<(int Seed, int X, int Z, string Id, int Salt), StructureTemplate> Cache = new();
        private static readonly ConcurrentDictionary<(int Seed, int X, int Z, int TierSalt, int Radius, int Delta), bool> FlatnessCache = new();

        public static int MixSeed(int worldSeed, int anchorX, int anchorZ, int variantSalt)
        {
            unchecked
            {
                int h = worldSeed;
                h = h * 31 + anchorX;
                h = h * 31 + anchorZ;
                h = h * 31 + variantSalt;
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                return h;
            }
        }

        public static int VariantSaltForStructure(int worldSeed, int anchorX, int anchorZ, string structureId, int placementHash)
        {
            return MixSeed(worldSeed, anchorX, anchorZ, placementHash ^ structureId.GetHashCode(StringComparison.Ordinal));
        }

        public static StructureTemplate Resolve(
            StructureDefinition definition,
            int worldSeed,
            int anchorX,
            int anchorZ,
            int variantSalt,
            BiomeType biome)
        {
            if (definition.GenerateTemplate == null)
            {
                return definition.Template;
            }

            var key = (worldSeed, anchorX, anchorZ, definition.Id, variantSalt);
            return Cache.GetOrAdd(key, _ =>
            {
                var context = StructureGenContext.Create(worldSeed, anchorX, anchorZ, variantSalt, biome);
                var template = definition.GenerateTemplate(context);
                if (template.ChunkIndex == null && template.Blocks.Length > 0)
                {
                    return new StructureTemplate
                    {
                        FootprintRadius = template.FootprintRadius,
                        Blocks = template.Blocks,
                        Chests = template.Chests,
                        ChunkIndex = StructureChunkIndex.Build(template.Blocks)
                    };
                }

                return template;
            });
        }

        public static bool IsAnchorFlat(
            int worldSeed,
            int anchorX,
            int anchorZ,
            int tierSalt,
            int radius,
            int maxFlatnessDelta,
            Func<bool> compute)
        {
            var key = (worldSeed, anchorX, anchorZ, tierSalt, radius, maxFlatnessDelta);
            return FlatnessCache.GetOrAdd(key, _ => compute());
        }

        public static void ClearCache()
        {
            Cache.Clear();
            FlatnessCache.Clear();
        }
    }
}

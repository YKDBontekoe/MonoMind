using Autonocraft.Domain.World;

namespace Autonocraft.World
{
    /// <summary>
    /// Terrain slab placement for gentle lowland stair-steps. Exposed for integration tests.
    /// </summary>
    internal static class TerrainSlabRules
    {
        internal const float MaxLowlandHeight = WorldConstants.SeaLevel + 9f;
        internal const float MaxLocalSpread = 1.05f;
        internal const float SteepHeightDelta = 1.35f;

        internal static bool TryGetPlacement(
            TerrainColumn draft,
            float height,
            float[,] heights,
            int x,
            int z,
            BlockType surface,
            out int slabHeight,
            out BlockType slabType)
        {
            slabHeight = 0;
            slabType = BlockType.Air;

            if (!IsLowlandSurface(surface))
            {
                return false;
            }

            slabType = ToSlabType(surface);
            if (slabType == BlockType.Air)
            {
                return false;
            }

            if (!IsLowlandBiome(draft, height))
            {
                return false;
            }

            if (draft.IsRiver || draft.IsLake)
            {
                return false;
            }

            if (IsCliffOrSteepTerrain(heights, x, z, height))
            {
                return false;
            }

            if (!IsGentleLowlandStep(heights, x, z, height, out int lowerBlockY))
            {
                return false;
            }

            slabHeight = lowerBlockY;
            return true;
        }

        internal static bool IsLowlandSurface(BlockType surface)
        {
            return surface is BlockType.Grass or BlockType.Sand or BlockType.Dirt;
        }

        internal static bool IsLowlandBiome(TerrainColumn draft, float height)
        {
            if (draft.Biome.Primary is BiomeType.Mountains or BiomeType.SnowyPeaks or BiomeType.Ocean or BiomeType.Swamp)
            {
                return false;
            }

            if (draft.Biome.Primary is not (BiomeType.Plains or BiomeType.Forest or BiomeType.Jungle or BiomeType.Desert))
            {
                return false;
            }

            if (height > MaxLowlandHeight)
            {
                return false;
            }

            if (draft.Profile.RidgeWeight > 0.12f)
            {
                return false;
            }

            return true;
        }

        internal static BlockType ToSlabType(BlockType surface)
        {
            return surface switch
            {
                BlockType.Grass => BlockType.GrassSlab,
                BlockType.Dirt => BlockType.DirtSlab,
                BlockType.Sand => BlockType.SandSlab,
                _ => BlockType.Air
            };
        }

        private static bool IsCliffOrSteepTerrain(float[,] heights, int x, int z, float height)
        {
            int width = heights.GetLength(0);
            int depth = heights.GetLength(1);
            int selfRound = (int)MathF.Round(height);

            ReadOnlySpan<(int dx, int dz)> offsets = stackalloc (int, int)[]
            {
                (-1, 0), (1, 0), (0, -1), (0, 1),
                (-1, -1), (1, -1), (-1, 1), (1, 1)
            };

            foreach (var (dx, dz) in offsets)
            {
                int nx = x + dx;
                int nz = z + dz;
                if (nx < 0 || nz < 0 || nx >= width || nz >= depth)
                {
                    continue;
                }

                int neighborRound = (int)MathF.Round(heights[nx, nz]);
                if (Math.Abs(neighborRound - selfRound) >= 2)
                {
                    return true;
                }

                if (MathF.Abs(heights[nx, nz] - height) >= SteepHeightDelta)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsGentleLowlandStep(float[,] heights, int x, int z, float height, out int slabBlockY)
        {
            slabBlockY = 0;
            int width = heights.GetLength(0);
            int depth = heights.GetLength(1);
            int selfRound = (int)MathF.Round(height);
            int minNeighborRound = selfRound;
            int maxNeighborRound = selfRound;
            float maxNeighbor = height;
            float minNeighbor = height;

            ReadOnlySpan<(int dx, int dz)> offsets = stackalloc (int, int)[]
            {
                (-1, 0), (1, 0), (0, -1), (0, 1)
            };

            foreach (var (dx, dz) in offsets)
            {
                int nx = x + dx;
                int nz = z + dz;
                if (nx < 0 || nz < 0 || nx >= width || nz >= depth)
                {
                    continue;
                }

                float neighbor = heights[nx, nz];
                int neighborRound = (int)MathF.Round(neighbor);
                minNeighborRound = Math.Min(minNeighborRound, neighborRound);
                maxNeighborRound = Math.Max(maxNeighborRound, neighborRound);
                maxNeighbor = MathF.Max(maxNeighbor, neighbor);
                minNeighbor = MathF.Min(minNeighbor, neighbor);
            }

            if (maxNeighbor - minNeighbor > MaxLocalSpread)
            {
                return false;
            }

            // Place the slab on the upper cell of a 1-block rounded step so its top
            // aligns with neighboring full blocks instead of sitting one block too low.
            if (maxNeighborRound - minNeighborRound != 1)
            {
                return false;
            }

            if (selfRound != maxNeighborRound)
            {
                return false;
            }

            slabBlockY = selfRound;
            return true;
        }
    }
}

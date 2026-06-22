using System;

namespace Autonocraft.World
{
    public sealed class CaveCarver
    {
        private const int LavaDepthThreshold = 10;
        private readonly PerlinNoise3D _caveNoise;
        private readonly PerlinNoise3D _wormNoise;
        private readonly PerlinNoise3D _ravineNoise;
        private readonly WorldGenParams _params;

        public CaveCarver(int seed, WorldGenParams parameters)
        {
            _params = parameters;
            _caveNoise = new PerlinNoise3D(seed + 606);
            _wormNoise = new PerlinNoise3D(seed + 707);
            _ravineNoise = new PerlinNoise3D(seed + 808);
        }

        public void CarveChunk(Chunk chunk, TerrainColumn[,] columns)
        {
            if (!_params.EnableCaves)
            {
                return;
            }

            int chunkOffsetX = chunk.ChunkX * Chunk.Width;
            int chunkOffsetZ = chunk.ChunkZ * Chunk.Depth;

            for (int lx = 0; lx < Chunk.Width; lx++)
            {
                for (int lz = 0; lz < Chunk.Depth; lz++)
                {
                    int surfaceHeight = columns[lx, lz].SurfaceHeight;
                    if (surfaceHeight <= 8)
                    {
                        continue;
                    }

                    int maxY = Math.Min(surfaceHeight - 1, Chunk.Height - 1);
                    if (maxY <= 3)
                    {
                        continue;
                    }

                    int wx = chunkOffsetX + lx;
                    int wz = chunkOffsetZ + lz;
                    float caveX = wx * 0.045f;
                    float caveZ = wz * 0.045f;
                    float wormX = wx * 0.08f;
                    float wormZ = wz * 0.08f;
                    float wormXb = wormX + 100f;
                    float wormZb = wormZ + 100f;
                    int cheeseMaxY = surfaceHeight - 4;
                    int wormMaxY = surfaceHeight - 6;

                    for (int y = 1; y < maxY; y++)
                    {
                        BlockType current = chunk.GetBlockUnchecked(lx, y, lz);
                        if (current == BlockType.Air || current == BlockType.Water)
                        {
                            continue;
                        }

                        if (y <= 3)
                        {
                            continue;
                        }

                        if (y <= WorldConstants.SeaLevel && y > surfaceHeight)
                        {
                            continue;
                        }

                        float caveY = y * 0.045f;
                        if (y < cheeseMaxY)
                        {
                            float cave = _caveNoise.Noise(caveX, caveY, caveZ);
                            if (cave > 0.34f)
                            {
                                BlockType carveBlock = y < LavaDepthThreshold ? BlockType.Lava : BlockType.Air;
                                chunk.SetBlockUnchecked(lx, y, lz, carveBlock);
                                continue;
                            }
                        }

                        if (y < wormMaxY)
                        {
                            float wormY = y * 0.08f;
                            float wormA = _wormNoise.Noise(wormX, wormY, wormZ);
                            if (MathF.Abs(wormA) < 0.05f)
                            {
                                float wormB = _wormNoise.Noise(wormXb, wormY + 100f, wormZb);
                                if (MathF.Abs(wormB) < 0.05f)
                                {
                                    BlockType carveBlock = y < LavaDepthThreshold ? BlockType.Lava : BlockType.Air;
                                    chunk.SetBlockUnchecked(lx, y, lz, carveBlock);
                                }
                            }
                        }
                    }

                    // Ravine pass: carve deep open-air V-shaped trenches using low-frequency 2D ridged noise.
                    // Ravines only generate in dry elevated land biomes, not near water or wetlands.
                    var biome = columns[lx, lz].Biome.Primary;
                    bool ravineCapable = biome is BiomeType.Plains or BiomeType.Forest or BiomeType.Jungle
                        or BiomeType.Mountains or BiomeType.SnowyPeaks or BiomeType.BorealTaiga
                        or BiomeType.MushroomForest or BiomeType.Badlands or BiomeType.Desert;

                    if (ravineCapable && surfaceHeight > WorldConstants.SeaLevel + 6)
                    {
                        // Two perpendicular 2D values identify the ravine center-line.
                        float ra = _ravineNoise.Noise(wx * 0.012f, 0f, wz * 0.012f);
                        float rb = _ravineNoise.Noise(wx * 0.012f + 50f, 0f, wz * 0.012f + 50f);

                        // Narrow threshold creates sparse winding paths.
                        float ravineWidth = MathF.Abs(ra) + MathF.Abs(rb);
                        if (ravineWidth < 0.065f)
                        {
                            // Depth of the ravine: 12–26 blocks, modulated by a third noise value.
                            float depthNoise = _ravineNoise.Noise(wx * 0.008f + 200f, 0f, wz * 0.008f - 150f);
                            int ravineDepth = (int)(18f + depthNoise * 8f);
                            ravineDepth = Math.Clamp(ravineDepth, 12, 26);

                            int ravineBottom = Math.Max(surfaceHeight - ravineDepth, WorldConstants.SeaLevel - 2);
                            int ravineTop = surfaceHeight - 1;

                            for (int y = ravineTop; y >= ravineBottom; y--)
                            {
                                // V-shape: the ravine narrows linearly toward the bottom.
                                float progress = (float)(y - ravineBottom) / Math.Max(ravineTop - ravineBottom, 1);
                                float halfWidth = ravineWidth / 0.065f * progress; // 0 at bottom → 1 at top
                                // Only carve within the V-width computed against the raw path strength
                                float pathStrength = MathF.Abs(ra);
                                if (pathStrength < 0.032f + halfWidth * 0.033f)
                                {
                                    BlockType current = chunk.GetBlockUnchecked(lx, y, lz);
                                    if (current != BlockType.Air && current != BlockType.Water && current != BlockType.Lava)
                                    {
                                        chunk.SetBlockUnchecked(lx, y, lz, BlockType.Air);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
    }
}

using System;

namespace Autonocraft.World
{
    public sealed class CaveCarver
    {
        private readonly PerlinNoise3D _caveNoise;
        private readonly PerlinNoise3D _wormNoise;
        private readonly WorldGenParams _params;

        public CaveCarver(int seed, WorldGenParams parameters)
        {
            _params = parameters;
            _caveNoise = new PerlinNoise3D(seed + 606);
            _wormNoise = new PerlinNoise3D(seed + 707);
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
                                BlockType carveBlock = y < 10 ? BlockType.Lava : BlockType.Air;
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
                                    BlockType carveBlock = y < 10 ? BlockType.Lava : BlockType.Air;
                                    chunk.SetBlockUnchecked(lx, y, lz, carveBlock);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

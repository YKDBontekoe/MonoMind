using System;

namespace Autonocraft.World
{
    public sealed class OrePlacer
    {
        private readonly int _seed;
        private readonly WorldGenParams _params;

        public OrePlacer(int seed, WorldGenParams parameters)
        {
            _seed = seed;
            _params = parameters;
        }

        public void PlaceOres(Chunk chunk, TerrainColumn[,] columns)
        {
            if (!_params.EnableOres)
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
                    int yMax = Math.Min(surfaceHeight - WorldConstants.DirtDepth, Chunk.Height - 8);
                    if (yMax < 4)
                    {
                        continue;
                    }

                    int wx = chunkOffsetX + lx;
                    int wz = chunkOffsetZ + lz;
                    for (int y = 4; y <= yMax; y++)
                    {
                        if (chunk.GetBlockUnchecked(lx, y, lz) != BlockType.Stone)
                        {
                            continue;
                        }

                        BlockType? ore = GetOre(wx, y, wz);
                        if (ore.HasValue)
                        {
                            chunk.SetBlockUnchecked(lx, y, lz, ore.Value);
                        }
                    }
                }
            }
        }

        private BlockType? GetOre(int wx, int y, int wz)
        {
            if (y >= 96)
            {
                return null;
            }

            // Large geological strata layers using sine/cosine waves
            float waveA = MathF.Sin(wx * 0.02f) * 10f + MathF.Cos(wz * 0.02f) * 10f;
            float waveB = MathF.Cos(wx * 0.015f) * 8f + MathF.Sin(wz * 0.015f) * 8f;

            if (y >= 30 + waveA && y < 38 + waveA)
            {
                return BlockType.Marble;
            }
            if (y >= 10 + waveB && y < 18 + waveB)
            {
                return BlockType.Basalt;
            }
            if (y >= 20 + waveA && y < 27 + waveA)
            {
                return BlockType.Slate;
            }
            if (y >= 48 + waveB && y < 55 + waveB)
            {
                return BlockType.Limestone;
            }
            if (y >= 62 + waveA && y < 70 + waveA)
            {
                return BlockType.Granite;
            }

            int hash = Hash(wx, y, wz);
            int band = hash % 1000;

            if (y < 48 && band < 45)
            {
                return BlockType.CoalOre;
            }

            if (y >= 24 && band >= 120 && band < 135)
            {
                return BlockType.IronOre;
            }

            if (y >= 8 && y < 48 && band >= 220 && band < 226)
            {
                return BlockType.GoldOre;
            }

            if (y >= 16 && y < 64 && band >= 300 && band < 320)
            {
                return BlockType.CopperOre;
            }

            if (y >= 8 && y < 48 && band >= 400 && band < 412)
            {
                return BlockType.SilverOre;
            }

            if (y < 16 && band >= 500 && band < 503)
            {
                return BlockType.DiamondOre;
            }

            if (y < 20 && band >= 510 && band < 512)
            {
                return BlockType.EmeraldOre;
            }

            if (y < 16 && band >= 520 && band < 523)
            {
                return BlockType.RubyOre;
            }

            if (y < 40 && band >= 530 && band < 542)
            {
                return BlockType.QuartzOre;
            }

            if (y < 12 && band >= 600 && band < 610)
            {
                return BlockType.MagmaBlock;
            }

            if (y < 10 && band >= 620 && band < 628)
            {
                return BlockType.Obsidian;
            }

            return null;
        }

        private int Hash(int wx, int y, int wz)
        {
            unchecked
            {
                int h = wx * 734287 + y * 912271 + wz * 438289 + _seed;
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                return h & int.MaxValue;
            }
        }
    }
}

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

        public void PlaceOres(Chunk chunk)
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
                    for (int y = 4; y < Chunk.Height - 8; y++)
                    {
                        int wx = chunkOffsetX + lx;
                        int wz = chunkOffsetZ + lz;
                        BlockType current = chunk.GetBlock(lx, y, lz);
                        if (current != BlockType.Stone)
                        {
                            continue;
                        }

                        BlockType? ore = GetOre(wx, y, wz);
                        if (ore.HasValue)
                        {
                            chunk.SetBlock(lx, y, lz, ore.Value);
                        }
                    }
                }
            }
        }

        private BlockType? GetOre(int wx, int y, int wz)
        {
            int hash = Hash(wx, y, wz);
            int band = hash % 1000;

            if (y < 48 && band < 45)
            {
                return BlockType.CoalOre;
            }

            if (y >= 24 && y < 96 && band >= 120 && band < 135)
            {
                return BlockType.IronOre;
            }

            if (y >= 8 && y < 48 && band >= 220 && band < 226)
            {
                return BlockType.GoldOre;
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
                return Math.Abs(h);
            }
        }
    }
}

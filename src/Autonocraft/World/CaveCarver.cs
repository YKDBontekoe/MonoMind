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

        public bool ShouldCarve(int wx, int y, int wz, int surfaceHeight)
        {
            if (!_params.EnableCaves || y <= 3 || y >= surfaceHeight - 2)
            {
                return false;
            }

            if (y <= WorldConstants.SeaLevel && y > surfaceHeight)
            {
                return false;
            }

            float cave = _caveNoise.Noise(wx * 0.045f, y * 0.045f, wz * 0.045f);
            float wormA = _wormNoise.Noise(wx * 0.08f, y * 0.08f, wz * 0.08f);
            float wormB = _wormNoise.Noise(wx * 0.08f + 100f, y * 0.08f + 100f, wz * 0.08f + 100f);

            bool cheeseCave = cave > 0.34f && y < surfaceHeight - 4;
            bool wormTunnel = MathF.Abs(wormA) < 0.05f && MathF.Abs(wormB) < 0.05f && y < surfaceHeight - 6;

            return cheeseCave || wormTunnel;
        }

        public void CarveChunk(Chunk chunk, TerrainColumn[,] columns)
        {
            int chunkOffsetX = chunk.ChunkX * Chunk.Width;
            int chunkOffsetZ = chunk.ChunkZ * Chunk.Depth;

            for (int lx = 0; lx < Chunk.Width; lx++)
            {
                for (int lz = 0; lz < Chunk.Depth; lz++)
                {
                    int surfaceHeight = columns[lx, lz].SurfaceHeight;
                    for (int y = 1; y < Chunk.Height; y++)
                    {
                        int wx = chunkOffsetX + lx;
                        int wz = chunkOffsetZ + lz;
                        BlockType current = chunk.GetBlock(lx, y, lz);
                        if (current == BlockType.Air || current == BlockType.Water)
                        {
                            continue;
                        }

                        if (ShouldCarve(wx, y, wz, surfaceHeight))
                        {
                            chunk.SetBlock(lx, y, lz, BlockType.Air);
                        }
                    }
                }
            }
        }
    }
}

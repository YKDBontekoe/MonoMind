namespace Autonocraft.World
{
    /// <summary>
    /// Snapshot of a chunk and its cardinal neighbors for mesh generation without world locks.
    /// </summary>
    internal sealed class MeshBuildContext
    {
        private readonly Chunk _center;
        private readonly Chunk? _negX;
        private readonly Chunk? _posX;
        private readonly Chunk? _negZ;
        private readonly Chunk? _posZ;

        public int Seed { get; }
        public BiomeMap? BiomeMap { get; }

        public MeshBuildContext(
            Chunk center,
            Chunk? negX,
            Chunk? posX,
            Chunk? negZ,
            Chunk? posZ,
            int seed,
            BiomeMap? biomeMap = null)
        {
            _center = center;
            _negX = negX;
            _posX = posX;
            _negZ = negZ;
            _posZ = posZ;
            Seed = seed;
            BiomeMap = biomeMap;
        }

        public BlockType GetBlock(int wx, int wy, int wz)
        {
            if (wy < 0 || wy >= Chunk.Height)
            {
                return BlockType.Air;
            }

            int localX = wx - _center.ChunkX * Chunk.Width;
            int localZ = wz - _center.ChunkZ * Chunk.Depth;
            if (localX >= 0 && localX < Chunk.Width && localZ >= 0 && localZ < Chunk.Depth)
            {
                return _center.GetBlock(localX, wy, localZ);
            }

            Chunk? neighbor = null;
            if (wx < _center.ChunkX * Chunk.Width)
            {
                neighbor = _negX;
            }
            else if (wx >= (_center.ChunkX + 1) * Chunk.Width)
            {
                neighbor = _posX;
            }
            else if (wz < _center.ChunkZ * Chunk.Depth)
            {
                neighbor = _negZ;
            }
            else if (wz >= (_center.ChunkZ + 1) * Chunk.Depth)
            {
                neighbor = _posZ;
            }

            if (neighbor == null)
            {
                return BlockType.Air;
            }

            VoxelWorld.GetChunkCoords(wx, wz, out _, out _, out int lx, out int lz);
            return neighbor.GetBlock(lx, wy, lz);
        }
    }
}

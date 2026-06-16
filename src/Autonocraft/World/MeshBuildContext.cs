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

            int originX = _center.ChunkX << 4;
            int originZ = _center.ChunkZ << 4;
            int localX = wx - originX;
            int localZ = wz - originZ;
            if ((uint)localX < Chunk.Width && (uint)localZ < Chunk.Depth)
            {
                return _center.GetBlock(localX, wy, localZ);
            }

            Chunk? neighbor = null;
            if (wx < originX)
            {
                neighbor = _negX;
            }
            else if (wx >= originX + Chunk.Width)
            {
                neighbor = _posX;
            }
            else if (wz < originZ)
            {
                neighbor = _negZ;
            }
            else if (wz >= originZ + Chunk.Depth)
            {
                neighbor = _posZ;
            }

            if (neighbor == null)
            {
                return BlockType.Air;
            }

            return neighbor.GetBlock(wx & 15, wy, wz & 15);
        }
    }
}

using System;

namespace Autonocraft.World
{
    public class WorldGenerator
    {
        private readonly BiomeMap _biomeMap;
        private readonly TerrainShaper _terrainShaper;
        private readonly CaveCarver _caveCarver;
        private readonly OrePlacer _orePlacer;
        private readonly Decorator _decorator;
        private readonly int _seed;
        private readonly WorldGenParams _params;

        public int Seed => _seed;
        public WorldGenParams Parameters => _params;

        public WorldGenerator(int seed = WorldConstants.DefaultSeed, WorldGenParams? parameters = null)
        {
            _seed = seed;
            _params = parameters ?? WorldGenParams.ForType(WorldType.Default);
            _biomeMap = new BiomeMap(seed, _params);
            _terrainShaper = new TerrainShaper(seed, _biomeMap, _params);
            _caveCarver = new CaveCarver(seed, _params);
            _orePlacer = new OrePlacer(seed, _params);
            _decorator = new Decorator(seed, _params);
        }

        public void GenerateChunkTerrain(Chunk chunk, VoxelWorld? world = null)
        {
            var columns = new TerrainColumn[Chunk.Width, Chunk.Depth];

            for (int lx = 0; lx < Chunk.Width; lx++)
            {
                for (int lz = 0; lz < Chunk.Depth; lz++)
                {
                    int wx = chunk.ChunkX * Chunk.Width + lx;
                    int wz = chunk.ChunkZ * Chunk.Depth + lz;
                    columns[lx, lz] = _terrainShaper.BuildColumn(wx, wz);
                }
            }

            for (int lx = 0; lx < Chunk.Width; lx++)
            {
                for (int lz = 0; lz < Chunk.Depth; lz++)
                {
                    _terrainShaper.FillColumn(chunk, lx, lz, columns[lx, lz]);
                }
            }

            _caveCarver.CarveChunk(chunk, columns);
            _orePlacer.PlaceOres(chunk);
            _decorator.DecorateChunk(chunk, world, columns);
        }

        public TerrainColumn PreviewColumn(int wx, int wz) => _terrainShaper.BuildColumn(wx, wz);
    }
}

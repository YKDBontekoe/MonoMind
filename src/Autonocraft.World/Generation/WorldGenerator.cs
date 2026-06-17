using System;
using System.Collections.Generic;
using Autonocraft.World.Generation.Caves;
using Autonocraft.World.Structures;

namespace Autonocraft.World
{
    public class WorldGenerator
    {
        private readonly BiomeMap _biomeMap;
        private readonly TerrainShaper _terrainShaper;
        private readonly CaveCarver _caveCarver;
        private readonly CaveDecorator _caveDecorator;
        private readonly OrePlacer _orePlacer;
        private readonly Decorator _decorator;
        private readonly StructurePlacer _structurePlacer;
        private readonly int _seed;
        private readonly WorldGenParams _params;

        public int Seed => _seed;
        public WorldGenParams Parameters => _params;
        internal BiomeMap BiomeMap => _biomeMap;

        public WorldGenerator(int seed = WorldConstants.DefaultSeed, WorldGenParams? parameters = null)
        {
            _seed = seed;
            _params = parameters ?? WorldGenParams.ForType(WorldType.Default);
            _biomeMap = new BiomeMap(seed, _params);
            _terrainShaper = new TerrainShaper(seed, _biomeMap, _params);
            _caveCarver = new CaveCarver(seed, _params);
            _caveDecorator = new CaveDecorator(seed);
            _orePlacer = new OrePlacer(seed, _params);
            _decorator = new Decorator(seed, _params);
            _structurePlacer = new StructurePlacer(seed, _params);
        }

        public void GenerateChunkTerrain(Chunk chunk, VoxelWorld? world = null)
        {
            var columns = new TerrainColumn[Chunk.Width, Chunk.Depth];
            var biomeCache = new BiomeSampleCache(_biomeMap);

            TerrainPostProcessor.ProcessChunk(
                chunk.ChunkX,
                chunk.ChunkZ,
                columns,
                (wx, wz) => _terrainShaper.BuildBaseColumn(wx, wz, biomeCache),
                _params.EnableRivers);

            for (int lx = 0; lx < Chunk.Width; lx++)
            {
                for (int lz = 0; lz < Chunk.Depth; lz++)
                {
                    _terrainShaper.FillColumn(chunk, lx, lz, columns[lx, lz]);
                }
            }

            _caveCarver.CarveChunk(chunk, columns);
            _caveDecorator.DecorateChunk(chunk, columns);
            _orePlacer.PlaceOres(chunk, columns);

            var previewCache = new Dictionary<(int cx, int cz), TerrainColumn[,]>
            {
                [(chunk.ChunkX, chunk.ChunkZ)] = columns
            };

            TerrainColumn PreviewColumnCached(int wx, int wz)
            {
                VoxelWorld.GetChunkCoords(wx, wz, out int cx, out int cz, out int lx, out int lz);
                if (!previewCache.TryGetValue((cx, cz), out var cachedColumns))
                {
                    cachedColumns = PreviewChunkColumns(cx, cz);
                    previewCache[(cx, cz)] = cachedColumns;
                }

                return cachedColumns[lx, lz];
            }

            _decorator.DecorateChunk(chunk, world, columns, PreviewColumnCached);
            _structurePlacer.PlaceStructures(chunk, columns, PreviewColumnCached);
        }

        public TerrainColumn PreviewColumn(int wx, int wz)
        {
            VoxelWorld.GetChunkCoords(wx, wz, out int cx, out int cz, out int lx, out int lz);
            return PreviewChunkColumns(cx, cz)[lx, lz];
        }

        public BiomeSample SampleBiome(int wx, int wz) => _biomeMap.Sample(wx, wz);

        public TerrainColumn[,] PreviewChunkColumns(int chunkX, int chunkZ)
        {
            var columns = new TerrainColumn[Chunk.Width, Chunk.Depth];
            var biomeCache = new BiomeSampleCache(_biomeMap);

            TerrainPostProcessor.ProcessChunk(
                chunkX,
                chunkZ,
                columns,
                (x, z) => _terrainShaper.BuildBaseColumn(x, z, biomeCache),
                _params.EnableRivers);

            return columns;
        }
    }
}

using System;
using Autonocraft.World.Generation.Flora;
using Autonocraft.World.Generation.Trees;

namespace Autonocraft.World
{
    public sealed class Decorator
    {
        private readonly TreePlacer _treePlacer;
        private readonly FloraPlacer _floraPlacer;
        private readonly int _seed;

        public Decorator(int seed, WorldGenParams parameters)
        {
            _seed = seed;
            _treePlacer = new TreePlacer(seed, parameters);
            _floraPlacer = new FloraPlacer(seed, parameters);
        }

        public void DecorateChunk(
            Chunk chunk,
            VoxelWorld? world,
            TerrainColumn[,] columns,
            Func<int, int, TerrainColumn>? previewColumn = null)
        {
            int chunkOffsetX = chunk.ChunkX * Chunk.Width;
            int chunkOffsetZ = chunk.ChunkZ * Chunk.Depth;

            for (int lx = 0; lx < Chunk.Width; lx++)
            {
                for (int lz = 0; lz < Chunk.Depth; lz++)
                {
                    int wx = chunkOffsetX + lx;
                    int wz = chunkOffsetZ + lz;
                    var column = columns[lx, lz];
                    int columnHash = GenerationBlocks.Hash(wx, wz, _seed, 17);

                    if (column.Profile.Type == BiomeType.Desert && columnHash % 19 == 0)
                    {
                        for (int dy = 0; dy < 3; dy++)
                        {
                            int qy = column.SurfaceHeight - dy;
                            if (qy > 0 && chunk.GetBlockUnchecked(lx, qy, lz) == BlockType.Sand)
                            {
                                GenerationBlocks.SetBlock(chunk, world, wx, wz, lx, lz, qy, BlockType.Quicksand);
                            }
                        }
                    }

                    if (column.Profile.Type == BiomeType.Volcanic && columnHash % 23 == 0)
                    {
                        int lavaY = column.SurfaceHeight - 1 - columnHash % 4;
                        if (lavaY > 2 && chunk.GetBlockUnchecked(lx, lavaY, lz) == BlockType.Basalt)
                        {
                            GenerationBlocks.SetBlock(chunk, world, wx, wz, lx, lz, lavaY, BlockType.MagmaBlock);
                        }
                    }

                    if (column.Profile.Type == BiomeType.Badlands && columnHash % 17 == 0)
                    {
                        GenerationBlocks.SetBlockIfAir(chunk, world, wx, wz, lx, lz, column.SurfaceHeight + 1, BlockType.Lichen);
                    }

                    PlaceCaveGlowshrooms(chunk, world, wx, wz, lx, lz, column);

                    if (column.Biome.Primary == BiomeType.Mountains && columnHash % 103 == 0)
                    {
                        PlaceRope(chunk, world, wx, wz, lx, lz, column, columnHash);
                    }

                    if (column.Biome.Primary == BiomeType.Ocean || column.IsRiver || column.IsLake)
                    {
                        _floraPlacer.TryPlaceWaterFlora(chunk, world, wx, wz, lx, lz, column);
                        continue;
                    }

                    _treePlacer.TryPlaceTrees(chunk, world, columns, wx, wz, lx, lz, column, previewColumn);
                    _floraPlacer.TryPlaceGroundFlora(chunk, world, wx, wz, lx, lz, column, columnHash);
                    TryPlaceBoulder(chunk, world, wx, wz, lx, lz, column, GenerationBlocks.Hash(wx, wz, _seed, 41));
                }
            }
        }

        private void PlaceCaveGlowshrooms(Chunk chunk, VoxelWorld? world, int wx, int wz, int lx, int lz, TerrainColumn column)
        {
            for (int y = 4; y < column.SurfaceHeight - 3; y++)
            {
                if (y < 40 && chunk.GetBlockUnchecked(lx, y, lz) == BlockType.Air)
                {
                    var below = chunk.GetBlockUnchecked(lx, y - 1, lz);
                    if (below is BlockType.Stone or BlockType.Gravel or BlockType.Dirt)
                    {
                        int caveHash = GenerationBlocks.Hash(wx, y, wz, _seed);
                        if (caveHash % 157 == 0)
                        {
                            GenerationBlocks.SetBlock(chunk, world, wx, wz, lx, lz, y, BlockType.Glowshroom);
                            if (caveHash % 2 == 0)
                            {
                                GenerationBlocks.SetBlockIfAir(chunk, world, wx, wz, lx, lz, y + 1, BlockType.Glowshroom);
                            }

                            if (caveHash % 3 == 0)
                            {
                                GenerationBlocks.SetBlockIfAir(chunk, world, wx + 1, wz, lx + 1, lz, y, BlockType.Glowshroom);
                            }
                        }
                    }
                }
            }
        }

        private static void PlaceRope(Chunk chunk, VoxelWorld? world, int wx, int wz, int lx, int lz, TerrainColumn column, int columnHash)
        {
            for (int y = column.SurfaceHeight + 5; y < Chunk.Height - 5; y++)
            {
                if (chunk.GetBlockUnchecked(lx, y, lz).IsCollidable() && chunk.GetBlockUnchecked(lx, y - 1, lz) == BlockType.Air)
                {
                    int ropeLength = 3 + columnHash % 4;
                    for (int ry = 1; ry <= ropeLength; ry++)
                    {
                        int ropeY = y - ry;
                        if (ropeY > 0 && chunk.GetBlockUnchecked(lx, ropeY, lz) == BlockType.Air)
                        {
                            GenerationBlocks.SetBlock(chunk, world, wx, wz, lx, lz, ropeY, BlockType.Rope);
                        }
                    }

                    break;
                }
            }
        }

        private static void TryPlaceBoulder(Chunk chunk, VoxelWorld? world, int wx, int wz, int lx, int lz, TerrainColumn column, int hash)
        {
            if (hash % 311 != 0 || column.Biome.Primary is BiomeType.Ocean or BiomeType.Beach)
            {
                return;
            }

            int y = column.SurfaceHeight + 1;
            GenerationBlocks.SetBlockIfAir(chunk, world, wx, wz, lx, lz, y, BlockType.Gravel);
            if (hash % 2 == 0)
            {
                GenerationBlocks.SetBlockIfAir(chunk, world, wx, wz, lx, lz, y + 1, BlockType.Stone);
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace Autonocraft.World.Generation.Caves
{
    public sealed class CaveDecorator
    {
        private readonly CaveBiomeMap _caveBiomeMap;
        private readonly int _seed;

        public CaveDecorator(int seed)
        {
            _seed = seed;
            _caveBiomeMap = new CaveBiomeMap(seed);
        }

        public void DecorateChunk(Chunk chunk, TerrainColumn[,] columns)
        {
            int chunkOffsetX = chunk.ChunkX * Chunk.Width;
            int chunkOffsetZ = chunk.ChunkZ * Chunk.Depth;

            for (int lx = 0; lx < Chunk.Width; lx++)
            {
                for (int lz = 0; lz < Chunk.Depth; lz++)
                {
                    int surfaceHeight = columns[lx, lz].SurfaceHeight;
                    if (surfaceHeight <= 10)
                    {
                        continue;
                    }

                    int wx = chunkOffsetX + lx;
                    int wz = chunkOffsetZ + lz;
                    int maxY = Math.Min(surfaceHeight - 2, Chunk.Height - 2);

                    for (int y = 4; y < maxY; y++)
                    {
                        if (chunk.GetBlockUnchecked(lx, y, lz) != BlockType.Air)
                        {
                            continue;
                        }

                        var caveBiome = _caveBiomeMap.Sample(wx, y, wz);
                        var profile = CaveBiomeProfile.For(caveBiome);
                        int hash = GenerationBlocks.Hash3D(wx, y, wz, _seed);

                        ApplyWallAndFloor(chunk, lx, lz, y, profile, hash);
                        PlaceCaveFeatures(chunk, lx, lz, y, wx, wz, caveBiome, profile, hash);
                    }
                }
            }
        }

        private static void ApplyWallAndFloor(Chunk chunk, int lx, int lz, int y, CaveBiomeProfile profile, int hash)
        {
            if (profile.Type == CaveBiomeType.Stone || profile.Type == CaveBiomeType.DeepDark)
            {
                return;
            }

            var below = chunk.GetBlockUnchecked(lx, y - 1, lz);
            if (below is BlockType.Stone or BlockType.Gravel or BlockType.Dirt && hash % 5 == 0)
            {
                chunk.SetBlockUnchecked(lx, y - 1, lz, profile.FloorBlock);
            }

            if (profile.Type == CaveBiomeType.Crystal && hash % 7 == 0)
            {
                TryReplaceAdjacentStone(chunk, lx, lz, y, BlockType.Amethyst);
            }
            else if (hash % 9 == 0)
            {
                TryReplaceAdjacentStone(chunk, lx, lz, y, profile.WallBlock);
            }
        }

        private static void TryReplaceAdjacentStone(Chunk chunk, int lx, int lz, int y, BlockType replacement)
        {
            foreach (var (nx, ny, nz) in Neighbors(lx, y, lz))
            {
                if (nx < 0 || nx >= Chunk.Width || nz < 0 || nz >= Chunk.Depth || ny <= 0 || ny >= Chunk.Height)
                {
                    continue;
                }

                var block = chunk.GetBlockUnchecked(nx, ny, nz);
                if (block is BlockType.Stone or BlockType.Limestone or BlockType.Gravel)
                {
                    chunk.SetBlockUnchecked(nx, ny, nz, replacement);
                    return;
                }
            }
        }

        private static void PlaceCaveFeatures(
            Chunk chunk,
            int lx,
            int lz,
            int y,
            int wx,
            int wz,
            CaveBiomeType caveBiome,
            CaveBiomeProfile profile,
            int hash)
        {
            var floor = chunk.GetBlockUnchecked(lx, y - 1, lz);
            var ceiling = chunk.GetBlockUnchecked(lx, y + 1, lz);
            bool hasFloor = floor.IsCollidable() && floor != BlockType.Water;
            bool hasCeiling = ceiling.IsCollidable();

            switch (caveBiome)
            {
                case CaveBiomeType.Lush:
                    if (hasFloor && hash % 11 == 0)
                    {
                        chunk.SetBlockUnchecked(lx, y, lz, BlockType.MossCarpet);
                    }
                    else if (hasFloor && hash % 17 == 0)
                    {
                        chunk.SetBlockUnchecked(lx, y, lz, BlockType.Fern);
                    }
                    else if (hasCeiling && hash % 23 == 0)
                    {
                        chunk.SetBlockUnchecked(lx, y, lz, BlockType.Glowshroom);
                    }
                    else if (hasFloor && hash % 29 == 0)
                    {
                        chunk.SetBlockUnchecked(lx, y, lz, BlockType.Moss);
                    }

                    break;

                case CaveBiomeType.Dripstone:
                    if (hasCeiling && hash % 13 == 0)
                    {
                        int length = 1 + hash % 3;
                        for (int dy = 0; dy < length && y - dy > 0; dy++)
                        {
                            if (chunk.GetBlockUnchecked(lx, y - dy, lz) == BlockType.Air)
                            {
                                chunk.SetBlockUnchecked(lx, y - dy, lz, BlockType.Dripstone);
                            }
                        }
                    }
                    else if (hasFloor && hash % 19 == 0)
                    {
                        chunk.SetBlockUnchecked(lx, y, lz, BlockType.Dripstone);
                    }

                    break;

                case CaveBiomeType.Crystal:
                    if (hasFloor && hash % 15 == 0)
                    {
                        chunk.SetBlockUnchecked(lx, y, lz, BlockType.Amethyst);
                    }

                    break;

                case CaveBiomeType.Mushroom:
                    if (hasFloor && hash % 9 == 0)
                    {
                        chunk.SetBlockUnchecked(lx, y, lz, hash % 2 == 0 ? BlockType.MushroomRed : BlockType.MushroomBrown);
                    }
                    else if (hasCeiling && hash % 21 == 0)
                    {
                        chunk.SetBlockUnchecked(lx, y, lz, BlockType.Glowshroom);
                    }

                    break;

                case CaveBiomeType.DeepDark:
                    if (hasFloor && hash % 97 == 0)
                    {
                        chunk.SetBlockUnchecked(lx, y, lz, BlockType.Glowshroom);
                    }

                    break;
            }
        }

        private static IEnumerable<(int x, int y, int z)> Neighbors(int x, int y, int z)
        {
            yield return (x + 1, y, z);
            yield return (x - 1, y, z);
            yield return (x, y + 1, z);
            yield return (x, y - 1, z);
            yield return (x, y, z + 1);
            yield return (x, y, z - 1);
        }
    }
}

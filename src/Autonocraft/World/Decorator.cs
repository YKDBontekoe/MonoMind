using System;

namespace Autonocraft.World
{
    public sealed class Decorator
    {
        private readonly NoiseStack _treeNoise;
        private readonly NoiseStack _floraNoise;
        private readonly int _seed;
        private readonly WorldGenParams _params;

        public Decorator(int seed, WorldGenParams parameters)
        {
            _seed = seed;
            _params = parameters;
            _treeNoise = new NoiseStack(seed + 9999);
            _floraNoise = new NoiseStack(seed + 8888);
        }

        public void DecorateChunk(Chunk chunk, VoxelWorld? world, TerrainColumn[,] columns)
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

                    if (column.Biome.Primary == BiomeType.Ocean || column.IsRiver || column.IsLake)
                    {
                        continue;
                    }

                    TryPlaceTree(chunk, world, wx, wz, lx, lz, column);
                    TryPlaceFlora(chunk, world, wx, wz, lx, lz, column);
                    TryPlaceBoulder(chunk, world, wx, wz, lx, lz, column);
                    TryPlaceAnimalFeature(chunk, world, wx, wz, lx, lz, column.SurfaceHeight);
                }
            }
        }

        private void TryPlaceTree(Chunk chunk, VoxelWorld? world, int wx, int wz, int lx, int lz, TerrainColumn column)
        {
            if (column.Profile.TreeDensity <= 0f)
            {
                return;
            }

            float treeDensity = _treeNoise.Fbm(wx * 0.1f, wz * 0.1f, 3);
            float threshold = 0.45f - column.Profile.TreeDensity * 0.35f * _params.TreeDensityScale;
            if (treeDensity <= threshold || (wx * 17 + wz * 31) % 43 != 0)
            {
                return;
            }

            int treeTypeRand = Math.Abs((wx * 73 + wz * 101) % 100);
            BlockType logType = BlockType.OakLog;
            BlockType leafType = BlockType.OakLeaves;

            if (column.Biome.Primary == BiomeType.Swamp)
            {
                logType = BlockType.WillowLog;
                leafType = BlockType.WillowLeaves;
            }
            else if (column.Biome.Primary is BiomeType.Desert or BiomeType.Beach)
            {
                logType = BlockType.PalmLog;
                leafType = BlockType.PalmLeaves;
            }
            else if (column.Biome.Primary == BiomeType.SnowyPeaks || column.Biome.Temperature < -0.05f)
            {
                logType = BlockType.PineLog;
                leafType = BlockType.PineLeaves;
            }
            else if (treeTypeRand < 33)
            {
                logType = BlockType.BirchLog;
                leafType = BlockType.BirchLeaves;
            }
            else if (treeTypeRand < 66)
            {
                logType = BlockType.PineLog;
                leafType = BlockType.PineLeaves;
            }

            int surfaceHeight = column.SurfaceHeight;
            int treeHeight = 4 + (int)((treeDensity - threshold) * 10f) % 4;

            for (int ty = 1; ty <= treeHeight; ty++)
            {
                int y = surfaceHeight + ty;
                if (y < Chunk.Height)
                {
                    SetBlock(chunk, world, wx, wz, lx, lz, y, logType);
                }
            }

            int canopyBottom = surfaceHeight + treeHeight - 2;
            int canopyTop = surfaceHeight + treeHeight + 1;
            int canopyLayers = canopyTop - canopyBottom;

            for (int y = canopyBottom; y <= canopyTop; y++)
            {
                int layerFromBottom = y - canopyBottom;
                int radius = GetCanopyRadius(logType, layerFromBottom, canopyLayers);

                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        if (dx * dx + dz * dz > radius * radius)
                        {
                            continue;
                        }

                        if (dx == 0 && dz == 0 && y <= surfaceHeight + treeHeight)
                        {
                            continue;
                        }

                        int leafWx = wx + dx;
                        int leafWz = wz + dz;
                        int leafLx = lx + dx;
                        int leafLz = lz + dz;

                        if (y <= 0 || y >= Chunk.Height)
                        {
                            continue;
                        }

                        SetBlockIfAir(chunk, world, leafWx, leafWz, leafLx, leafLz, y, leafType);
                    }
                }
            }
        }

        private void TryPlaceFlora(Chunk chunk, VoxelWorld? world, int wx, int wz, int lx, int lz, TerrainColumn column)
        {
            int surfaceHeight = column.SurfaceHeight;
            float floraSample = _floraNoise.Fbm(wx * 0.21f, wz * 0.21f, 3);
            int hash = Hash(wx, wz, 17);

            if (column.Profile.AllowCactus && floraSample > 0.88f && hash % 11 == 0)
            {
                SetBlockIfAir(chunk, world, wx, wz, lx, lz, surfaceHeight + 1, BlockType.Cactus);
                return;
            }

            if (column.Profile.AllowTallGrass)
            {
                float grassThreshold = column.Biome.Primary switch
                {
                    BiomeType.Forest => 0.76f,
                    BiomeType.Plains => 0.68f,
                    BiomeType.Swamp => 0.74f,
                    _ => 0.72f
                };
                if (floraSample > grassThreshold)
                {
                    SetBlockIfAir(chunk, world, wx, wz, lx, lz, surfaceHeight + 1, BlockType.TallGrass);
                }
            }

            if (column.Profile.AllowFlowers)
            {
                float flowerThreshold = 0.82f;
                float sunflowerThreshold = 0.91f;
                if (floraSample > sunflowerThreshold && hash % 3 == 0)
                {
                    SetBlockIfAir(chunk, world, wx, wz, lx, lz, surfaceHeight + 1, BlockType.Sunflower);
                }
                else if (floraSample > flowerThreshold)
                {
                    SetBlockIfAir(chunk, world, wx, wz, lx, lz, surfaceHeight + 1, BlockType.Flower);
                }
            }

            if (column.Biome.Primary == BiomeType.Swamp && floraSample > 0.70f && hash % 5 == 0)
            {
                SetBlockIfAir(chunk, world, wx, wz, lx, lz, surfaceHeight + 1, BlockType.Reed);
            }
        }

        private void TryPlaceBoulder(Chunk chunk, VoxelWorld? world, int wx, int wz, int lx, int lz, TerrainColumn column)
        {
            int hash = Hash(wx, wz, 41);
            if (hash % 311 != 0 || column.Biome.Primary is BiomeType.Ocean or BiomeType.Beach)
            {
                return;
            }

            int y = column.SurfaceHeight + 1;
            SetBlockIfAir(chunk, world, wx, wz, lx, lz, y, BlockType.Gravel);
            if (hash % 2 == 0)
            {
                SetBlockIfAir(chunk, world, wx, wz, lx, lz, y + 1, BlockType.Stone);
            }
        }

        private void TryPlaceAnimalFeature(Chunk chunk, VoxelWorld? world, int wx, int wz, int lx, int lz, int surfaceHeight)
        {
            int hash = Math.Abs((wx * 811 + wz * 977 + _seed) % 10000);
            if (hash % 800 == 0)
            {
                PlaceHayBale(chunk, world, wx, wz, lx, lz, surfaceHeight);
            }
            else if (hash % 1200 == 17)
            {
                PlaceNest(chunk, world, wx, wz, lx, lz, surfaceHeight);
            }
            else if (hash % 1500 == 42)
            {
                PlaceSheepRock(chunk, world, wx, wz, lx, lz, surfaceHeight);
            }
        }

        private static void PlaceHayBale(Chunk chunk, VoxelWorld? world, int wx, int wz, int lx, int lz, int surfaceHeight)
        {
            for (int dy = 1; dy <= 2; dy++)
            {
                int y = surfaceHeight + dy;
                if (y >= Chunk.Height) return;
                SetBlockIfAir(chunk, world, wx, wz, lx, lz, y, BlockType.HayBale);
            }
        }

        private static void PlaceNest(Chunk chunk, VoxelWorld? world, int wx, int wz, int lx, int lz, int surfaceHeight)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    int y = surfaceHeight + 1;
                    if (y >= Chunk.Height) continue;
                    BlockType type = (dx == 0 && dz == 0) ? BlockType.Dirt : BlockType.Grass;
                    SetBlockIfAir(chunk, world, wx + dx, wz + dz, lx + dx, lz + dz, y, type);
                }
            }
        }

        private static void PlaceSheepRock(Chunk chunk, VoxelWorld? world, int wx, int wz, int lx, int lz, int surfaceHeight)
        {
            int y = surfaceHeight + 1;
            if (y >= Chunk.Height) return;
            SetBlockIfAir(chunk, world, wx, wz, lx, lz, y, BlockType.Stone);
        }

        private static int GetCanopyRadius(BlockType logType, int layerFromBottom, int totalLayers)
        {
            if (logType == BlockType.PineLog)
            {
                return layerFromBottom <= 1 ? 2 : 1;
            }

            if (logType == BlockType.WillowLog)
            {
                if (layerFromBottom >= totalLayers - 1)
                {
                    return 3;
                }

                return layerFromBottom <= 1 ? 2 : 3;
            }

            if (logType == BlockType.PalmLog)
            {
                return layerFromBottom >= totalLayers - 1 ? 2 : 0;
            }

            int peakLayer = totalLayers / 2;
            return Math.Max(1, 2 - Math.Abs(layerFromBottom - peakLayer));
        }

        private static void SetBlock(Chunk chunk, VoxelWorld? world, int wx, int wz, int lx, int lz, int y, BlockType type)
        {
            if (world != null)
            {
                world.SetBlockDuringGeneration(wx, y, wz, type);
                return;
            }

            if (lx >= 0 && lx < Chunk.Width && lz >= 0 && lz < Chunk.Depth)
            {
                chunk.SetBlock(lx, y, lz, type);
            }
        }

        private static void SetBlockIfAir(Chunk chunk, VoxelWorld? world, int wx, int wz, int lx, int lz, int y, BlockType type)
        {
            if (y <= 0 || y >= Chunk.Height)
            {
                return;
            }

            if (world != null)
            {
                if (world.GetBlock(wx, y, wz) == BlockType.Air)
                {
                    world.SetBlockDuringGeneration(wx, y, wz, type);
                }
                return;
            }

            if (lx >= 0 && lx < Chunk.Width && lz >= 0 && lz < Chunk.Depth)
            {
                if (chunk.GetBlock(lx, y, lz) == BlockType.Air)
                {
                    chunk.SetBlock(lx, y, lz, type);
                }
            }
        }

        private int Hash(int wx, int wz, int salt)
        {
            unchecked
            {
                return Math.Abs((wx * 92821 + wz * 68917 + _seed + salt) % 100000);
            }
        }
    }
}

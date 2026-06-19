using System;

namespace Autonocraft.World.Generation.Flora
{
    public sealed class FloraPlacer
    {
        private readonly NoiseStack _floraNoise;
        private readonly int _seed;
        private readonly WorldGenParams _params;

        public FloraPlacer(int seed, WorldGenParams parameters)
        {
            _seed = seed;
            _params = parameters;
            _floraNoise = new NoiseStack(seed + 8888);
        }

        public void TryPlaceGroundFlora(
            Chunk chunk,
            VoxelWorld? world,
            int wx,
            int wz,
            int lx,
            int lz,
            TerrainColumn column,
            int hash)
        {
            int surfaceHeight = column.SurfaceHeight;
            BlockType surface = VegetationSurfaces.GetSurfaceBlock(chunk, world, wx, wz, lx, lz, surfaceHeight);

            float patchNoise = _floraNoise.Fbm(wx * 0.045f, wz * 0.045f, 2);
            float detailNoise = _floraNoise.Fbm(wx * 0.22f, wz * 0.22f, 3);
            float floraSample = patchNoise * 0.45f + detailNoise * 0.55f;
            float densityGate = 0.38f - column.Profile.FloraDensity * 0.28f * _params.FloraDensityScale;
            bool placedFlora = false;
            if (floraSample >= densityGate)
            {
                if ((column.Profile.Type == BiomeType.Swamp || column.Profile.Type == BiomeType.Beach)
                    && floraSample > 0.82f
                    && PassesRarityGate(hash, 9))
                {
                    if (surface is BlockType.Mud or BlockType.Sand or BlockType.Grass)
                    {
                        int bambooHeight = 2 + hash % 3;
                        for (int by = 1; by <= bambooHeight; by++)
                        {
                            GenerationBlocks.SetBlockIfAir(chunk, world, wx, wz, lx, lz, surfaceHeight + by, BlockType.Bamboo);
                        }
                    }

                    return;
                }

                bool underCanopy = IsUnderLeafCanopy(chunk, world, wx, wz, lx, lz, surfaceHeight);
                var entries = FloraPlacementRules.For(column.Biome.Primary);
                if (FloraPlacementRules.TryPick(entries, column.Profile, floraSample, hash, underCanopy, out var picked)
                    || FloraPlacementRules.TryPick(entries, column.Profile, floraSample, hash, !underCanopy, out picked))
                {
                    if (VegetationSurfaces.CanPlaceFlora(surface, column.Biome.Primary, picked.Block))
                    {
                        GenerationBlocks.SetBlockIfAir(chunk, world, wx, wz, lx, lz, surfaceHeight + 1, picked.Block);
                        placedFlora = chunk.GetBlockUnchecked(lx, surfaceHeight + 1, lz) == picked.Block;
                        if (placedFlora)
                        {
                            TryPlaceFloraCluster(chunk, world, wx, wz, lx, lz, column, surfaceHeight, picked.Block, floraSample, hash);
                        }
                    }
                }
            }

            if (!placedFlora)
            {
                TryPlaceMeadowLayer(chunk, world, wx, wz, lx, lz, column, surface, surfaceHeight, hash);
            }

            if (!placedFlora && floraSample >= densityGate)
            {
                TryPlaceSurfaceGrowth(chunk, world, wx, wz, lx, lz, column, surface, surfaceHeight, floraSample, hash);
            }
            else if (placedFlora)
            {
                TryPlaceMoss(chunk, world, wx, wz, lx, lz, column, surfaceHeight, hash);
            }
        }

        private void TryPlaceSurfaceGrowth(
            Chunk chunk,
            VoxelWorld? world,
            int wx,
            int wz,
            int lx,
            int lz,
            TerrainColumn column,
            BlockType surface,
            int surfaceHeight,
            float floraSample,
            int hash)
        {
            TryPlaceMoss(chunk, world, wx, wz, lx, lz, column, surfaceHeight, hash);

            if (floraSample < 0.44f)
            {
                return;
            }

            if (VegetationSurfaces.CanPlaceFlora(surface, column.Biome.Primary, BlockType.Lichen)
                && PassesRarityGate(hash, 11))
            {
                GenerationBlocks.SetBlockIfAir(chunk, world, wx, wz, lx, lz, surfaceHeight + 1, BlockType.Lichen);
            }
        }

        private void TryPlaceMeadowLayer(
            Chunk chunk,
            VoxelWorld? world,
            int wx,
            int wz,
            int lx,
            int lz,
            TerrainColumn column,
            BlockType surface,
            int surfaceHeight,
            int hash)
        {
            if (!column.Profile.AllowTallGrass)
            {
                return;
            }

            if (surface is not (BlockType.Grass or BlockType.Dirt))
            {
                return;
            }

            float meadowNoise = _floraNoise.Fbm(wx * 0.16f, wz * 0.16f, 3);
            float meadowGate = 0.30f - column.Profile.FloraDensity * 0.18f * _params.FloraDensityScale;
            if (meadowNoise < meadowGate)
            {
                return;
            }

            GenerationBlocks.SetBlockIfAir(chunk, world, wx, wz, lx, lz, surfaceHeight + 1, BlockType.TallGrass);

            if (meadowNoise > 0.62f && PassesRarityGate(hash, 4))
            {
                int patchHash = GenerationBlocks.Hash(wx, wz, _seed, 127);
                int spread = 1 + patchHash % 3;
                for (int i = 1; i <= spread; i++)
                {
                    int offsetHash = GenerationBlocks.Hash(wx, wz, _seed, 127 + i);
                    int dx = (offsetHash % 5) - 2;
                    int dz = ((offsetHash / 5) % 5) - 2;
                    if (dx == 0 && dz == 0)
                    {
                        continue;
                    }

                    int nx = wx + dx;
                    int nz = wz + dz;
                    BlockType neighborSurface = VegetationSurfaces.GetSurfaceBlock(
                        chunk, world, nx, nz, lx + dx, lz + dz, surfaceHeight);
                    if (neighborSurface is BlockType.Grass or BlockType.Dirt)
                    {
                        GenerationBlocks.SetBlockIfAir(
                            chunk, world, nx, nz, lx + dx, lz + dz, surfaceHeight + 1, BlockType.TallGrass);
                    }
                }
            }
        }

        private void TryPlaceFloraCluster(
            Chunk chunk,
            VoxelWorld? world,
            int wx,
            int wz,
            int lx,
            int lz,
            TerrainColumn column,
            int surfaceHeight,
            BlockType flora,
            float floraSample,
            int hash)
        {
            if (floraSample < 0.55f || !PassesRarityGate(hash, 5))
            {
                return;
            }

            int clusterSize = 2 + hash % 4;
            for (int i = 1; i <= clusterSize; i++)
            {
                int offsetHash = GenerationBlocks.Hash(wx, wz, _seed, 97 + i);
                int dx = (offsetHash % 7) - 3;
                int dz = ((offsetHash / 7) % 7) - 3;
                if (dx == 0 && dz == 0)
                {
                    continue;
                }

                int nx = wx + dx;
                int nz = wz + dz;
                int nlx = lx + dx;
                int nlz = lz + dz;
                BlockType neighborSurface = VegetationSurfaces.GetSurfaceBlock(
                    chunk, world, nx, nz, nlx, nlz, surfaceHeight);
                if (!VegetationSurfaces.CanPlaceFlora(neighborSurface, column.Biome.Primary, flora))
                {
                    continue;
                }

                GenerationBlocks.SetBlockIfAir(chunk, world, nx, nz, nlx, nlz, surfaceHeight + 1, flora);
            }
        }

        private void TryPlaceMoss(
            Chunk chunk,
            VoxelWorld? world,
            int wx,
            int wz,
            int lx,
            int lz,
            TerrainColumn column,
            int surfaceHeight,
            int hash)
        {
            if (column.Biome.Primary is not (BiomeType.Swamp or BiomeType.Mountains))
            {
                return;
            }

            if (!PassesRarityGate(hash, 23))
            {
                return;
            }

            BlockType surface = chunk.GetBlockUnchecked(lx, surfaceHeight, lz);
            if (!VegetationSurfaces.CanPlaceMoss(surface))
            {
                return;
            }

            GenerationBlocks.SetBlockIfAir(chunk, world, wx, wz, lx, lz, surfaceHeight + 1, BlockType.Moss);
        }

        public void TryUnderstory(
            Chunk chunk,
            VoxelWorld? world,
            int wx,
            int wz,
            int lx,
            int lz,
            TerrainColumn column,
            int hash)
        {
            if (!column.Profile.AllowUnderstory)
            {
                return;
            }

            float floraSample = _floraNoise.Fbm(wx * 0.21f, wz * 0.21f, 3);
            if (floraSample < 0.44f)
            {
                return;
            }

            int surfaceHeight = column.SurfaceHeight;
            if (!IsUnderLeafCanopy(chunk, world, wx, wz, lx, lz, surfaceHeight))
            {
                return;
            }

            BlockType surface = VegetationSurfaces.GetSurfaceBlock(chunk, world, wx, wz, lx, lz, surfaceHeight);
            var entries = FloraPlacementRules.For(column.Biome.Primary);
            if (!FloraPlacementRules.TryPick(entries, column.Profile, floraSample, hash, underCanopy: true, out var picked))
            {
                return;
            }

            if (!VegetationSurfaces.CanPlaceFlora(surface, column.Biome.Primary, picked.Block))
            {
                return;
            }

            GenerationBlocks.SetBlockIfAir(chunk, world, wx, wz, lx, lz, surfaceHeight + 1, picked.Block);
        }

        public void TryPlaceWaterFlora(Chunk chunk, VoxelWorld? world, int wx, int wz, int lx, int lz, TerrainColumn column)
        {
            int hash = GenerationBlocks.Hash(wx, wz, _seed, 23);
            int waterY = column.Biome.Primary == BiomeType.Ocean
                ? WorldConstants.SeaLevel
                : FindWaterSurfaceY(chunk, lx, lz);

            if (waterY < 0)
            {
                return;
            }

            if (column.Biome.Primary == BiomeType.Ocean
                && chunk.GetBlockUnchecked(lx, waterY, lz) != BlockType.Water)
            {
                waterY = FindWaterSurfaceY(chunk, lx, lz);
                if (waterY < 0)
                {
                    return;
                }
            }

            if (column.Biome.Primary == BiomeType.Swamp && PassesRarityGate(hash, 5))
            {
                int padY = waterY + 1;
                if (padY < Chunk.Height && chunk.GetBlockUnchecked(lx, padY, lz) == BlockType.Air)
                {
                    GenerationBlocks.SetBlockIfAir(chunk, world, wx, wz, lx, lz, padY, BlockType.LilyPad);
                }
            }

            int floorY = FindWaterFloorY(chunk, lx, lz, waterY);
            if (floorY == -1)
            {
                return;
            }

            BlockType floorType = chunk.GetBlockUnchecked(lx, floorY, lz);
            if (floorType is not (BlockType.Sand or BlockType.Dirt or BlockType.Grass or BlockType.Mud or BlockType.Clay))
            {
                return;
            }

            int grassY = floorY + 1;
            if (grassY >= Chunk.Height || chunk.GetBlockUnchecked(lx, grassY, lz) != BlockType.Water)
            {
                return;
            }

            int seagrassMod = Math.Max(3, (int)(6f - column.Profile.FloraDensity * 3f * _params.FloraDensityScale));
            if (hash % seagrassMod == 0)
            {
                GenerationBlocks.SetBlock(chunk, world, wx, wz, lx, lz, grassY, BlockType.Seagrass);
            }
            else if (column.Biome.Primary == BiomeType.Ocean && PassesRarityGate(hash, 5))
            {
                int depth = waterY - floorY;
                if (depth is >= 4 and <= 12)
                {
                    int kelpHeight = 3 + hash % 6;
                    for (int ky = 1; ky <= kelpHeight; ky++)
                    {
                        int kelpY = floorY + ky;
                        if (kelpY < waterY && kelpY < Chunk.Height && chunk.GetBlockUnchecked(lx, kelpY, lz) == BlockType.Water)
                        {
                            GenerationBlocks.SetBlock(chunk, world, wx, wz, lx, lz, kelpY, BlockType.Kelp);
                        }
                    }
                }
            }
        }

        private static bool PassesRarityGate(int hash, int rarity)
        {
            return Math.Abs(hash % rarity) == 0;
        }

        private static bool IsUnderLeafCanopy(Chunk chunk, VoxelWorld? world, int wx, int wz, int lx, int lz, int surfaceHeight)
        {
            int maxY = Math.Min(surfaceHeight + 14, Chunk.Height - 1);
            for (int checkY = surfaceHeight + 2; checkY <= maxY; checkY++)
            {
                if (world != null)
                {
                    if (world.GetBlock(wx, checkY, wz).IsAnyLeaves())
                    {
                        return true;
                    }

                    continue;
                }

                if (lx >= 0 && lx < Chunk.Width && lz >= 0 && lz < Chunk.Depth)
                {
                    if (chunk.GetBlockUnchecked(lx, checkY, lz).IsAnyLeaves())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int FindWaterSurfaceY(Chunk chunk, int lx, int lz)
        {
            for (int y = Chunk.Height - 1; y >= 0; y--)
            {
                if (chunk.GetBlockUnchecked(lx, y, lz) == BlockType.Water)
                {
                    return y;
                }
            }

            return -1;
        }

        private static int FindWaterFloorY(Chunk chunk, int lx, int lz, int waterY)
        {
            for (int y = waterY - 1; y >= 0; y--)
            {
                if (chunk.GetBlockUnchecked(lx, y, lz) != BlockType.Water)
                {
                    return y;
                }
            }

            return -1;
        }
    }
}

using System;
using Autonocraft.World.Generation.Flora;

namespace Autonocraft.World.Generation.Trees
{
    public sealed class TreePlacer
    {
        private readonly NoiseStack _treeNoise;
        private readonly FloraPlacer _floraPlacer;
        private readonly int _seed;
        private readonly WorldGenParams _params;

        public TreePlacer(int seed, WorldGenParams parameters)
        {
            _seed = seed;
            _params = parameters;
            _treeNoise = new NoiseStack(seed + 9999);
            _floraPlacer = new FloraPlacer(seed, parameters);
        }

        public void TryPlaceTrees(
            Chunk chunk,
            VoxelWorld? world,
            TerrainColumn[,] columns,
            int wx,
            int wz,
            int lx,
            int lz,
            TerrainColumn column,
            Func<int, int, TerrainColumn>? previewColumn)
        {
            if (column.Profile.TreeDensity <= 0f || column.IsRiver || column.IsLake)
            {
                return;
            }

            int surfaceHeight = column.SurfaceHeight;
            BlockType surface = VegetationSurfaces.GetSurfaceBlock(chunk, world, wx, wz, lx, lz, surfaceHeight);
            if (!VegetationSurfaces.CanPlaceTree(surface, column.Biome.Primary))
            {
                return;
            }

            // Low-frequency noise defines organic forest patches (no grid alignment).
            float patchNoise = _treeNoise.Fbm(wx * 0.038f, wz * 0.038f, 2);
            float patchThreshold = 0.50f - column.Profile.TreeDensity * 0.32f * _params.TreeDensityScale;
            if (patchNoise <= patchThreshold)
            {
                return;
            }

            // Higher-frequency noise picks individual tree sites inside a patch.
            float treeNoise = _treeNoise.Fbm(wx * 0.14f + 50f, wz * 0.14f, 3);
            float treeThreshold = 0.38f - column.Profile.TreeDensity * 0.22f * _params.TreeDensityScale;
            if (treeNoise <= treeThreshold)
            {
                return;
            }

            // Extra spacing jitter so trees don't fill every column in a patch.
            float spacingNoise = _treeNoise.Fbm(wx * 0.31f, wz * 0.31f, 2);
            if (spacingNoise < 0.32f)
            {
                return;
            }

            var species = TreeSpeciesRegistry.PickSpecies(column.Biome, wx, wz, _seed);
            float scale = ResolveTreeScale(column, wx, wz, treeNoise);
            if (scale >= 2f)
            {
                species = TreeSpeciesRegistry.PickMegaSpecies(column.Biome, wx, wz, _seed);
            }

            PlaceShapedTree(chunk, world, wx, wz, lx, lz, column, species, treeNoise, treeThreshold, scale);

            if (column.Profile.AllowUnderstory)
            {
                int understoryHash = GenerationBlocks.Hash(wx, wz, _seed, 53);
                _floraPlacer.TryUnderstory(chunk, world, wx, wz, lx, lz, column, understoryHash);
            }

            TryPlaceTreeCluster(chunk, world, columns, previewColumn, wx, wz, column, treeNoise, treeThreshold);

            if (column.Profile.Type == BiomeType.Forest)
            {
                int groveHash = GenerationBlocks.Hash(wx, wz, _seed, 31);
                TryPlaceFallenLog(chunk, world, wx, wz, lx, lz, column, groveHash);
            }
        }

        private void TryPlaceTreeCluster(
            Chunk chunk,
            VoxelWorld? world,
            TerrainColumn[,] columns,
            Func<int, int, TerrainColumn>? previewColumn,
            int wx,
            int wz,
            TerrainColumn column,
            float treeNoise,
            float treeThreshold)
        {
            int clusterHash = GenerationBlocks.Hash(wx, wz, _seed, 41);
            if (clusterHash % 11 != 0)
            {
                return;
            }

            int companions = 1 + clusterHash % 3;
            int radius = 2 + clusterHash % 4;

            for (int i = 0; i < companions; i++)
            {
                int offsetHash = GenerationBlocks.Hash(wx, wz, _seed, 41 + i * 13);
                int dx = (offsetHash % (radius * 2 + 1)) - radius;
                int dz = ((offsetHash / 7) % (radius * 2 + 1)) - radius;
                if (dx == 0 && dz == 0)
                {
                    continue;
                }

                int treeWx = wx + dx;
                int treeWz = wz + dz;
                if (!TryResolveColumn(columns, chunk, previewColumn, treeWx, treeWz, out var treeColumn, out int treeLx, out int treeLz))
                {
                    continue;
                }

                if (treeColumn.Profile.TreeDensity <= 0f || treeColumn.IsRiver || treeColumn.IsLake)
                {
                    continue;
                }

                BlockType companionSurface = VegetationSurfaces.GetSurfaceBlock(
                    chunk, world, treeWx, treeWz, treeLx, treeLz, treeColumn.SurfaceHeight);
                if (!VegetationSurfaces.CanPlaceTree(companionSurface, treeColumn.Biome.Primary))
                {
                    continue;
                }

                float companionNoise = _treeNoise.Fbm(treeWx * 0.14f + 50f, treeWz * 0.14f, 3);
                if (companionNoise <= treeThreshold - 0.05f)
                {
                    continue;
                }

                var species = TreeSpeciesRegistry.PickSpecies(treeColumn.Biome, treeWx, treeWz, _seed);
                float companionScale = ResolveTreeScale(treeColumn, treeWx, treeWz, companionNoise);
                if (companionScale >= 2f)
                {
                    species = TreeSpeciesRegistry.PickMegaSpecies(treeColumn.Biome, treeWx, treeWz, _seed);
                }

                PlaceShapedTree(chunk, world, treeWx, treeWz, treeLx, treeLz, treeColumn, species, companionNoise, treeThreshold, companionScale);
            }
        }

        private float ResolveTreeScale(TerrainColumn column, int wx, int wz, float treeNoise)
        {
            if (column.Profile.Type is not (BiomeType.Forest or BiomeType.Swamp or BiomeType.Plains))
            {
                return 1f;
            }

            int megaHash = GenerationBlocks.Hash(wx, wz, _seed, 211);
            if (megaHash % 160 != 0 || treeNoise < 0.68f)
            {
                return 1f;
            }

            return 2.2f + (megaHash % 80) / 100f;
        }

        private void PlaceShapedTree(
            Chunk chunk,
            VoxelWorld? world,
            int wx,
            int wz,
            int lx,
            int lz,
            TerrainColumn column,
            TreeSpecies species,
            float treeDensity,
            float threshold,
            float scale = 1f)
        {
            int surfaceHeight = column.SurfaceHeight;
            var voxels = TreeShapeGenerator.Generate(species, wx, wz, surfaceHeight, _seed, treeDensity, threshold, scale);

            foreach (var voxel in voxels)
            {
                int y = surfaceHeight + voxel.Dy;
                GenerationBlocks.SetBlockIfAir(
                    chunk,
                    world,
                    wx + voxel.Dx,
                    wz + voxel.Dz,
                    lx + voxel.Dx,
                    lz + voxel.Dz,
                    y,
                    voxel.Type);
            }

            if (column.Profile.Type is BiomeType.Forest or BiomeType.Swamp)
            {
                ApplyVinesPostPass(chunk, world, wx, wz, lx, lz, surfaceHeight, species, voxels);
            }
        }

        private static void ApplyVinesPostPass(
            Chunk chunk,
            VoxelWorld? world,
            int wx,
            int wz,
            int lx,
            int lz,
            int surfaceHeight,
            TreeSpecies species,
            System.Collections.Generic.List<TreeVoxel> voxels)
        {
            foreach (var voxel in voxels)
            {
                if (voxel.Type != species.Leaves)
                {
                    continue;
                }

                int leafWx = wx + voxel.Dx;
                int leafWz = wz + voxel.Dz;
                int leafY = surfaceHeight + voxel.Dy;
                if ((leafWx * 13 + leafWz * 37 + leafY * 7) % 15 != 0)
                {
                    continue;
                }

                int vineLength = 1 + Math.Abs(leafWx * 7 + leafWz * 11) % 3;
                for (int vy = 1; vy <= vineLength; vy++)
                {
                    int vineY = leafY - vy;
                    if (vineY > surfaceHeight)
                    {
                        GenerationBlocks.SetBlockIfAir(
                            chunk,
                            world,
                            leafWx,
                            leafWz,
                            lx + voxel.Dx,
                            lz + voxel.Dz,
                            vineY,
                            BlockType.Vine);
                    }
                }
            }
        }

        private void TryPlaceFallenLog(
            Chunk chunk,
            VoxelWorld? world,
            int wx,
            int wz,
            int lx,
            int lz,
            TerrainColumn column,
            int groveHash)
        {
            int hash = GenerationBlocks.Hash(wx, wz, _seed, 83);
            if (hash % 400 != 0)
            {
                return;
            }

            var species = TreeSpeciesRegistry.PickSpecies(column.Biome, wx, wz, _seed);
            int length = 2 + groveHash % 3;
            bool alongX = hash % 2 == 0;
            for (int i = 0; i < length; i++)
            {
                int logWx = wx + (alongX ? i : 0);
                int logWz = wz + (alongX ? 0 : i);
                GenerationBlocks.SetBlockIfAir(
                    chunk,
                    world,
                    logWx,
                    logWz,
                    lx + (alongX ? i : 0),
                    lz + (alongX ? 0 : i),
                    column.SurfaceHeight + 1,
                    species.Log);
            }
        }

        private static bool TryResolveColumn(
            TerrainColumn[,] columns,
            Chunk chunk,
            Func<int, int, TerrainColumn>? previewColumn,
            int wx,
            int wz,
            out TerrainColumn column,
            out int lx,
            out int lz)
        {
            int chunkOffsetX = chunk.ChunkX * Chunk.Width;
            int chunkOffsetZ = chunk.ChunkZ * Chunk.Depth;
            lx = wx - chunkOffsetX;
            lz = wz - chunkOffsetZ;

            if (lx >= 0 && lx < Chunk.Width && lz >= 0 && lz < Chunk.Depth)
            {
                column = columns[lx, lz];
                return true;
            }

            if (previewColumn != null)
            {
                column = previewColumn(wx, wz);
                return true;
            }

            column = default;
            return false;
        }
    }
}

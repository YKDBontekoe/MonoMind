using System;
using Autonocraft.Domain.World;
using Autonocraft.World.Containers;

namespace Autonocraft.World.Structures
{
    public sealed class StructurePlacer
    {
        private const int SmallCellSize = 96;
        private const int MediumCellSize = 192;
        private const int LargeCellSize = 384;
        private const int MegaCellSize = 640;
        private const int SmallRarity = 900;
        private const int MediumRarity = 5000;
        private const int LargeRarity = 12000;
        private const int MegaRarity = 32000;
        private const int SpawnRemainder = 37;
        private const int MaxSearchRadius = 12;
        private const int MaxFlatnessDelta = 2;
        private const int MegaFlatnessDelta = 6;

        private readonly int _seed;
        private readonly WorldGenParams _params;

        public StructurePlacer(int seed, WorldGenParams parameters)
        {
            _seed = seed;
            _params = parameters;
        }

        public void PlaceStructures(
            Chunk chunk,
            TerrainColumn[,] columns,
            Func<int, int, TerrainColumn> previewColumn,
            VoxelWorld? world = null)
        {
            if (StructureGallery.IsGalleryWorld(_params.WorldType))
            {
                PlaceGalleryGrid(chunk, world);
                return;
            }

            if (!_params.EnableStructures)
            {
                return;
            }

            PlaceTier(chunk, columns, previewColumn, StructureTier.Small, SmallCellSize, SmallRarity, 11, MaxFlatnessDelta, world);
            PlaceTier(chunk, columns, previewColumn, StructureTier.Medium, MediumCellSize, MediumRarity, 29, MaxFlatnessDelta, world);
            PlaceTier(chunk, columns, previewColumn, StructureTier.Large, LargeCellSize, LargeRarity, 53, MaxFlatnessDelta + 1, world);
            PlaceTier(chunk, columns, previewColumn, StructureTier.Mega, MegaCellSize, MegaRarity, 71, MegaFlatnessDelta, world);
        }

        private void PlaceTier(
            Chunk chunk,
            TerrainColumn[,] columns,
            Func<int, int, TerrainColumn> previewColumn,
            StructureTier tier,
            int cellSize,
            int baseRarity,
            int salt,
            int maxFlatnessDelta,
            VoxelWorld? world)
        {
            float density = Math.Max(0.1f, _params.StructureDensityScale);
            int rarity = Math.Max(64, (int)(baseRarity / density));

            int chunkMinX = chunk.ChunkX * Chunk.Width;
            int chunkMinZ = chunk.ChunkZ * Chunk.Depth;
            int chunkMaxX = chunkMinX + Chunk.Width - 1;
            int chunkMaxZ = chunkMinZ + Chunk.Depth - 1;

            int minCellX = CellIndex(chunkMinX - MaxSearchRadius, cellSize);
            int maxCellX = CellIndex(chunkMaxX + MaxSearchRadius, cellSize);
            int minCellZ = CellIndex(chunkMinZ - MaxSearchRadius, cellSize);
            int maxCellZ = CellIndex(chunkMaxZ + MaxSearchRadius, cellSize);

            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
                {
                    int anchorX = cellX * cellSize + cellSize / 2;
                    int anchorZ = cellZ * cellSize + cellSize / 2;

                    int hash = Hash(anchorX, anchorZ, salt);
                    if (hash % rarity != SpawnRemainder)
                    {
                        continue;
                    }

                    TerrainColumn anchorColumn = SampleColumn(anchorX, anchorZ, chunk, columns, previewColumn);
                    if (!IsValidAnchorColumn(anchorColumn))
                    {
                        continue;
                    }

                    var candidates = StructureRegistry.GetCandidates(anchorColumn.Biome.Primary, tier);
                    if (candidates.Count == 0)
                    {
                        continue;
                    }

                    var definition = candidates[hash % candidates.Count];
                    int flatRadius = definition.Template.FootprintRadius;
                    if (!StructureCoords.ChunkOverlapsFootprint(
                            chunkMinX, chunkMaxX, chunkMinZ, chunkMaxZ, anchorX, anchorZ, flatRadius))
                    {
                        continue;
                    }

                    if (!StructurePlacementKeys.IsAnchorFlat(
                            _seed,
                            anchorX,
                            anchorZ,
                            salt,
                            flatRadius,
                            maxFlatnessDelta,
                            () => IsFlat(anchorX, anchorZ, flatRadius, chunk, columns, previewColumn, maxFlatnessDelta)))
                    {
                        continue;
                    }

                    int variantSalt = StructurePlacementKeys.VariantSaltForStructure(
                        _seed, anchorX, anchorZ, definition.Id, hash);
                    var template = definition.ResolveTemplate(
                        _seed,
                        anchorX,
                        anchorZ,
                        variantSalt,
                        anchorColumn.Biome.Primary);

                    PlaceInChunk(chunk, anchorX, anchorZ, anchorColumn.SurfaceHeight, template, world, _seed);
                }
            }
        }

        private static TerrainColumn SampleColumn(
            int wx,
            int wz,
            Chunk chunk,
            TerrainColumn[,] columns,
            Func<int, int, TerrainColumn> previewColumn)
        {
            int lx = wx - chunk.ChunkX * Chunk.Width;
            int lz = wz - chunk.ChunkZ * Chunk.Depth;

            if (lx >= 0 && lx < Chunk.Width && lz >= 0 && lz < Chunk.Depth)
            {
                return columns[lx, lz];
            }

            return previewColumn(wx, wz);
        }

        private static bool IsValidAnchorColumn(TerrainColumn column)
        {
            if (column.Biome.Primary == BiomeType.Ocean || column.IsRiver || column.IsLake)
            {
                return false;
            }

            return true;
        }

        private static bool IsFlat(
            int anchorX,
            int anchorZ,
            int radius,
            Chunk chunk,
            TerrainColumn[,] columns,
            Func<int, int, TerrainColumn> previewColumn,
            int maxFlatnessDelta)
        {
            var center = SampleColumn(anchorX, anchorZ, chunk, columns, previewColumn);
            int baseHeight = center.SurfaceHeight;
            int step = radius > 24 ? 4 : radius > 12 ? 2 : 1;

            for (int dx = -radius; dx <= radius; dx += step)
            {
                for (int dz = -radius; dz <= radius; dz += step)
                {
                    var column = SampleColumn(anchorX + dx, anchorZ + dz, chunk, columns, previewColumn);
                    if (!IsValidAnchorColumn(column))
                    {
                        return false;
                    }

                    if (Math.Abs(column.SurfaceHeight - baseHeight) > maxFlatnessDelta)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void PlaceInChunk(
            Chunk chunk,
            int anchorX,
            int anchorZ,
            int surfaceHeight,
            StructureTemplate template,
            VoxelWorld? world,
            int worldSeed)
        {
            int chunkMinX = chunk.ChunkX * Chunk.Width;
            int chunkMinZ = chunk.ChunkZ * Chunk.Depth;

            IEnumerable<StructureBlock> blocks = template.ChunkIndex != null
                ? template.ChunkIndex.EnumerateForChunk(chunkMinX, chunkMinZ, anchorX, anchorZ)
                : template.Blocks;

            foreach (var block in blocks)
            {
                int wx = anchorX + block.Dx;
                int wz = anchorZ + block.Dz;
                int wy = surfaceHeight + block.Dy;

                int lx = wx - chunkMinX;
                int lz = wz - chunkMinZ;

                if (lx < 0 || lx >= Chunk.Width || lz < 0 || lz >= Chunk.Depth)
                {
                    continue;
                }

                if (wy <= 0 || wy >= Chunk.Height)
                {
                    continue;
                }

                BlockType current = chunk.GetBlock(lx, wy, lz);
                if (!CanReplace(current, block.Type, block.Mode))
                {
                    continue;
                }

                chunk.SetBlock(lx, wy, lz, block.Type);
            }

            if (world == null || template.Chests.Length == 0)
            {
                if (world == null)
                {
                    QueueChestRegistrations(chunk, anchorX, anchorZ, surfaceHeight, template, worldSeed);
                }

                return;
            }

            foreach (var chest in template.Chests)
            {
                int wx = anchorX + chest.Dx;
                int wy = surfaceHeight + chest.Dy;
                int wz = anchorZ + chest.Dz;
                int lx = wx - chunkMinX;
                int lz = wz - chunkMinZ;
                if (lx < 0 || lx >= Chunk.Width || lz < 0 || lz >= Chunk.Depth || wy <= 0 || wy >= Chunk.Height)
                {
                    continue;
                }

                int rollSeed = StructureContainerSystem.RollSeed(worldSeed, wx, wy, wz, chest.LootTableId);
                world.Containers.RegisterChest(wx, wy, wz, chest.LootTableId, rollSeed);
            }
        }

        private static void QueueChestRegistrations(
            Chunk chunk,
            int anchorX,
            int anchorZ,
            int surfaceHeight,
            StructureTemplate template,
            int worldSeed)
        {
            int chunkMinX = chunk.ChunkX * Chunk.Width;
            int chunkMinZ = chunk.ChunkZ * Chunk.Depth;

            foreach (var chest in template.Chests)
            {
                int wx = anchorX + chest.Dx;
                int wy = surfaceHeight + chest.Dy;
                int wz = anchorZ + chest.Dz;
                int lx = wx - chunkMinX;
                int lz = wz - chunkMinZ;

                if (lx < 0 || lx >= Chunk.Width || lz < 0 || lz >= Chunk.Depth || wy <= 0 || wy >= Chunk.Height)
                {
                    continue;
                }

                int rollSeed = StructureContainerSystem.RollSeed(worldSeed, wx, wy, wz, chest.LootTableId);
                chunk.PendingChests.Add(new PendingChestRegistration
                {
                    LocalX = lx,
                    LocalY = wy,
                    LocalZ = lz,
                    LootTableId = chest.LootTableId,
                    RollSeed = rollSeed
                });
            }
        }

        private static bool CanReplace(BlockType current, BlockType target, StructurePlacementMode mode)
        {
            if (mode == StructurePlacementMode.ReplaceAll)
            {
                return true;
            }

            if (target == BlockType.Air)
            {
                return current != BlockType.Air;
            }

            if (mode == StructurePlacementMode.AirOnly)
            {
                return current == BlockType.Air;
            }

            if (current == BlockType.Air)
            {
                return true;
            }

            if (current.IsTransparent())
            {
                return true;
            }

            return current is BlockType.Grass
                or BlockType.Dirt
                or BlockType.Sand
                or BlockType.Snow
                or BlockType.Gravel
                or BlockType.Mud;
        }

        private static int CellIndex(int worldCoord, int cellSize)
        {
            return (int)Math.Floor((worldCoord - cellSize / 2.0) / cellSize);
        }

        private int Hash(int wx, int wz, int salt)
        {
            unchecked
            {
                int h = wx * 92821 + wz * 68917 + _seed + salt;
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                return Math.Abs(h);
            }
        }

        private void PlaceGalleryGrid(Chunk chunk, VoxelWorld? world)
        {
            int chunkMinX = chunk.ChunkX * Chunk.Width;
            int chunkMinZ = chunk.ChunkZ * Chunk.Depth;

            foreach (var placement in StructureGallery.GetPlacements())
            {
                int radius = placement.FootprintRadius;
                int gridX = placement.AnchorX;
                int gridZ = placement.AnchorZ;

                if (gridX + radius >= chunkMinX && gridX - radius < chunkMinX + Chunk.Width &&
                    gridZ + radius >= chunkMinZ && gridZ - radius < chunkMinZ + Chunk.Depth)
                {
                    var definition = StructureRegistry.All[placement.Index];
                    int variantSalt = StructureGallery.VariantSaltFor(placement.Index);
                    var template = definition.ResolveTemplate(
                        StructureGallery.Seed,
                        gridX,
                        gridZ,
                        variantSalt,
                        BiomeType.Plains);
                    PlaceInChunk(chunk, gridX, gridZ, placement.SurfaceY, template, world, StructureGallery.Seed);
                }
            }
        }
    }
}

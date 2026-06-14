using System;

namespace Autonocraft.World.Structures
{
    public sealed class StructurePlacer
    {
        private const int SmallCellSize = 96;
        private const int MediumCellSize = 192;
        private const int SmallRarity = 900;
        private const int MediumRarity = 5000;
        private const int SpawnRemainder = 37;
        private const int MaxSearchRadius = 12;
        private const int MaxFlatnessDelta = 2;

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
            Func<int, int, TerrainColumn> previewColumn)
        {
            if (!_params.EnableStructures)
            {
                return;
            }

            PlaceTier(chunk, columns, previewColumn, StructureTier.Small, SmallCellSize, SmallRarity, 11);
            PlaceTier(chunk, columns, previewColumn, StructureTier.Medium, MediumCellSize, MediumRarity, 29);
        }

        private void PlaceTier(
            Chunk chunk,
            TerrainColumn[,] columns,
            Func<int, int, TerrainColumn> previewColumn,
            StructureTier tier,
            int cellSize,
            int baseRarity,
            int salt)
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
                    if (!IsFlat(anchorX, anchorZ, definition.Template, chunk, columns, previewColumn))
                    {
                        continue;
                    }

                    PlaceInChunk(chunk, anchorX, anchorZ, anchorColumn.SurfaceHeight, definition);
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
            StructureTemplate template,
            Chunk chunk,
            TerrainColumn[,] columns,
            Func<int, int, TerrainColumn> previewColumn)
        {
            var center = SampleColumn(anchorX, anchorZ, chunk, columns, previewColumn);
            int baseHeight = center.SurfaceHeight;
            int radius = template.FootprintRadius;

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    var column = SampleColumn(anchorX + dx, anchorZ + dz, chunk, columns, previewColumn);
                    if (!IsValidAnchorColumn(column))
                    {
                        return false;
                    }

                    if (Math.Abs(column.SurfaceHeight - baseHeight) > MaxFlatnessDelta)
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
            StructureDefinition definition)
        {
            int chunkMinX = chunk.ChunkX * Chunk.Width;
            int chunkMinZ = chunk.ChunkZ * Chunk.Depth;

            foreach (var block in definition.Template.Blocks)
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
                if (!CanReplace(current, block.Mode))
                {
                    continue;
                }

                chunk.SetBlock(lx, wy, lz, block.Type);
            }
        }

        private static bool CanReplace(BlockType current, StructurePlacementMode mode)
        {
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
    }
}

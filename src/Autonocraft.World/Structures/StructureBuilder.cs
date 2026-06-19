using System;
using System.Collections.Generic;
using System.Linq;
using Autonocraft.Domain.World;

namespace Autonocraft.World.Structures
{
    public sealed class StructureBuilder
    {
        private readonly Dictionary<(int dx, int dy, int dz), StructureBlock> _blocks = new Dictionary<(int, int, int), StructureBlock>();
        private readonly List<StructureChestMarker> _chests = new List<StructureChestMarker>();

        public StructureBuilder Add(int dx, int dy, int dz, BlockType type, StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            _blocks[(dx, dy, dz)] = new StructureBlock(dx, dy, dz, type, mode);
            return this;
        }

        public StructureBuilder Fill(int minX, int minY, int minZ, int maxX, int maxY, int maxZ, BlockType type, StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        Add(x, y, z, type, mode);
                    }
                }
            }
            return this;
        }

        public StructureBuilder FillHollow(int minX, int minY, int minZ, int maxX, int maxY, int maxZ, BlockType type, BlockType innerType = BlockType.Air, StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        bool isBorder = x == minX || x == maxX || y == minY || y == maxY || z == minZ || z == maxZ;
                        Add(x, y, z, isBorder ? type : innerType, mode);
                    }
                }
            }
            return this;
        }

        public StructureBuilder Pillar(int dx, int dz, int minY, int maxY, BlockType type, StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Add(dx, y, dz, type, mode);
            }
            return this;
        }

        public StructureBuilder WallX(int x, int minY, int maxY, int minZ, int maxZ, BlockType type, StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Add(x, y, z, type, mode);
                }
            }
            return this;
        }

        public StructureBuilder WallZ(int z, int minY, int maxY, int minX, int maxX, BlockType type, StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Add(x, y, z, type, mode);
                }
            }
            return this;
        }

        public StructureBuilder Dome(int centerX, int centerZ, int startY, int radius, BlockType type, StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            for (int dy = 0; dy <= radius; dy++)
            {
                int y = startY + dy;
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        if (dist <= radius && dist >= radius - 1.2)
                        {
                            Add(centerX + dx, y, centerZ + dz, type, mode);
                        }
                    }
                }
            }
            return this;
        }

        public StructureBuilder Crenellations(int minX, int y, int minZ, int maxX, int maxZ, BlockType type, StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (((x + z) & 1) == 0)
                    {
                        Add(x, y, z, type, mode);
                    }
                }
            }

            return this;
        }

        public StructureBuilder ArchZ(int z, int minY, int maxY, int minX, int maxX, BlockType type, StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            int midX = (minX + maxX) / 2;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (x == minX || x == maxX || y == maxY)
                    {
                        Add(x, y, z, type, mode);
                    }
                    else if (x == midX && y <= maxY - 2)
                    {
                        Add(x, y, z, BlockType.Air, mode);
                    }
                }
            }

            return this;
        }

        public StructureBuilder GabledRoof(
            int minX,
            int minZ,
            int maxX,
            int maxZ,
            int baseY,
            int peakHeight,
            BlockType roof,
            BlockType ridge,
            bool ridgeAlongX = true,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            int width = ridgeAlongX ? maxZ - minZ : maxX - minX;
            int layers = Math.Max(1, peakHeight);
            for (int layer = 0; layer < layers; layer++)
            {
                int inset = layer;
                int y = baseY + layer;
                if (ridgeAlongX)
                {
                    int z0 = minZ + inset;
                    int z1 = maxZ - inset;
                    if (z0 > z1)
                    {
                        break;
                    }

                    for (int x = minX; x <= maxX; x++)
                    {
                        Add(x, y, z0, roof, mode);
                        Add(x, y, z1, roof, mode);
                    }

                    if (layer == layers - 1)
                    {
                        for (int x = minX; x <= maxX; x++)
                        {
                            Add(x, y + 1, (minZ + maxZ) / 2, ridge, mode);
                        }
                    }
                }
                else
                {
                    int x0 = minX + inset;
                    int x1 = maxX - inset;
                    if (x0 > x1)
                    {
                        break;
                    }

                    for (int z = minZ; z <= maxZ; z++)
                    {
                        Add(x0, y, z, roof, mode);
                        Add(x1, y, z, roof, mode);
                    }

                    if (layer == layers - 1)
                    {
                        for (int z = minZ; z <= maxZ; z++)
                        {
                            Add((minX + maxX) / 2, y + 1, z, ridge, mode);
                        }
                    }
                }
            }

            return this;
        }

        public StructureBuilder PyramidRoof(
            int centerX,
            int centerZ,
            int halfSpan,
            int baseY,
            int height,
            BlockType roof,
            BlockType cap,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            for (int layer = 0; layer < height; layer++)
            {
                int span = Math.Max(0, halfSpan - layer);
                int y = baseY + layer;
                for (int x = -span; x <= span; x++)
                {
                    for (int z = -span; z <= span; z++)
                    {
                        bool edge = x == -span || x == span || z == -span || z == span;
                        if (edge || layer == height - 1)
                        {
                            Add(centerX + x, y, centerZ + z, roof, mode);
                        }
                    }
                }
            }

            Add(centerX, baseY + height, centerZ, cap, mode);
            return this;
        }

        public StructureBuilder HipRoof(
            int minX,
            int minZ,
            int maxX,
            int maxZ,
            int baseY,
            int height,
            BlockType roof,
            BlockType cap,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            int cx = (minX + maxX) / 2;
            int cz = (minZ + maxZ) / 2;
            int halfX = (maxX - minX) / 2;
            int halfZ = (maxZ - minZ) / 2;
            return PyramidRoof(cx, cz, Math.Max(halfX, halfZ), baseY, height, roof, cap, mode);
        }

        public StructureBuilder Staircase(
            int startX,
            int startY,
            int startZ,
            int steps,
            bool alongZ,
            BlockType step = BlockType.StoneSlab,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            for (int i = 0; i < steps; i++)
            {
                int y = startY + i;
                if (alongZ)
                {
                    Add(startX, y, startZ + i, step, mode);
                    Add(startX + 1, y, startZ + i, step, mode);
                }
                else
                {
                    Add(startX + i, y, startZ, step, mode);
                    Add(startX + i, y, startZ + 1, step, mode);
                }
            }

            return this;
        }

        public StructureBuilder SpiralStair(
            int centerX,
            int centerZ,
            int startY,
            int steps,
            int radius,
            BlockType step = BlockType.StoneSlab,
            BlockType core = BlockType.OakLog,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            Pillar(centerX, centerZ, startY, startY + steps, core, mode);
            for (int i = 0; i < steps; i++)
            {
                double angle = i * Math.PI / 2;
                int dx = (int)Math.Round(Math.Cos(angle) * radius);
                int dz = (int)Math.Round(Math.Sin(angle) * radius);
                Add(centerX + dx, startY + i, centerZ + dz, step, mode);
            }

            return this;
        }

        public StructureBuilder PointedArchZ(
            int z,
            int minY,
            int maxY,
            int minX,
            int maxX,
            BlockType type,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            int midX = (minX + maxX) / 2;
            int height = maxY - minY;
            for (int y = minY; y <= maxY; y++)
            {
                int dy = y - minY;
                int taper = Math.Max(0, (height - dy) / 3);
                for (int x = minX; x <= maxX; x++)
                {
                    bool pillar = x == minX || x == maxX;
                    bool keystone = x == midX && y >= maxY - 1;
                    bool inner = x > minX + taper && x < maxX - taper && y < maxY - 1;
                    if (pillar || keystone)
                    {
                        Add(x, y, z, type, mode);
                    }
                    else if (inner && x == midX)
                    {
                        Add(x, y, z, BlockType.Air, mode);
                    }
                }
            }

            return this;
        }

        public StructureBuilder PointedArchX(
            int x,
            int minY,
            int maxY,
            int minZ,
            int maxZ,
            BlockType type,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            int midZ = (minZ + maxZ) / 2;
            int height = maxY - minY;
            for (int y = minY; y <= maxY; y++)
            {
                int dy = y - minY;
                int taper = Math.Max(0, (height - dy) / 3);
                for (int z = minZ; z <= maxZ; z++)
                {
                    bool pillar = z == minZ || z == maxZ;
                    bool keystone = z == midZ && y >= maxY - 1;
                    bool inner = z > minZ + taper && z < maxZ - taper && y < maxY - 1;
                    if (pillar || keystone)
                    {
                        Add(x, y, z, type, mode);
                    }
                    else if (inner && z == midZ)
                    {
                        Add(x, y, z, BlockType.Air, mode);
                    }
                }
            }

            return this;
        }

        public StructureBuilder Battlements(
            int minX,
            int y,
            int minZ,
            int maxX,
            int maxZ,
            BlockType type,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    int idx = (x - minX) + (z - minZ);
                    if (idx % 3 != 2)
                    {
                        Add(x, y, z, type, mode);
                        if (idx % 3 == 0)
                        {
                            Add(x, y + 1, z, type, mode);
                        }
                    }
                }
            }

            return this;
        }

        public StructureBuilder ArrowSlit(
            int x,
            int y,
            int z,
            int height,
            BlockType frame,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            for (int dy = 0; dy < height; dy++)
            {
                if (dy == height / 2)
                {
                    Add(x, y + dy, z, BlockType.Air, mode);
                }
                else
                {
                    Add(x, y + dy, z, frame, mode);
                }
            }

            return this;
        }

        public StructureBuilder Buttress(
            int wallX,
            int wallZ,
            int baseY,
            int height,
            bool alongX,
            BlockType type,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            for (int step = 0; step < height; step++)
            {
                int y = baseY + step;
                int depth = height - step;
                if (alongX)
                {
                    int dir = wallX >= 0 ? 1 : -1;
                    for (int d = 0; d < depth; d++)
                    {
                        Add(wallX + dir * d, y, wallZ, type, mode);
                    }
                }
                else
                {
                    int dir = wallZ >= 0 ? 1 : -1;
                    for (int d = 0; d < depth; d++)
                    {
                        Add(wallX, y, wallZ + dir * d, type, mode);
                    }
                }
            }

            return this;
        }

        public StructureBuilder LancetWindow(
            int x,
            int y,
            int z,
            int height,
            BlockType frame,
            BlockType glass,
            BlockType? glowBelow = null,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            for (int dy = 0; dy < height; dy++)
            {
                bool edge = dy == 0 || dy == height - 1;
                Add(x, y + dy, z, edge ? frame : glass, mode);
            }

            if (glowBelow.HasValue)
            {
                Add(x, y - 1, z, glowBelow.Value, mode);
            }

            return this;
        }

        public StructureBuilder HalfTimberFace(
            int faceX,
            int faceZ,
            int minY,
            int maxY,
            int minAlong,
            int maxAlong,
            bool alongZ,
            BlockType plank,
            BlockType timber,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            for (int y = minY; y <= maxY; y++)
            {
                bool band = (y - minY) % 2 == 0;
                for (int i = minAlong; i <= maxAlong; i++)
                {
                    BlockType mat = band ? timber : plank;
                    if (alongZ)
                    {
                        Add(faceX, y, i, mat, mode);
                    }
                    else
                    {
                        Add(i, y, faceZ, mat, mode);
                    }
                }
            }

            return this;
        }

        public StructureBuilder Chimney(
            int x,
            int z,
            int baseY,
            int height,
            BlockType body,
            BlockType cap,
            bool withLantern = false,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            Pillar(x, z, baseY, baseY + height - 1, body, mode);
            Add(x, baseY + height, z, cap, mode);
            if (withLantern)
            {
                Add(x, baseY + height + 1, z, BlockType.Lantern, mode);
            }

            return this;
        }

        public StructureBuilder RuinOverlay(
            StructureRng rng,
            int minX,
            int minY,
            int minZ,
            int maxX,
            int maxY,
            int maxZ,
            BlockType ruinBlock,
            float intensity,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        if (rng.Chance(intensity))
                        {
                            Add(x, y, z, ruinBlock, mode);
                        }
                    }
                }
            }

            return this;
        }

        public StructureBuilder Chest(
            int dx,
            int dy,
            int dz,
            string lootTableId,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            _chests.Add(new StructureChestMarker
            {
                Dx = dx,
                Dy = dy,
                Dz = dz,
                LootTableId = lootTableId
            });
            return Add(dx, dy, dz, BlockType.Chest, mode)
                .Add(dx, dy + 1, dz, BlockType.Air, mode);
        }

        public StructureTemplate Build(int footprintRadius)
        {
            foreach (var chest in _chests)
            {
                Add(chest.Dx, chest.Dy + 1, chest.Dz, BlockType.Air, StructurePlacementMode.ReplaceAll);
            }

            var blocks = _blocks.Values.ToArray();
            int maxExtent = 0;
            foreach (var block in blocks)
            {
                if (block.Type == BlockType.Air)
                {
                    continue;
                }

                maxExtent = Math.Max(maxExtent, Math.Max(Math.Abs(block.Dx), Math.Abs(block.Dz)));
            }

            return new StructureTemplate
            {
                FootprintRadius = Math.Max(footprintRadius, maxExtent),
                Blocks = blocks,
                Chests = _chests.ToArray(),
                ChunkIndex = blocks.Length > 0 ? StructureChunkIndex.Build(blocks) : null
            };
        }
    }
}

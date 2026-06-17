using Autonocraft.Domain.World;

namespace Autonocraft.World.Structures
{
    internal static class StructurePaths
    {
        public static void ApproachPath(
            StructureBuilder b,
            int fromZ,
            int length,
            int halfWidth,
            BlockType pathBlock)
        {
            for (int z = fromZ - length; z < fromZ; z++)
            {
                for (int x = -halfWidth; x <= halfWidth; x++)
                {
                    b.Add(x, 0, z, pathBlock, StructurePlacementMode.ReplaceSurface);
                }
            }
        }

        public static void DoorwaySouth(
            StructureBuilder b,
            int z,
            int minX,
            int maxX,
            int minY,
            int maxY,
            in BiomeStructurePalette palette)
        {
            int midX = (minX + maxX) / 2;
            for (int y = minY; y <= maxY; y++)
            {
                b.Add(midX, y, z, BlockType.Air);
                if (maxX > minX)
                {
                    b.Add(midX - 1, y, z, BlockType.Air);
                }
            }

            b.ArchZ(z, minY, maxY, minX - 1, maxX + 1, palette.Trim);
        }

        public static void CorridorZ(
            StructureBuilder b,
            int x,
            int z0,
            int z1,
            int y,
            int height,
            in BiomeStructurePalette palette)
        {
            int minZ = Math.Min(z0, z1);
            int maxZ = Math.Max(z0, z1);
            b.Fill(x - 1, y, minZ, x + 1, y, maxZ, palette.Floor, StructurePlacementMode.ReplaceAll);
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int dy = 1; dy <= height; dy++)
                {
                    b.Add(x - 1, y + dy, z, BlockType.Air, StructurePlacementMode.ReplaceAll);
                    b.Add(x, y + dy, z, BlockType.Air, StructurePlacementMode.ReplaceAll);
                    b.Add(x + 1, y + dy, z, BlockType.Air, StructurePlacementMode.ReplaceAll);
                }

                b.Add(x - 1, y + height + 1, z, palette.Roof, StructurePlacementMode.ReplaceAll);
                b.Add(x, y + height + 1, z, palette.Roof, StructurePlacementMode.ReplaceAll);
                b.Add(x + 1, y + height + 1, z, palette.Roof, StructurePlacementMode.ReplaceAll);
            }

            b.Add(x, y + 1, minZ, BlockType.Lantern, StructurePlacementMode.ReplaceAll);
            b.Add(x, y + 1, maxZ, BlockType.Lantern, StructurePlacementMode.ReplaceAll);
        }

        public static void CorridorX(
            StructureBuilder b,
            int z,
            int x0,
            int x1,
            int y,
            int height,
            in BiomeStructurePalette palette)
        {
            int minX = Math.Min(x0, x1);
            int maxX = Math.Max(x0, x1);
            b.Fill(minX, y, z - 1, maxX, y, z + 1, palette.Floor, StructurePlacementMode.ReplaceAll);
            for (int x = minX; x <= maxX; x++)
            {
                for (int dy = 1; dy <= height; dy++)
                {
                    b.Add(x, y + dy, z - 1, BlockType.Air, StructurePlacementMode.ReplaceAll);
                    b.Add(x, y + dy, z, BlockType.Air, StructurePlacementMode.ReplaceAll);
                    b.Add(x, y + dy, z + 1, BlockType.Air, StructurePlacementMode.ReplaceAll);
                }

                b.Add(x, y + height + 1, z - 1, palette.Roof, StructurePlacementMode.ReplaceAll);
                b.Add(x, y + height + 1, z, palette.Roof, StructurePlacementMode.ReplaceAll);
                b.Add(x, y + height + 1, z + 1, palette.Roof, StructurePlacementMode.ReplaceAll);
            }
        }
    }
}

using Autonocraft.Domain.World;
using Autonocraft.World.Loot;

namespace Autonocraft.World.Structures
{
    internal static class MegaStructureKit
    {
        public static void MoatRing(StructureBuilder b, int half, int width, BlockType water = BlockType.Water)
        {
            int outer = half + width;
            for (int x = -outer; x <= outer; x++)
            {
                for (int z = -outer; z <= outer; z++)
                {
                    bool inOuter = x >= -outer && x <= outer && z >= -outer && z <= outer;
                    bool inInner = x >= -half && x <= half && z >= -half && z <= half;
                    if (inOuter && !inInner)
                    {
                        b.Add(x, -1, z, water, StructurePlacementMode.ReplaceAll);
                    }
                }
            }
        }

        public static void StoneBridge(StructureBuilder b, int z, int halfWidth, int length, BlockType deck)
        {
            for (int dz = z; dz < z + length; dz++)
            {
                for (int x = -halfWidth; x <= halfWidth; x++)
                {
                    b.Add(x, 0, dz, deck, StructurePlacementMode.ReplaceAll);
                    b.Add(x, -1, dz, deck, StructurePlacementMode.ReplaceAll);
                }
            }
        }

        public static void CourtyardCross(
            StructureBuilder b,
            int half,
            int y,
            int armWidth,
            in BiomeStructurePalette palette)
        {
            b.Fill(-armWidth, y, -half + 2, armWidth, y, half - 2, palette.Path, StructurePlacementMode.ReplaceAll);
            b.Fill(-half + 2, y, -armWidth, half - 2, y, armWidth, palette.Path, StructurePlacementMode.ReplaceAll);
            for (int i = -half + 4; i <= half - 4; i += 8)
            {
                b.Add(i, y + 1, 0, BlockType.Lantern, StructurePlacementMode.ReplaceAll);
                b.Add(0, y + 1, i, BlockType.Lantern, StructurePlacementMode.ReplaceAll);
            }
        }

        public static void WindowBand(
            StructureBuilder b,
            int y,
            int minX,
            int maxX,
            int z,
            BlockType frame,
            BlockType glass)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (((x + z) & 3) != 0)
                {
                    continue;
                }

                b.Add(x, y, z, glass, StructurePlacementMode.ReplaceAll);
                b.Add(x, y + 1, z, frame, StructurePlacementMode.ReplaceAll);
            }
        }

        public static void BandedWallRing(
            StructureBuilder b,
            int half,
            int minY,
            int maxY,
            BlockType primary,
            BlockType band,
            int bandEvery = 4)
        {
            for (int y = minY; y <= maxY; y++)
            {
                BlockType mat = ((y - minY) % bandEvery) == bandEvery - 1 ? band : primary;
                b.WallX(-half, y, y, -half, half, mat, StructurePlacementMode.ReplaceAll);
                b.WallX(half, y, y, -half, half, mat, StructurePlacementMode.ReplaceAll);
                b.WallZ(-half, y, y, -half + 1, half - 1, mat, StructurePlacementMode.ReplaceAll);
                b.WallZ(half, y, y, -half + 1, half - 1, mat, StructurePlacementMode.ReplaceAll);
            }
        }

        public static void GatehouseSouth(
            StructureBuilder b,
            int z,
            int halfWidth,
            int height,
            int depth,
            in BiomeStructurePalette palette)
        {
            b.FillHollow(-halfWidth, 1, z, halfWidth, height, z + depth, palette.WallAccent, BlockType.Air, StructurePlacementMode.ReplaceAll);
            int gateW = Math.Max(3, halfWidth / 2);
            for (int y = 1; y <= height; y++)
            {
                for (int x = -gateW / 2; x <= gateW / 2; x++)
                {
                    b.Add(x, y, z, BlockType.Air, StructurePlacementMode.ReplaceAll);
                }
            }

            b.PointedArchZ(z, 1, height - 1, -gateW / 2 - 1, gateW / 2 + 1, palette.Trim, StructurePlacementMode.ReplaceAll);
            b.PointedArchZ(z + depth, 1, height - 2, -gateW / 2, gateW / 2, palette.Trim, StructurePlacementMode.ReplaceAll);
            b.Add(0, height, z + depth / 2, palette.GlowAccent, StructurePlacementMode.ReplaceAll);
            b.Pillar(-halfWidth, z + depth, 1, height, palette.Pillar, StructurePlacementMode.ReplaceAll);
            b.Pillar(halfWidth, z + depth, 1, height, palette.Pillar, StructurePlacementMode.ReplaceAll);
            b.ArrowSlit(-halfWidth, 2, z + depth / 2, 3, palette.Trim, StructurePlacementMode.ReplaceAll);
            b.ArrowSlit(halfWidth, 2, z + depth / 2, 3, palette.Trim, StructurePlacementMode.ReplaceAll);
            b.Battlements(-halfWidth, height + 1, z, halfWidth, z + depth, palette.Trim, StructurePlacementMode.ReplaceAll);
        }

        public static void DungeonCell(
            StructureBuilder b,
            int ox,
            int oy,
            int oz,
            int size,
            in BiomeStructurePalette palette,
            StructureRng rng)
        {
            b.FillHollow(ox, oy, oz, ox + size, oy + 3, oz + size, palette.Wall, BlockType.Air, StructurePlacementMode.ReplaceAll)
                .Fill(ox, oy, oz, ox + size, oy, oz + size, palette.Foundation, StructurePlacementMode.ReplaceAll);
            b.Add(ox + size / 2, oy + 1, oz, BlockType.IronBlock, StructurePlacementMode.ReplaceAll);
            b.Add(ox, oy + 2, oz + size / 2, palette.GlowAccent, StructurePlacementMode.ReplaceAll);
            if (rng.Chance(0.35f))
            {
                b.Chest(ox + 1, oy + 1, oz + size - 1, LootTableIds.Dungeon);
            }
        }

        public static void Fountain(StructureBuilder b, int y, BlockType rim, BlockType core)
        {
            b.Fill(-2, y, -2, 2, y, 2, rim, StructurePlacementMode.ReplaceAll);
            b.Add(0, y + 1, 0, BlockType.Water, StructurePlacementMode.ReplaceAll);
            b.Add(0, y + 2, 0, core, StructurePlacementMode.ReplaceAll);
        }
    }
}

using Autonocraft.Domain.World;

namespace Autonocraft.World.Structures
{
    internal static class MedievalDetailKit
    {
        public static void StampFantasyLighting(
            StructureBuilder b,
            (int x, int y, int z)[] positions,
            in BiomeStructurePalette palette,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            foreach (var (x, y, z) in positions)
            {
                b.Add(x, y, z, palette.GlowAccent, mode);
            }
        }

        public static void StampRuinWear(
            StructureBuilder b,
            StructureRng rng,
            int minX,
            int minY,
            int minZ,
            int maxX,
            int maxY,
            int maxZ,
            in BiomeStructurePalette palette,
            float intensity,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            b.RuinOverlay(rng, minX, minY, minZ, maxX, maxY, maxZ, palette.Ruin, intensity, mode);
        }

        public static void HalfTimberWalls(
            StructureBuilder b,
            int minX,
            int maxX,
            int minY,
            int maxY,
            int minZ,
            int maxZ,
            in BiomeStructurePalette palette,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            b.HalfTimberFace(minX, 0, minY, maxY, minZ, maxZ, alongZ: true, palette.Wall, palette.WallAccent, mode);
            b.HalfTimberFace(maxX, 0, minY, maxY, minZ, maxZ, alongZ: true, palette.Wall, palette.WallAccent, mode);
            b.HalfTimberFace(0, minZ, minY, maxY, minX, maxX, alongZ: false, palette.Wall, palette.WallAccent, mode);
            b.HalfTimberFace(0, maxZ, minY, maxY, minX, maxX, alongZ: false, palette.Wall, palette.WallAccent, mode);
        }

        public static void WallButtresses(
            StructureBuilder b,
            int half,
            int baseY,
            int height,
            int spacing,
            in BiomeStructurePalette palette,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            for (int x = -half + spacing; x < half; x += spacing)
            {
                b.Buttress(-half, x, baseY, height, alongX: true, palette.WallAccent, mode);
                b.Buttress(half, x, baseY, height, alongX: true, palette.WallAccent, mode);
            }

            for (int z = -half + spacing; z < half; z += spacing)
            {
                b.Buttress(z, -half, baseY, height, alongX: false, palette.WallAccent, mode);
                b.Buttress(z, half, baseY, height, alongX: false, palette.WallAccent, mode);
            }
        }

        public static void GatehouseFantasy(
            StructureBuilder b,
            int z,
            int gateW,
            int wallH,
            in BiomeStructurePalette palette,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            b.PointedArchZ(z, 1, wallH - 1, -gateW / 2 - 1, gateW / 2 + 1, palette.Trim, mode);
            b.ArrowSlit(-gateW / 2 - 2, 2, z, 3, palette.Trim, mode);
            b.ArrowSlit(gateW / 2 + 2, 2, z, 3, palette.Trim, mode);
            b.Add(0, wallH, z, palette.GlowAccent, mode);
        }

        public static void CourtyardLanterns(
            StructureBuilder b,
            int half,
            int y,
            in BiomeStructurePalette palette,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            int inset = half / 3;
            StampFantasyLighting(b,
            [
                (-inset, y, -inset),
                (inset, y, -inset),
                (-inset, y, inset),
                (inset, y, inset)
            ],
            palette,
            mode);
        }
    }
}

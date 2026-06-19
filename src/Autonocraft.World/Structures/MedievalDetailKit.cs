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
            if (spacing <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(spacing), "spacing must be > 0.");
            }

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

        public static void PorchSouth(
            StructureBuilder b,
            int halfWidth,
            int z,
            int y,
            int depth,
            in BiomeStructurePalette palette,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            b.Fill(-halfWidth, y, z - depth, halfWidth, y, z, palette.Floor, mode)
                .Pillar(-halfWidth, z - depth, y + 1, y + 3, palette.Pillar, mode)
                .Pillar(halfWidth, z - depth, y + 1, y + 3, palette.Pillar, mode)
                .Fill(-halfWidth, y + 4, z - depth, halfWidth, y + 4, z, palette.Roof, mode)
                .Add(-halfWidth, y + 2, z - depth, palette.GlowAccent, mode)
                .Add(halfWidth, y + 2, z - depth, palette.GlowAccent, mode);
        }

        public static void FacadeDepthSouth(
            StructureBuilder b,
            int z,
            int minX,
            int maxX,
            int minY,
            int maxY,
            in BiomeStructurePalette palette,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            b.PointedArchZ(z, minY, maxY, -1, 1, palette.Trim, mode);
            for (int x = minX; x <= maxX; x++)
            {
                if (x is >= -1 and <= 1)
                {
                    continue;
                }

                if ((x - minX) % 2 == 0)
                {
                    b.Pillar(x, z, minY, maxY, palette.WallAccent, mode);
                }
            }
        }

        public static void RoofDormerSouth(
            StructureBuilder b,
            int x,
            int z,
            int baseY,
            in BiomeStructurePalette palette,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            b.Fill(x - 1, baseY, z, x + 1, baseY + 1, z, palette.WallAccent, mode)
                .Add(x, baseY, z, palette.Window, mode)
                .GabledRoof(x - 1, z, x + 1, z + 2, baseY + 2, 2, palette.Roof, palette.Trim, ridgeAlongX: true, mode);
        }

        public static void LandmarkSpire(
            StructureBuilder b,
            int x,
            int z,
            int baseY,
            in BiomeStructurePalette palette,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            b.Pillar(x, z, baseY, baseY + 2, palette.Trim, mode)
                .Add(x, baseY + 3, z, palette.Accent, mode)
                .Add(x, baseY + 4, z, palette.GlowAccent, mode);
        }

        public static void ExteriorPlanters(
            StructureBuilder b,
            int z,
            int y,
            int halfWidth,
            StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            b.Add(-halfWidth, y, z, BlockType.MossCarpet, mode)
                .Add(-halfWidth, y + 1, z, BlockType.Flower, mode)
                .Add(halfWidth, y, z, BlockType.MossCarpet, mode)
                .Add(halfWidth, y + 1, z, BlockType.Poppy, mode);
        }
    }
}

using Autonocraft.Domain.World;
using Autonocraft.World.Loot;

namespace Autonocraft.World.Structures
{
    internal static class MegaCitadelGenerator
    {
        public static StructureTemplate Generate(in StructureGenContext ctx)
        {
            var rng = ctx.Rng;
            var p = BiomeStructurePalette.ForMega(ctx.Biome);
            var b = new StructureBuilder();

            int half = rng.Range(28, 34);
            int depth = rng.Range(10, 14);
            int midDepth = depth / 2;
            bool volcanic = ctx.Biome == BiomeType.Volcanic;

            StructurePaths.ApproachPath(b, -half, rng.Range(12, 16), 3, p.Path);

            b.Fill(-half, -depth, -half, half, -depth, half, p.Wall, StructurePlacementMode.ReplaceAll)
                .FillHollow(-half, -depth + 1, -half, half, 2, half, p.Wall, BlockType.Air, StructurePlacementMode.ReplaceAll);

            for (int step = 0; step < depth + 2; step++)
            {
                b.Add(0, -step, -half - step - 1, p.Floor, StructurePlacementMode.ReplaceAll);
                b.Add(0, -step - 1, -half - step - 1, BlockType.Air, StructurePlacementMode.ReplaceAll);
                b.Pillar(-1, -half - step - 1, -step, -step + 2, p.Pillar, StructurePlacementMode.ReplaceAll);
                b.Pillar(1, -half - step - 1, -step, -step + 2, p.Pillar, StructurePlacementMode.ReplaceAll);
                b.PointedArchZ(-half - step - 1, -step, 0, -1, 1, p.Trim, StructurePlacementMode.ReplaceAll);
            }

            int hallCount = rng.Range(12, 18);
            for (int i = 0; i < hallCount; i++)
            {
                int angle = rng.NextInt(4);
                int offset = rng.Range(-half + 4, half - 8);
                switch (angle)
                {
                    case 0:
                        CarveHall(b, -half + 1, -depth + 1, offset, 4, rng.Range(8, 14));
                        break;
                    case 1:
                        CarveHall(b, half - 5, -depth + 1, offset, 4, rng.Range(8, 14));
                        break;
                    case 2:
                        CarveHall(b, offset, -depth + 1, -half + 1, rng.Range(8, 14), 4);
                        break;
                    default:
                        CarveHall(b, offset, -depth + 1, half - 5, rng.Range(8, 14), 4);
                        break;
                }
            }

            b.Fill(-half + 2, -midDepth, -half + 2, half - 2, -midDepth, half - 2, p.Foundation, StructurePlacementMode.ReplaceAll)
                .FillHollow(-half + 3, -midDepth + 1, -half + 3, half - 3, -2, half - 3, p.WallAccent, BlockType.Air, StructurePlacementMode.ReplaceAll);

            for (int i = 0; i < 10; i++)
            {
                MegaStructureKit.DungeonCell(
                    b,
                    rng.Range(-half + 4, half - 7),
                    -midDepth + 1,
                    rng.Range(-half + 4, half - 7),
                    rng.Pick(3, 4),
                    p,
                    rng);
            }

            b.Add(0, -depth + 1, 0, BlockType.Obsidian, StructurePlacementMode.ReplaceAll)
                .Add(0, -depth + 2, 0, BlockType.GoldBlock, StructurePlacementMode.ReplaceAll)
                .Add(0, -depth + 3, 0, p.GlowAccent, StructurePlacementMode.ReplaceAll)
                .Chest(1, -depth + 1, 0, LootTableIds.Citadel)
                .Chest(-1, -depth + 1, 1, LootTableIds.Dungeon)
                .Chest(2, -depth + 1, -1, LootTableIds.Treasury);

            if (volcanic)
            {
                b.Fill(-4, -depth + 1, -4, 4, -depth + 1, 4, BlockType.MagmaBlock, StructurePlacementMode.ReplaceAll);
                b.Fill(-2, -depth + 1, -2, 2, -depth + 1, 2, BlockType.Lava, StructurePlacementMode.ReplaceAll);
            }

            for (int i = 0; i < 6; i++)
            {
                int px = rng.Pick(-half + 3, half - 3);
                int pz = rng.Pick(-half + 3, half - 3);
                b.Add(px, -depth + 1, pz, rng.Chance(0.5f) ? BlockType.Amethyst : BlockType.RubyBlock, StructurePlacementMode.ReplaceAll);
            }

            int ruinH = rng.Range(5, 9);
            int naveW = rng.Range(10, 14);
            int naveD = rng.Range(16, 22);
            b.FillHollow(-naveW, 0, -half, naveW, ruinH, -half + naveD, p.Wall, BlockType.Air)
                .Fill(-naveW + 1, 0, -half + 1, naveW - 1, 0, -half + naveD - 1, p.Floor);

            for (int z = -half + 2; z < -half + naveD - 2; z += 5)
            {
                b.Pillar(-naveW + 2, z, 0, ruinH + 1, p.Pillar);
                b.Pillar(naveW - 2, z, 0, ruinH + rng.Pick(0, 2), p.Pillar);
                b.Buttress(-naveW, z, 1, ruinH / 2, alongX: true, p.WallAccent);
                b.Buttress(naveW, z, 1, ruinH / 2, alongX: true, p.WallAccent);
            }

            for (int z = -half + 3; z < -half + naveD - 3; z += 4)
            {
                b.LancetWindow(-naveW + 1, 2, z, 3, p.Trim, p.Window, p.GlowAccent);
                b.LancetWindow(naveW - 1, 2, z, 3, p.Trim, p.Window, p.GlowAccent);
            }

            MegaStructureKit.WindowBand(b, 3, -naveW + 2, naveW - 2, -half + 1, p.Trim, p.Window);
            MegaStructureKit.WindowBand(b, 3, -naveW + 2, naveW - 2, -half + naveD - 1, p.Trim, p.Window);

            b.Add(0, 1, -half, BlockType.Air)
                .Add(0, 2, -half, BlockType.Air)
                .Add(0, 3, -half, BlockType.Air)
                .PointedArchZ(-half, 1, ruinH - 1, -2, 2, p.Trim)
                .Battlements(-half, ruinH + 1, -half, half, half, p.Trim);

            b.FillHollow(-half, 0, -half, half, ruinH, half, p.WallAccent, BlockType.Air)
                .Dome(0, 0, ruinH + 1, rng.Pick(4, 5), p.Roof);

            b.Pillar(-half + 3, -half + 3, 0, ruinH + 3, p.Pillar)
                .Pillar(half - 3, -half + 3, 0, ruinH + 2, p.Pillar)
                .Pillar(-half + 3, half - 3, 0, ruinH + 1, p.Pillar)
                .Pillar(half - 3, half - 3, 0, ruinH + 4, p.Pillar);

            MedievalDetailKit.StampRuinWear(b, rng, -half, 0, -half, half, ruinH, half, p, 0.2f);

            b.Add(-half + 2, 1, 0, BlockType.StationCrucible)
                .Add(half - 2, 1, 0, BlockType.StationBench)
                .Add(0, 1, half - 2, BlockType.StationForge)
                .Chest(0, 1, 0, LootTableIds.Citadel)
                .Chest(-3, 1, 2, LootTableIds.Medium)
                .Chest(3, 1, -2, LootTableIds.Treasury);

            StructurePaths.CorridorX(b, 0, -half + 4, half - 4, 1, 3, p);
            MedievalDetailKit.CourtyardLanterns(b, half / 2, 2, p);

            return b.Build(half + 4);
        }

        private static void CarveHall(StructureBuilder b, int x, int y, int z, int w, int d)
        {
            b.Fill(x, y, z, x + w, y, z + d, BlockType.Air, StructurePlacementMode.ReplaceAll);
            b.Fill(x, y + 4, z, x + w, y + 4, z + d, BlockType.Cobblestone, StructurePlacementMode.ReplaceAll);
            b.Add(x + w / 2, y + 1, z + d / 2, BlockType.Lantern, StructurePlacementMode.ReplaceAll);
        }
    }
}

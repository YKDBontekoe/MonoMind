namespace Autonocraft.World.Structures
{
    internal static class CitadelGenerator
    {
        public static StructureTemplate Generate(in StructureGenContext ctx)
        {
            var rng = ctx.Rng;
            var p = ctx.Palette;
            var b = new StructureBuilder();

            int half = rng.Range(18, 28);
            int depth = rng.Range(4, 8);
            int halls = rng.Range(6, 12);

            StructurePaths.ApproachPath(b, -half, rng.Range(8, 12), 2, p.Path);

            b.Fill(-half, -depth, -half, half, -depth, half, p.Wall, StructurePlacementMode.ReplaceAll)
                .FillHollow(-half, -depth + 1, -half, half, 2, half, p.Wall, BlockType.Air);

            for (int step = 0; step < depth; step++)
            {
                b.Add(0, -step, -half - step - 1, p.Floor, StructurePlacementMode.ReplaceAll);
                b.Add(0, -step - 1, -half - step - 1, BlockType.Air, StructurePlacementMode.ReplaceAll);
                b.PointedArchZ(-half - step - 1, -step, 0, -1, 1, p.Trim, StructurePlacementMode.ReplaceAll);
            }

            for (int i = 0; i < halls; i++)
            {
                int angle = rng.NextInt(4);
                int offset = rng.Range(-half + 3, half - 6);
                switch (angle)
                {
                    case 0:
                        CarveHall(b, -half + 1, -depth + 1, offset, 3, rng.Range(6, 12));
                        break;
                    case 1:
                        CarveHall(b, half - 4, -depth + 1, offset, 3, rng.Range(6, 12));
                        break;
                    case 2:
                        CarveHall(b, offset, -depth + 1, -half + 1, rng.Range(6, 12), 3);
                        break;
                    default:
                        CarveHall(b, offset, -depth + 1, half - 4, rng.Range(6, 12), 3);
                        break;
                }
            }

            b.Add(0, -depth + 1, 0, BlockType.Obsidian, StructurePlacementMode.ReplaceAll)
                .Add(0, -depth + 2, 0, BlockType.GoldBlock, StructurePlacementMode.ReplaceAll)
                .Add(0, -depth + 3, 0, p.GlowAccent, StructurePlacementMode.ReplaceAll)
                .Chest(1, -depth + 1, 0, Loot.LootTableIds.Citadel)
                .Chest(-1, -depth + 1, 1, Loot.LootTableIds.Dungeon);

            for (int i = 0; i < 4; i++)
            {
                int px = rng.Pick(-half + 2, half - 2);
                int pz = rng.Pick(-half + 2, half - 2);
                b.Add(px, -depth + 1, pz, rng.Chance(0.5f) ? BlockType.Amethyst : BlockType.RubyBlock, StructurePlacementMode.ReplaceAll);
            }

            int ruinH = rng.Range(3, 6);
            b.FillHollow(-half, 0, -half, half, ruinH, half, p.WallAccent, BlockType.Air)
                .Add(0, 1, -half, BlockType.Air)
                .Add(0, 2, -half, BlockType.Air)
                .Pillar(-half + 2, 0, -half + 2, ruinH + 2, p.Pillar)
                .Pillar(half - 2, 0, -half + 2, ruinH + 1, p.Pillar)
                .Pillar(-half + 2, 0, half - 2, ruinH, p.Pillar)
                .Pillar(half - 2, 0, half - 2, ruinH + 3, p.Pillar)
                .Battlements(-half, ruinH + 1, -half, half, half, p.Trim);

            b.PointedArchZ(-half, 1, ruinH - 1, -1, 1, p.Trim);
            MedievalDetailKit.StampRuinWear(b, rng, -half, 0, -half, half, ruinH, half, p, 0.22f);

            b.Add(-half + 1, 1, 0, BlockType.StationCrucible)
                .Add(half - 1, 1, 0, BlockType.StationBench)
                .Chest(0, 1, 0, Loot.LootTableIds.Citadel);

            return b.Build(half + 2);
        }

        private static void CarveHall(StructureBuilder b, int x, int y, int z, int w, int d)
        {
            b.Fill(x, y, z, x + w, y, z + d, BlockType.Air, StructurePlacementMode.ReplaceAll);
            b.Fill(x, y + 3, z, x + w, y + 3, z + d, BlockType.Cobblestone, StructurePlacementMode.ReplaceAll);
            b.Add(x + w / 2, y + 1, z + d / 2, BlockType.Lantern, StructurePlacementMode.ReplaceAll);
        }
    }
}

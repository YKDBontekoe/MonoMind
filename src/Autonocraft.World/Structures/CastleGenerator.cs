namespace Autonocraft.World.Structures
{
    internal static class CastleGenerator
    {
        public static StructureTemplate Generate(in StructureGenContext ctx, int minBlocks = 1000)
        {
            var rng = ctx.Rng;
            var p = ctx.Palette;
            var b = new StructureBuilder();

            int half = rng.Range(18, 26);
            int wallH = rng.Range(8, 12);
            int towerH = rng.Range(wallH + 5, wallH + 12);
            int keepHalf = rng.Range(7, 12);

            b.Fill(-half, -1, -half, half, -1, half, p.Foundation, StructurePlacementMode.ReplaceAll)
                .Fill(-half + 1, 0, -half + 1, half - 1, 0, half - 1, p.Floor);

            StructurePaths.ApproachPath(b, -half, rng.Range(6, 10), 2, p.Path);

            b.FillHollow(-half, 1, -half, half, wallH, half, p.Wall, BlockType.Air);

            int gateW = rng.Pick(3, 5);
            int gateX0 = -gateW / 2;
            for (int y = 1; y <= wallH; y++)
            {
                for (int x = gateX0; x <= gateX0 + gateW; x++)
                {
                    b.Add(x, y, -half, BlockType.Air);
                }
            }

            MedievalDetailKit.GatehouseFantasy(b, -half, gateW, wallH, p);
            MedievalDetailKit.WallButtresses(b, half, 1, wallH / 2, 6, p, StructurePlacementMode.ReplaceAll);

            StructurePaths.CorridorZ(b, 0, -half + 2, 0, 1, 3, p);

            int[] corners = { -half, half };
            foreach (int cx in corners)
            {
                foreach (int cz in corners)
                {
                    int tr = rng.Pick(3, 4);
                    RoomStamper.TowerCore(b, cx, 0, cz, tr, towerH, p);
                    b.Battlements(cx - tr, towerH + 1, cz - tr, cx + tr, cz + tr, p.WallAccent);
                    b.Add(cx, towerH - 1, cz, p.GlowAccent, StructurePlacementMode.ReplaceAll);
                }
            }

            b.Battlements(-half, wallH + 1, -half, half, half, p.WallAccent);

            b.FillHollow(-keepHalf, 1, -keepHalf + 6, keepHalf, wallH + 5, keepHalf, p.WallAccent, BlockType.Air)
                .GabledRoof(-keepHalf, -keepHalf + 6, keepHalf, keepHalf, wallH + 6, 3, p.Roof, p.Trim)
                .Add(0, wallH + 8, keepHalf - 2, p.Accent)
                .Chest(0, 2, keepHalf - 2, Loot.LootTableIds.Castle)
                .Chest(-keepHalf + 2, 2, keepHalf - 4, Loot.LootTableIds.Treasury);

            StructurePaths.DoorwaySouth(b, keepHalf - 5, -1, 1, 1, 2, p);

            int roomCount = rng.Range(8, 16);
            for (int i = 0; i < roomCount; i++)
            {
                int side = rng.NextInt(4);
                int offset = rng.Range(-half + 4, half - 8);
                switch (side)
                {
                    case 0:
                        RoomStamper.StampRandomRoom(b, rng, p, -half + 2, 1, offset);
                        break;
                    case 1:
                        RoomStamper.StampRandomRoom(b, rng, p, half - 6, 1, offset);
                        break;
                    case 2:
                        RoomStamper.StampRandomRoom(b, rng, p, offset, 1, -half + 2);
                        break;
                    default:
                        RoomStamper.StampRandomRoom(b, rng, p, offset, 1, half - 6);
                        break;
                }
            }

            b.Add(0, 1, 0, BlockType.Water)
                .Add(0, 2, 0, p.Accent)
                .Pillar(0, 0, -half - 1, wallH, p.Pillar)
                .Fill(-2, wallH + 1, -half - 3, 2, wallH + 1, -half - 1, p.Roof)
                .Add(0, wallH, -half - 2, p.GlowAccent);

            if (rng.Chance(0.6f))
            {
                int wingHalf = rng.Range(5, 9);
                int wingZ = half - wingHalf - 2;
                b.FillHollow(-wingHalf, 1, wingZ, wingHalf, wallH - 2, half - 2, p.Wall, BlockType.Air)
                    .GabledRoof(-wingHalf, wingZ, wingHalf, half - 2, wallH - 1, 2, p.Roof, p.Trim);
            }

            MedievalDetailKit.StampRuinWear(b, rng, -half, 1, -half, half, wallH, half, p, 0.18f, StructurePlacementMode.ReplaceAll);

            var template = b.Build(half + 4);
            if (template.Blocks.Length < minBlocks && ctx.VariantSalt < 10_000)
            {
                var retry = StructureGenContext.Create(
                    ctx.WorldSeed,
                    ctx.AnchorX,
                    ctx.AnchorZ,
                    ctx.VariantSalt + 17,
                    ctx.Biome);
                return Generate(retry, minBlocks);
            }

            return template;
        }
    }
}

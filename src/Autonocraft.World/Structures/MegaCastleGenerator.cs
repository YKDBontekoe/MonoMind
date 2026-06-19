using Autonocraft.Domain.World;
using Autonocraft.World.Loot;

namespace Autonocraft.World.Structures
{
    internal static class MegaCastleGenerator
    {
        public static StructureTemplate Generate(in StructureGenContext ctx)
        {
            var rng = ctx.Rng;
            var p = BiomeStructurePalette.ForMega(ctx.Biome);
            var b = new StructureBuilder();

            int outerHalf = rng.Range(32, 38);
            int innerHalf = outerHalf - 12;
            int wallH = rng.Range(10, 14);
            int towerH = wallH + rng.Range(8, 14);
            int keepHalf = rng.Range(9, 12);

            b.Fill(-outerHalf, -2, -outerHalf, outerHalf, -1, outerHalf, p.Foundation, StructurePlacementMode.ReplaceAll)
                .Fill(-outerHalf + 1, 0, -outerHalf + 1, outerHalf - 1, 0, outerHalf - 1, p.Floor);

            MegaStructureKit.MoatRing(b, outerHalf, 3);
            MegaStructureKit.StoneBridge(b, -outerHalf - 4, 3, 6, p.Path);
            StructurePaths.ApproachPath(b, -outerHalf, rng.Range(10, 14), 3, p.Path);

            b.FillHollow(-outerHalf, 1, -outerHalf, outerHalf, wallH, outerHalf, p.Wall, BlockType.Air, StructurePlacementMode.ReplaceAll);
            MegaStructureKit.BandedWallRing(b, outerHalf, 2, wallH - 1, p.Wall, p.WallAccent);

            int gateW = rng.Pick(5, 7);
            for (int y = 1; y <= wallH; y++)
            {
                for (int x = -gateW / 2; x <= gateW / 2; x++)
                {
                    b.Add(x, y, -outerHalf, BlockType.Air, StructurePlacementMode.ReplaceAll);
                }
            }

            MedievalDetailKit.GatehouseFantasy(b, -outerHalf, gateW, wallH, p, StructurePlacementMode.ReplaceAll);
            MegaStructureKit.GatehouseSouth(b, -outerHalf + 1, 5, wallH + 2, 4, p);
            MedievalDetailKit.WallButtresses(b, outerHalf, 1, wallH / 2 + 1, 8, p, StructurePlacementMode.ReplaceAll);

            int[] corners = { -outerHalf, outerHalf };
            foreach (int cx in corners)
            {
                foreach (int cz in corners)
                {
                    int tr = rng.Pick(4, 5);
                    RoomStamper.TowerCore(b, cx, 0, cz, tr, towerH, p);
                    b.Battlements(cx - tr, towerH + 1, cz - tr, cx + tr, cz + tr, p.Trim, StructurePlacementMode.ReplaceAll);
                    b.Add(cx, towerH - 2, cz, p.GlowAccent, StructurePlacementMode.ReplaceAll);
                    b.Add(cx, towerH + 2, cz, p.Accent, StructurePlacementMode.ReplaceAll);
                }
            }

            foreach (int offset in new[] { 0, -outerHalf + 8, outerHalf - 8 })
            {
                int tr = 3;
                int midTowerH = wallH + 6;
                RoomStamper.TowerCore(b, offset, 0, -outerHalf, tr, midTowerH, p);
                RoomStamper.TowerCore(b, offset, 0, outerHalf, tr, midTowerH, p);
                RoomStamper.TowerCore(b, -outerHalf, 0, offset, tr, midTowerH, p);
                RoomStamper.TowerCore(b, outerHalf, 0, offset, tr, midTowerH, p);
            }

            b.Battlements(-outerHalf, wallH + 1, -outerHalf, outerHalf, outerHalf, p.Trim, StructurePlacementMode.ReplaceAll);

            b.FillHollow(-innerHalf, 1, -innerHalf, innerHalf, wallH - 1, innerHalf, p.WallAccent, BlockType.Air, StructurePlacementMode.ReplaceAll);
            MegaStructureKit.CourtyardCross(b, innerHalf - 2, 0, 2, p);
            MegaStructureKit.Fountain(b, 0, p.Trim, p.Accent);
            MedievalDetailKit.CourtyardLanterns(b, innerHalf - 4, 1, p, StructurePlacementMode.ReplaceAll);

            StructurePaths.CorridorZ(b, 0, -innerHalf + 2, 0, 1, 3, p);

            b.FillHollow(-keepHalf, 1, innerHalf - keepHalf - 2, keepHalf, wallH + 6, innerHalf - 2, p.Wall, BlockType.Air, StructurePlacementMode.ReplaceAll)
                .GabledRoof(-keepHalf, innerHalf - keepHalf - 2, keepHalf, innerHalf - 2, wallH + 7, 3, p.Roof, p.Trim, ridgeAlongX: false, StructurePlacementMode.ReplaceAll)
                .Add(0, wallH + 10, innerHalf - keepHalf, p.Accent, StructurePlacementMode.ReplaceAll)
                .Chest(0, 2, innerHalf - keepHalf + 1, LootTableIds.Castle)
                .Chest(-keepHalf + 2, 2, innerHalf - 4, LootTableIds.Treasury)
                .Chest(keepHalf - 2, 2, innerHalf - 4, LootTableIds.Castle);

            StructurePaths.DoorwaySouth(b, innerHalf - keepHalf - 3, -1, 1, 1, 2, p);

            RoomStamper.Hall(b, -2, 1, -innerHalf + 4, rng.Range(10, 14), p);
            RoomStamper.Chapel(b, innerHalf - 8, 1, -4, p, rng);
            RoomStamper.Treasury(b, -innerHalf + 4, 1, -6, p, rng);
            RoomStamper.ThroneRoom(b, innerHalf - 10, 1, 4, p, rng);

            int roomCount = rng.Range(14, 22);
            for (int i = 0; i < roomCount; i++)
            {
                int side = rng.NextInt(4);
                int offset = rng.Range(-innerHalf + 4, innerHalf - 8);
                switch (side)
                {
                    case 0:
                        RoomStamper.StampRandomRoom(b, rng, p, -innerHalf + 2, 1, offset);
                        break;
                    case 1:
                        RoomStamper.StampRandomRoom(b, rng, p, innerHalf - 6, 1, offset);
                        break;
                    case 2:
                        RoomStamper.StampRandomRoom(b, rng, p, offset, 1, -innerHalf + 2);
                        break;
                    default:
                        RoomStamper.StampRandomRoom(b, rng, p, offset, 1, innerHalf - 6);
                        break;
                }
            }

            int basementY = -3;
            b.Fill(-keepHalf, basementY, innerHalf - keepHalf - 2, keepHalf, basementY, innerHalf - 2, p.Foundation, StructurePlacementMode.ReplaceAll)
                .FillHollow(-keepHalf + 1, basementY + 1, innerHalf - keepHalf - 1, keepHalf - 1, basementY + 3, innerHalf - 3, p.Wall, BlockType.Air, StructurePlacementMode.ReplaceAll);
            for (int i = 0; i < 6; i++)
            {
                MegaStructureKit.DungeonCell(
                    b,
                    rng.Range(-keepHalf + 2, keepHalf - 5),
                    basementY + 1,
                    innerHalf - rng.Range(6, keepHalf + 2),
                    rng.Pick(3, 4),
                    p,
                    rng);
            }

            b.Chest(0, basementY + 1, innerHalf - keepHalf, LootTableIds.Dungeon);

            MegaStructureKit.WindowBand(b, wallH / 2, -outerHalf + 2, outerHalf - 2, -outerHalf, p.Trim, p.Window);
            MegaStructureKit.WindowBand(b, wallH / 2, -outerHalf + 2, outerHalf - 2, outerHalf, p.Trim, p.Window);

            int wingHalf = rng.Range(7, 10);
            int wingZ = outerHalf - wingHalf - 3;
            b.FillHollow(-wingHalf, 1, wingZ, wingHalf, wallH, outerHalf - 2, p.WallAccent, BlockType.Air, StructurePlacementMode.ReplaceAll)
                .GabledRoof(-wingHalf, wingZ, wingHalf, outerHalf - 2, wallH + 1, 2, p.Roof, p.Trim, mode: StructurePlacementMode.ReplaceAll);
            RoomStamper.Barracks(b, -wingHalf + 1, 1, wingZ + 1, p, rng);
            b.Chest(0, 2, wingZ + 2, LootTableIds.Medium);

            MedievalDetailKit.StampRuinWear(b, rng, -outerHalf, 1, -outerHalf, outerHalf, wallH, outerHalf, p, 0.15f, StructurePlacementMode.ReplaceAll);

            return b.Build(outerHalf + 6);
        }
    }
}

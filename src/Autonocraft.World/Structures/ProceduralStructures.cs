using System;
using Autonocraft.Domain.World;
using Autonocraft.World.Loot;

namespace Autonocraft.World.Structures
{
    public static class ProceduralStructures
    {
        public static StructureTemplate ForestShelter(in StructureGenContext ctx)
        {
            var rng = ctx.Rng;
            var p = ctx.Palette;
            int span = rng.Pick(2, 3);
            int height = rng.Range(4, 6);
            var b = new StructureBuilder()
                .Fill(-span - 1, 0, -span - 1, span + 1, 0, span + 1, p.Foundation)
                .FillHollow(-span, 1, -span, span, height - 1, span, p.Wall, BlockType.Air)
                .Pillar(-span, -span, 1, height, p.Pillar)
                .Pillar(span, -span, 1, height, p.Pillar)
                .Pillar(-span, span, 1, height, p.Pillar)
                .Pillar(span, span, 1, height, p.Pillar);

            StructurePaths.ApproachPath(b, -span, rng.Range(4, 6), 1, p.Path);
            StructurePaths.DoorwaySouth(b, -span, -1, 1, 1, 2, p);

            b.HipRoof(-span, -span, span, span, height, 2, p.Roof, p.Trim)
                .Add(0, 1, 0, BlockType.HayBale)
                .Add(0, height - 1, 0, p.Accent)
                .Add(0, height + 1, 0, p.GlowAccent)
                .Chest(span - 1, 1, 0, LootTableIds.Small);

            if (rng.Chance(0.35f))
            {
                MedievalDetailKit.StampRuinWear(b, rng, -span, 1, -span, span, height - 1, span, p, 0.12f);
            }

            return b.Build(span + 2);
        }

        public static StructureTemplate PlainsWell(in StructureGenContext ctx)
        {
            var rng = ctx.Rng;
            var p = ctx.Palette;
            int rim = rng.Pick(2, 3);
            int roofY = 5 + rng.NextInt(2);
            var b = new StructureBuilder()
                .Fill(-rim, 0, -rim, rim, 0, rim, p.Foundation)
                .FillHollow(-rim + 1, -1, -rim + 1, rim - 1, 0, rim - 1, p.Foundation, BlockType.Water);

            for (int x = -rim; x <= rim; x += rim)
            {
                for (int z = -rim; z <= rim; z += rim)
                {
                    int pillarH = 4 + rng.NextInt(2);
                    b.Pillar(x, 1, z, pillarH, p.Pillar);
                    if (x == -rim && z == -rim)
                    {
                        b.PointedArchZ(z, 2, pillarH, x, x + rim, p.Trim);
                    }
                    if (x == rim && z == -rim)
                    {
                        b.PointedArchZ(z, 2, pillarH, x - rim, x, p.Trim);
                    }
                }
            }

            b.Fill(-rim, roofY, -rim, rim, roofY, rim, p.Roof)
                .Add(0, 3, 0, BlockType.Rope)
                .Add(0, 4, 0, p.Accent)
                .Add(0, roofY + 1, 0, p.GlowAccent);
            return b.Build(rim + 1);
        }

        public static StructureTemplate DesertCairn(in StructureGenContext ctx)
        {
            var rng = ctx.Rng;
            var p = ctx.Palette;
            int tiers = rng.Range(3, 5);
            var b = new StructureBuilder();
            for (int t = 0; t < tiers; t++)
            {
                int r = tiers - t;
                b.Fill(-r, t, -r, r, t, r, p.Wall);
                if (t % 2 == 0)
                {
                    b.Fill(-r, t, -r, r, t, r, p.Trim);
                }
            }

            b.Pillar(0, tiers, 0, tiers + rng.Range(3, 6), p.Trim)
                .Add(0, tiers - 1, 0, p.Accent)
                .Add(0, tiers + 6, 0, p.GlowAccent);
            return b.Build(tiers + 2);
        }

        public static StructureTemplate SwampShrine(in StructureGenContext ctx)
        {
            var rng = ctx.Rng;
            var p = ctx.Palette;
            int r = rng.Pick(2, 3);
            var b = new StructureBuilder()
                .Fill(-r - 1, 0, -r - 1, r + 1, 0, r + 1, BlockType.Mud)
                .Fill(-r, 0, -r, r, 0, r, p.Foundation)
                .Pillar(-r, 1, -r, 3 + rng.NextInt(2), p.Ruin)
                .Pillar(r, 1, -r, 3 + rng.NextInt(2), p.Ruin)
                .Pillar(-r, 1, r, 3 + rng.NextInt(2), p.Ruin)
                .Pillar(r, 1, r, 3 + rng.NextInt(2), p.Ruin)
                .Fill(-1, 1, -1, 1, 1, 1, p.Floor)
                .Add(0, 1, 0, BlockType.Obsidian)
                .LancetWindow(0, 2, 0, 2, p.Trim, BlockType.RedStainedGlass, p.GlowAccent)
                .Pillar(0, 3, 0, 4 + rng.NextInt(2), p.Pillar);
            return b.Build(r + 2);
        }

        public static StructureTemplate BeachPost(in StructureGenContext ctx)
        {
            var rng = ctx.Rng;
            var p = ctx.Palette;
            int w = rng.Pick(2, 3);
            var b = new StructureBuilder()
                .Fill(-w, 0, -w, w, 0, w, BlockType.Sand)
                .Pillar(-w + 1, 1, 0, 4 + rng.NextInt(2), p.Pillar)
                .Pillar(w - 1, 1, 0, 4 + rng.NextInt(2), p.Pillar)
                .Fill(-w, 5, -w, w, 5, w, p.Roof);

            for (int layer = 0; layer < 2; layer++)
            {
                int inset = layer;
                b.Fill(-w - 1 + inset, 6 + layer, -w - 1 + inset, w + 1 - inset, 6 + layer, w + 1 - inset, BlockType.PalmLeaves);
            }

            b.Add(0, 7, 0, p.Pillar)
                .Add(0, 5, 0, p.Accent)
                .Add(0, 6, 0, p.GlowAccent);
            return b.Build(w + 2);
        }

        public static StructureTemplate MountainCairn(in StructureGenContext ctx)
        {
            var rng = ctx.Rng;
            var p = ctx.Palette;
            int tiers = 3 + rng.NextInt(2);
            var b = new StructureBuilder().Fill(-2, 0, -2, 2, 0, 2, p.Foundation);
            for (int y = 1; y <= tiers; y++)
            {
                int r = Math.Max(0, 3 - y / 2);
                b.Fill(-r, y, -r, r, y, r, p.Wall);
            }

            b.PyramidRoof(0, 0, 2, tiers + 1, 2, p.Roof, p.Trim)
                .Pillar(0, tiers + 1, 0, 6 + rng.NextInt(3), p.Trim)
                .Add(0, 7, 0, p.Accent)
                .Add(0, tiers + 4, 0, p.GlowAccent);
            return b.Build(3);
        }

        public static StructureTemplate PlainsCottage(in StructureGenContext ctx)
        {
            var rng = ctx.Rng;
            var p = ctx.Palette;
            int w = rng.Range(3, 5);
            int h = rng.Range(3, 5);
            var b = new StructureBuilder()
                .Fill(-w - 1, 0, -w - 1, w + 1, 0, w + 1, p.Foundation)
                .FillHollow(-w, 1, -w, w, h, w, p.Wall, BlockType.Air)
                .Add(0, 1, -w, BlockType.Air)
                .Add(0, 2, -w, BlockType.Air)
                .LancetWindow(-w, 2, 0, 2, p.Trim, p.Window, p.GlowAccent)
                .LancetWindow(w, 2, 0, 2, p.Trim, p.Window, p.GlowAccent);

            MedievalDetailKit.HalfTimberWalls(b, -w, w, 1, h, -w, w, p);
            b.GabledRoof(-w, -w, w, w, h, w / 2 + 1, p.Roof, p.Trim)
                .Chimney(w - 1, w - 1, h, 3, p.Pillar, p.Trim, rng.Chance(0.4f))
                .Add(-1, 1, w - 1, BlockType.StationBench)
                .Add(1, 1, w - 1, BlockType.HayBale)
                .Add(0, h, 0, p.Accent)
                .Chest(-w + 1, 1, w - 1, LootTableIds.Medium);

            StructurePaths.ApproachPath(b, -w, rng.Range(4, 7), 1, p.Path);
            StructurePaths.DoorwaySouth(b, -w, -1, 1, 1, 2, p);
            b.Add(0, 3, -w - 1, p.GlowAccent);
            return b.Build(w + 2);
        }

        public static StructureTemplate ForestWatchtower(in StructureGenContext ctx)
        {
            var rng = ctx.Rng;
            var p = ctx.Palette;
            int r = rng.Pick(2, 3);
            int baseH = rng.Range(6, 9);
            int topH = baseH + rng.Range(4, 7);
            var b = new StructureBuilder()
                .Fill(-r, 0, -r, r, 0, r, p.Foundation)
                .Pillar(-r, 1, -r, baseH, p.Pillar)
                .Pillar(r, 1, -r, baseH, p.Pillar)
                .Pillar(-r, 1, r, baseH, p.Pillar)
                .Pillar(r, 1, r, baseH, p.Pillar)
                .Fill(-r, baseH, -r, r, baseH, r, p.Floor)
                .FillHollow(-r, baseH + 1, -r, r, baseH + 3, r, p.Wall, BlockType.Air)
                .Add(0, baseH + 1, -r, BlockType.Air)
                .Add(0, baseH + 2, -r, BlockType.Air)
                .Pillar(-r, baseH + 1, -r, topH, p.Pillar)
                .Pillar(r, baseH + 1, -r, topH, p.Pillar)
                .Pillar(-r, baseH + 1, r, topH, p.Pillar)
                .Pillar(r, baseH + 1, r, topH, p.Pillar)
                .PyramidRoof(0, 0, r, topH, 2, p.Roof, p.Trim)
                .Battlements(-r, topH + 1, -r, r, r, p.Trim)
                .ArrowSlit(-r, baseH + 2, 0, 2, p.Trim)
                .ArrowSlit(r, baseH + 2, 0, 2, p.Trim)
                .ArrowSlit(0, baseH + 2, -r, 2, p.Trim)
                .ArrowSlit(0, baseH + 2, r, 2, p.Trim)
                .SpiralStair(0, 0, 1, Math.Min(topH - 2, 10), Math.Max(1, r - 1), BlockType.StoneSlab, p.Pillar)
                .Add(0, topH + 2, 0, p.GlowAccent)
                .Add(0, topH - 2, 0, p.Accent);
            return b.Build(r + 2);
        }

        public static StructureTemplate DesertShrine(in StructureGenContext ctx)
        {
            var rng = ctx.Rng;
            var p = ctx.Palette;
            int r = rng.Range(3, 5);
            var b = new StructureBuilder()
                .Fill(-r - 1, 0, -r - 1, r + 1, 0, r + 1, BlockType.Sand)
                .Pillar(-r, 1, -r, 5 + rng.NextInt(2), p.Wall)
                .Pillar(r, 1, -r, 5 + rng.NextInt(2), p.Wall)
                .Pillar(-r, 1, r, 5 + rng.NextInt(2), p.Wall)
                .Pillar(r, 1, r, 5 + rng.NextInt(2), p.Wall)
                .FillHollow(-r + 1, 1, -r + 1, r - 1, 3, r - 1, p.Wall, BlockType.Air)
                .Add(0, 1, -r, BlockType.Air)
                .Add(0, 2, -r, BlockType.Air)
                .Add(0, 1, 0, p.Accent)
                .Add(0, 2, 0, p.GlowAccent);

            b.PointedArchZ(-r, 1, 3, -1, 1, p.Trim);

            for (int t = 0; t < 3; t++)
            {
                int shrink = t;
                b.Fill(-r + shrink, 4 + t, -r + shrink, r - shrink, 4 + t, r - shrink, p.Roof);
            }

            b.PyramidRoof(0, 0, 1, 7, 2, p.Roof, p.Trim)
                .Add(0, 9, 0, p.Trim);
            return b.Build(r + 2);
        }

        public static StructureTemplate SwampAltar(in StructureGenContext ctx)
        {
            var rng = ctx.Rng;
            var p = ctx.Palette;
            int r = rng.Pick(2, 3);
            var b = new StructureBuilder()
                .Fill(-r - 1, 0, -r - 1, r + 1, 0, r + 1, p.Foundation)
                .Fill(-r, 0, -r, r, 0, r, p.Floor)
                .Pillar(-r, 1, -r, 2, p.Wall)
                .Pillar(r, 1, -r, 2, p.Wall)
                .Pillar(-r, 1, r, 2, p.Wall)
                .Pillar(r, 1, r, 2, p.Wall)
                .Pillar(-r, 1, 0, 4 + rng.NextInt(2), p.Wall)
                .Pillar(r, 1, 0, 4 + rng.NextInt(2), p.Wall)
                .Add(0, 1, 0, BlockType.Obsidian)
                .LancetWindow(0, 2, 0, 2, p.Trim, p.Window, p.GlowAccent)
                .Add(0, 3, 0, p.Accent);

            b.PointedArchZ(-r, 1, 3, -1, 1, p.Trim)
                .PointedArchZ(r, 1, 3, -1, 1, p.Trim);
            return b.Build(r + 2);
        }

        public static StructureTemplate SnowyHut(in StructureGenContext ctx)
        {
            var rng = ctx.Rng;
            var p = ctx.Palette;
            int r = rng.Pick(2, 3);
            int wallH = 2 + rng.NextInt(2);
            var b = new StructureBuilder()
                .Fill(-r, 0, -r, r, 0, r, p.Foundation)
                .FillHollow(-r, 1, -r, r, wallH, r, p.Wall, BlockType.Air)
                .Add(0, 1, -r, BlockType.Air)
                .Add(0, 2, -r, BlockType.Air)
                .LancetWindow(-r, 2, 0, 2, p.Trim, p.Window)
                .LancetWindow(r, 2, 0, 2, p.Trim, p.Window)
                .Dome(0, 0, wallH + 1, r, p.Wall)
                .Chimney(r - 1, -r + 1, wallH, 3, p.Pillar, p.Trim, withLantern: false)
                .Add(0, 1, 0, p.Accent)
                .Add(0, wallH + r + 2, 0, p.GlowAccent);
            return b.Build(r + 2);
        }

        public static StructureTemplate VillageOutpost(in StructureGenContext ctx)
        {
            var rng = ctx.Rng;
            var p = ctx.Palette;
            int r = rng.Range(3, 5);
            int wallH = rng.Range(4, 6);
            var b = new StructureBuilder()
                .Fill(-r, 0, -r, r, 0, r, p.Foundation);

            for (int x = -r; x <= r; x += 2)
            {
                b.Pillar(x, 1, -r, wallH, p.Pillar)
                    .Pillar(x, 1, r, wallH, p.Pillar);
            }

            for (int z = -r + 2; z <= r - 2; z += 2)
            {
                b.Pillar(-r, 1, z, wallH, p.Pillar)
                    .Pillar(r, 1, z, wallH, p.Pillar);
            }

            b.Add(0, 1, -r, BlockType.Air)
                .Add(0, 2, -r, BlockType.Air)
                .Fill(-2, 1, -2, 2, 1, 2, p.Floor)
                .FillHollow(-2, 2, -2, 2, wallH + 2, 2, p.Wall, BlockType.Air)
                .GabledRoof(-2, -2, 2, 2, wallH + 3, 2, p.Roof, p.Trim)
                .Add(-1, 1, 1, BlockType.StationBench)
                .Add(1, 1, 1, BlockType.HayBale)
                .Add(0, wallH + 1, 0, p.Accent)
                .Chest(2, 2, 0, LootTableIds.Medium);

            MedievalDetailKit.WallButtresses(b, r, 1, wallH / 2, 4, p);
            b.PointedArchZ(-r, 1, wallH - 1, -1, 1, p.Trim);
            MedievalDetailKit.CourtyardLanterns(b, r - 3, 2, p);

            StructurePaths.ApproachPath(b, -r, rng.Range(5, 8), 2, p.Path);
            StructurePaths.DoorwaySouth(b, -2, -1, 1, 2, 3, p);
            return b.Build(r + 2);
        }

        public static StructureTemplate BadlandsSpire(in StructureGenContext ctx)
        {
            var rng = ctx.Rng;
            var p = ctx.Palette;
            int h = rng.Range(8, 14);
            var b = new StructureBuilder().Fill(-2, 0, -2, 2, 0, 2, p.Foundation);
            for (int y = 1; y <= h; y++)
            {
                int r = Math.Max(0, 2 - y / 4);
                BlockType layer = (y % 3) switch
                {
                    0 => p.Wall,
                    1 => BlockType.RedSand,
                    _ => BlockType.GoldOre
                };
                b.Fill(-r, y, -r, r, y, r, layer);
            }

            b.PyramidRoof(0, 0, 1, h + 1, 3, p.Roof, p.Accent)
                .Add(0, h + 4, 0, p.GlowAccent);
            return b.Build(3);
        }

        public static StructureTemplate MushroomCircle(in StructureGenContext ctx)
        {
            var rng = ctx.Rng;
            var p = ctx.Palette;
            int r = rng.Pick(2, 3);
            var b = new StructureBuilder().Fill(-r, 0, -r, r, 0, r, BlockType.MossCarpet);
            for (int i = 0; i < 4 + rng.NextInt(3); i++)
            {
                int x = rng.Range(-r, r);
                int z = rng.Range(-r, r);
                b.Add(x, 0, z, rng.Chance(0.5f) ? BlockType.MushroomRed : BlockType.MushroomBrown);
            }

            for (int angle = 0; angle < 8; angle++)
            {
                double rad = angle * Math.PI / 4;
                int lx = (int)Math.Round(Math.Cos(rad) * r);
                int lz = (int)Math.Round(Math.Sin(rad) * r);
                b.Add(lx, 1, lz, BlockType.Glowshroom);
            }

            b.Pillar(0, 1, 0, 3 + rng.NextInt(2), BlockType.MushroomBrown)
                .Fill(-r, 4, -r, r, 4, r, BlockType.MushroomRed)
                .Fill(-1, 5, -1, 1, 5, 1, BlockType.MushroomRed)
                .Add(0, 6, 0, p.GlowAccent);
            return b.Build(r + 2);
        }

        public static StructureTemplate VolcanicVent(in StructureGenContext ctx)
        {
            var rng = ctx.Rng;
            var p = ctx.Palette;
            int r = rng.Pick(2, 3);
            var b = new StructureBuilder()
                .Fill(-r - 1, 0, -r - 1, r + 1, 0, r + 1, p.Foundation)
                .FillHollow(-r, 0, -r, r, 0, r, BlockType.Obsidian, BlockType.Lava);

            b.PointedArchZ(-r, 1, 3, -r, r, p.Trim)
                .PointedArchZ(r, 1, 3, -r, r, p.Trim)
                .PointedArchX(-r, 1, 3, -r, r, p.Trim)
                .PointedArchX(r, 1, 3, -r, r, p.Trim)
                .Pillar(0, 1, 0, 3 + rng.NextInt(3), p.Wall)
                .Add(0, 4 + rng.NextInt(2), 0, BlockType.MagmaBlock)
                .Add(0, 5 + rng.NextInt(2), 0, p.GlowAccent);
            return b.Build(r + 2);
        }

        public static StructureTemplate MangroveDock(in StructureGenContext ctx)
        {
            var rng = ctx.Rng;
            var p = ctx.Palette;
            int len = rng.Range(3, 6);
            var b = new StructureBuilder()
                .Pillar(-2, 0, 0, 2 + rng.NextInt(2), p.Pillar)
                .Pillar(2, 0, 0, 2 + rng.NextInt(2), p.Pillar)
                .Fill(-2, 3, -1, 2, 3, 1, p.Floor)
                .Pillar(-2, 3, -1, 4 + rng.NextInt(2), p.Pillar)
                .Pillar(2, 3, -1, 4 + rng.NextInt(2), p.Pillar)
                .Pillar(-2, 3, 1, 4 + rng.NextInt(2), p.Pillar)
                .Pillar(2, 3, 1, 4 + rng.NextInt(2), p.Pillar)
                .Fill(-1, 3, 2, 1, 3, 2 + len, p.Floor)
                .Add(0, 3, len / 2, BlockType.BerryBush)
                .Add(0, 5, 0, p.Accent)
                .Add(-2, 5, 0, p.GlowAccent)
                .Add(2, 5, 0, p.GlowAccent)
                .Add(0, 4, 2 + len, BlockType.Rope);
            return b.Build(len + 2);
        }

        public static StructureTemplate AbandonedCastle(in StructureGenContext ctx)
        {
            return CastleGenerator.Generate(ctx, minBlocks: 400);
        }

        public static StructureTemplate RuinedDungeon(in StructureGenContext ctx)
        {
            return CitadelGenerator.Generate(ctx);
        }

        public static StructureTemplate MegaCastle(in StructureGenContext ctx)
        {
            return MegaCastleGenerator.Generate(ctx);
        }

        public static StructureTemplate MegaCitadel(in StructureGenContext ctx)
        {
            return MegaCitadelGenerator.Generate(ctx);
        }
    }
}

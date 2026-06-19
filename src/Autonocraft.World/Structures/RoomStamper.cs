using Autonocraft.Domain.World;
using Autonocraft.World.Loot;

namespace Autonocraft.World.Structures
{
    internal static class RoomStamper
    {
        public static void Barracks(StructureBuilder b, int ox, int oy, int oz, in BiomeStructurePalette palette, StructureRng rng)
        {
            int w = rng.Pick(3, 4, 5);
            int d = rng.Pick(3, 4);
            int h = rng.Pick(3, 4);
            b.Fill(ox, oy, oz, ox + w, oy, oz + d, palette.Floor)
                .FillHollow(ox, oy + 1, oz, ox + w, oy + h, oz + d, palette.Wall, BlockType.Air)
                .Add(ox + 1, oy + 1, oz + 1, BlockType.HayBale)
                .Add(ox + w - 1, oy + 1, oz + d - 1, BlockType.HayBale)
                .GabledRoof(ox, oz, ox + w, oz + d, oy + h, 2, palette.Roof, palette.Trim);

            StructurePaths.DoorwaySouth(b, oz, ox + 1, ox + w - 1, oy + 1, oy + 2, palette);
            b.Chest(ox + w - 1, oy + 1, oz + 1, LootTableIds.Small);
        }

        public static void Treasury(StructureBuilder b, int ox, int oy, int oz, in BiomeStructurePalette palette, StructureRng rng)
        {
            int size = rng.Pick(3, 4);
            b.Fill(ox, oy, oz, ox + size, oy, oz + size, palette.Foundation)
                .FillHollow(ox, oy + 1, oz, ox + size, oy + 3, oz + size, palette.WallAccent, BlockType.Air)
                .Add(ox + size / 2, oy + 1, oz + size / 2, BlockType.GoldBlock)
                .Add(ox + size / 2, oy + 2, oz + size / 2, palette.Accent)
                .Add(ox + size / 2, oy + 3, oz + size / 2, palette.GlowAccent)
                .Chest(ox + 1, oy + 1, oz + size - 1, LootTableIds.Treasury);

            StructurePaths.DoorwaySouth(b, oz, ox + 1, ox + size - 1, oy + 1, oy + 2, palette);
            b.PointedArchZ(oz, oy + 1, oy + 3, ox, ox + size, palette.Trim);
        }

        public static void Chapel(StructureBuilder b, int ox, int oy, int oz, in BiomeStructurePalette palette, StructureRng rng)
        {
            int w = 5;
            int d = 4;
            b.Fill(ox, oy, oz, ox + w, oy, oz + d, palette.Floor)
                .FillHollow(ox, oy + 1, oz, ox + w, oy + 4, oz + d, palette.Wall, BlockType.Air)
                .LancetWindow(ox + w / 2, oy + 1, oz, 3, palette.Trim, palette.Window, palette.GlowAccent)
                .LancetWindow(ox + w / 2, oy + 1, oz + d, 3, palette.Trim, palette.Window, palette.GlowAccent)
                .Add(ox + w / 2, oy + 1, oz + d / 2, palette.Accent)
                .Add(ox + w / 2, oy + 2, oz + d / 2, palette.GlowAccent)
                .GabledRoof(ox, oz, ox + w, oz + d, oy + 5, 2, palette.Roof, palette.Trim);

            for (int x = ox + 1; x < ox + w; x += 2)
            {
                b.Add(x, oy + 4, oz + d / 2, BlockType.StoneSlab);
            }

            StructurePaths.DoorwaySouth(b, oz, ox + 1, ox + w - 1, oy + 1, oy + 2, palette);
            b.PointedArchZ(oz, oy + 1, oy + 3, ox, ox + w, palette.Trim);
            b.Chest(ox + 1, oy + 1, oz + d - 1, LootTableIds.Medium);
        }

        public static void Hall(StructureBuilder b, int ox, int oy, int oz, int length, in BiomeStructurePalette palette)
        {
            b.Fill(ox, oy, oz, ox + 3, oy, oz + length, palette.Floor)
                .FillHollow(ox, oy + 1, oz, ox + 3, oy + 4, oz + length, palette.Wall, BlockType.Air)
                .Fill(ox + 1, oy + 1, oz + 1, ox + 2, oy + 1, oz + length - 1, BlockType.StationBench)
                .Add(ox + 1, oy + 3, oz + length / 2, palette.GlowAccent);

            StructurePaths.DoorwaySouth(b, oz, ox + 1, ox + 2, oy + 1, oy + 2, palette);
            b.Add(ox + 3, oy + 1, oz + length - 1, BlockType.Air)
                .Add(ox + 3, oy + 2, oz + length - 1, BlockType.Air);
        }

        public static void Armory(StructureBuilder b, int ox, int oy, int oz, in BiomeStructurePalette palette, StructureRng rng)
        {
            int w = rng.Pick(4, 5);
            int d = rng.Pick(3, 4);
            b.Fill(ox, oy, oz, ox + w, oy, oz + d, palette.Floor)
                .FillHollow(ox, oy + 1, oz, ox + w, oy + 3, oz + d, palette.Wall, BlockType.Air)
                .Add(ox + 1, oy + 1, oz + 1, BlockType.StationStonecutter)
                .Add(ox + w - 1, oy + 1, oz + 1, BlockType.IronBlock)
                .Add(ox + w / 2, oy + 2, oz + d / 2, palette.GlowAccent)
                .Chest(ox + w - 1, oy + 1, oz + d - 1, LootTableIds.Medium);

            StructurePaths.DoorwaySouth(b, oz, ox + 1, ox + w - 1, oy + 1, oy + 2, palette);
        }

        public static void ThroneRoom(StructureBuilder b, int ox, int oy, int oz, in BiomeStructurePalette palette, StructureRng rng)
        {
            int w = rng.Pick(5, 6);
            int d = rng.Pick(5, 6);
            b.Fill(ox, oy, oz, ox + w, oy, oz + d, palette.Floor)
                .FillHollow(ox, oy + 1, oz, ox + w, oy + 4, oz + d, palette.WallAccent, BlockType.Air)
                .Fill(ox + 2, oy, oz + d - 2, ox + w - 2, oy, oz + d - 1, BlockType.StoneSlab)
                .Add(ox + w / 2, oy + 1, oz + d - 2, BlockType.GoldBlock)
                .Add(ox + 1, oy + 2, oz + 1, palette.GlowAccent)
                .Add(ox + w - 1, oy + 2, oz + 1, palette.GlowAccent)
                .Chest(ox + 1, oy + 1, oz + 1, LootTableIds.Treasury);

            StructurePaths.DoorwaySouth(b, oz, ox + 1, ox + w - 1, oy + 1, oy + 2, palette);
            b.PointedArchZ(oz, oy + 1, oy + 3, ox, ox + w, palette.Trim);
        }

        public static void Alchemist(StructureBuilder b, int ox, int oy, int oz, in BiomeStructurePalette palette, StructureRng rng)
        {
            int size = rng.Pick(3, 4);
            b.Fill(ox, oy, oz, ox + size, oy, oz + size, palette.Floor)
                .FillHollow(ox, oy + 1, oz, ox + size, oy + 3, oz + size, palette.Wall, BlockType.Air)
                .Add(ox + size / 2, oy + 1, oz + size / 2, BlockType.StationCrucible)
                .Add(ox + 1, oy + 1, oz + 1, palette.Accent)
                .Add(ox + size - 1, oy + 2, oz + size - 1, palette.GlowAccent)
                .Chest(ox + 1, oy + 1, oz + size - 1, LootTableIds.Medium);

            StructurePaths.DoorwaySouth(b, oz, ox + 1, ox + size - 1, oy + 1, oy + 2, palette);
        }

        public static void TowerCore(StructureBuilder b, int cx, int oy, int cz, int radius, int height, in BiomeStructurePalette palette)
        {
            b.FillHollow(cx - radius, oy, cz - radius, cx + radius, oy + height, cz + radius, palette.Wall, BlockType.Air)
                .SpiralStair(cx, cz, oy + 1, Math.Min(height - 2, 8), Math.Max(1, radius - 1), BlockType.StoneSlab, palette.Pillar)
                .Add(cx, oy + 1, cz - radius, BlockType.Air)
                .Add(cx, oy + 2, cz - radius, BlockType.Air);
        }

        public static void ShelterCamp(StructureBuilder b, int half, int y, in BiomeStructurePalette palette)
        {
            b.Add(-half + 1, y, half - 1, BlockType.HayBale)
                .Add(-half + 2, y, half - 1, BlockType.HayBale)
                .Add(0, y, half - 1, BlockType.MossCarpet)
                .Add(0, y + 1, half - 1, palette.GlowAccent)
                .Add(-half + 1, y + 1, 0, BlockType.Rope)
                .Add(half - 1, y + 1, 0, BlockType.Rope);
        }

        public static void CottageLiving(StructureBuilder b, int half, int y, in BiomeStructurePalette palette)
        {
            b.Add(-half + 1, y, half - 1, BlockType.StationBench)
                .Add(-half + 2, y, half - 1, BlockType.OakPlank)
                .Add(half - 1, y, half - 1, BlockType.HayBale)
                .Add(half - 2, y, half - 1, BlockType.HayBale)
                .Add(half - 1, y + 1, 0, BlockType.Lantern)
                .Add(-half + 1, y + 1, 0, palette.Accent)
                .Add(0, y, half - 2, BlockType.MossCarpet);
        }

        public static void OutpostSupplies(StructureBuilder b, int half, int y, in BiomeStructurePalette palette)
        {
            b.Add(-half + 1, y, half - 1, BlockType.StationBench)
                .Add(half - 1, y, half - 1, BlockType.HayBale)
                .Add(half - 1, y, half - 2, BlockType.HayBale)
                .Add(0, y + 1, half - 1, palette.GlowAccent)
                .Add(-half + 1, y + 1, 0, BlockType.Rope)
                .Add(half - 1, y + 1, 0, BlockType.Rope);
        }

        public static void WatchPostDetails(StructureBuilder b, int radius, int y, in BiomeStructurePalette palette)
        {
            b.Add(0, y, 0, BlockType.StationBench)
                .Add(-radius + 1, y, radius - 1, BlockType.Rope)
                .Add(radius - 1, y + 1, -radius + 1, palette.GlowAccent)
                .Add(-radius + 1, y + 1, -radius + 1, palette.GlowAccent);
        }

        public static void HearthCorner(StructureBuilder b, int x, int y, int z, in BiomeStructurePalette palette)
        {
            b.Add(x, y, z, BlockType.StoneSlab)
                .Add(x, y + 1, z, palette.GlowAccent)
                .Add(x, y + 2, z, BlockType.Lantern);
        }

        public static void StampRandomRoom(StructureBuilder b, StructureRng rng, BiomeStructurePalette p, int ox, int oy, int oz)
        {
            switch (rng.NextInt(7))
            {
                case 0:
                    Barracks(b, ox, oy, oz, p, rng);
                    break;
                case 1:
                    Treasury(b, ox, oy, oz, p, rng);
                    break;
                case 2:
                    Chapel(b, ox, oy, oz, p, rng);
                    break;
                case 3:
                    Hall(b, ox, oy, oz, rng.Pick(5, 7, 9), p);
                    break;
                case 4:
                    Armory(b, ox, oy, oz, p, rng);
                    break;
                case 5:
                    ThroneRoom(b, ox, oy, oz, p, rng);
                    break;
                default:
                    Alchemist(b, ox, oy, oz, p, rng);
                    break;
            }
        }
    }
}

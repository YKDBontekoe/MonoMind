using System.Numerics;

namespace Autonocraft.World
{
    public static class BlockParticleColors
    {
        public static Vector3 GetColor(BlockType type)
        {
            return type switch
            {
                BlockType.Grass => new Vector3(0.40f, 0.66f, 0.26f),
                BlockType.Dirt => new Vector3(0.55f, 0.38f, 0.22f),
                BlockType.Sand => new Vector3(0.86f, 0.78f, 0.52f),
                BlockType.Snow => new Vector3(0.92f, 0.94f, 0.98f),
                BlockType.Stone => new Vector3(0.55f, 0.55f, 0.58f),
                BlockType.Gravel => new Vector3(0.62f, 0.60f, 0.58f),
                BlockType.Water => new Vector3(0.30f, 0.55f, 0.92f),
                BlockType.OakLeaves => new Vector3(0.32f, 0.62f, 0.22f),
                BlockType.BirchLeaves => new Vector3(0.42f, 0.68f, 0.30f),
                BlockType.PineLeaves => new Vector3(0.28f, 0.52f, 0.24f),
                BlockType.CherryLeaves => new Vector3(0.88f, 0.42f, 0.52f),
                BlockType.MahoganyLeaves => new Vector3(0.30f, 0.58f, 0.26f),
                BlockType.MapleLeaves => new Vector3(0.82f, 0.38f, 0.22f),
                BlockType.OakLog or BlockType.BirchLog or BlockType.PineLog
                    or BlockType.WillowLog or BlockType.PalmLog => new Vector3(0.52f, 0.38f, 0.24f),
                BlockType.WillowLeaves => new Vector3(0.38f, 0.58f, 0.42f),
                BlockType.PalmLeaves => new Vector3(0.42f, 0.68f, 0.32f),
                BlockType.CherryLog or BlockType.MahoganyLog or BlockType.MapleLog => new Vector3(0.52f, 0.38f, 0.24f),
                BlockType.BirchPlank => new Vector3(0.78f, 0.74f, 0.66f),
                BlockType.PinePlank => new Vector3(0.62f, 0.48f, 0.28f),
                BlockType.Cobblestone => new Vector3(0.48f, 0.48f, 0.52f),
                BlockType.Brick => new Vector3(0.72f, 0.38f, 0.28f),
                BlockType.MossStone => new Vector3(0.48f, 0.55f, 0.42f),
                BlockType.Mud => new Vector3(0.42f, 0.32f, 0.22f),
                BlockType.Reed => new Vector3(0.38f, 0.72f, 0.42f),
                BlockType.Sunflower => new Vector3(0.92f, 0.78f, 0.28f),
                BlockType.HayBale => new Vector3(0.82f, 0.68f, 0.28f),
                BlockType.Wheat or BlockType.WheatSprout => new Vector3(0.78f, 0.72f, 0.28f),
                BlockType.Carrot or BlockType.CarrotSprout => new Vector3(0.88f, 0.52f, 0.22f),
                BlockType.Ice => new Vector3(0.72f, 0.88f, 0.96f),
                BlockType.CoalOre => new Vector3(0.28f, 0.28f, 0.30f),
                BlockType.IronOre => new Vector3(0.62f, 0.52f, 0.46f),
                BlockType.GoldOre => new Vector3(0.88f, 0.72f, 0.28f),
                BlockType.OakPlank => new Vector3(0.72f, 0.54f, 0.32f),
                BlockType.Glass => new Vector3(0.72f, 0.88f, 0.96f),
                BlockType.Clay => new Vector3(0.68f, 0.48f, 0.38f),
                BlockType.Sandstone => new Vector3(0.82f, 0.72f, 0.48f),
                BlockType.IronBlock => new Vector3(0.72f, 0.74f, 0.78f),
                BlockType.GoldBlock => new Vector3(0.92f, 0.78f, 0.22f),
                BlockType.StationBench or BlockType.StationForge or BlockType.StationCrucible => new Vector3(0.62f, 0.48f, 0.32f),
                BlockType.Cactus => new Vector3(0.38f, 0.62f, 0.28f),
                BlockType.TallGrass => new Vector3(0.48f, 0.72f, 0.32f),
                BlockType.Flower => new Vector3(0.92f, 0.42f, 0.58f),
                BlockType.Fern => new Vector3(0.38f, 0.68f, 0.28f),
                BlockType.MushroomRed => new Vector3(0.88f, 0.28f, 0.28f),
                BlockType.MushroomBrown => new Vector3(0.58f, 0.42f, 0.32f),
                BlockType.DeadBush => new Vector3(0.68f, 0.58f, 0.42f),
                BlockType.Poppy => new Vector3(0.88f, 0.32f, 0.28f),
                BlockType.Daisy => new Vector3(0.92f, 0.90f, 0.72f),
                BlockType.BlueFlax => new Vector3(0.42f, 0.58f, 0.92f),
                BlockType.Tulip => new Vector3(0.92f, 0.48f, 0.28f),
                BlockType.WildRose => new Vector3(0.90f, 0.42f, 0.58f),
                BlockType.MossCarpet => new Vector3(0.36f, 0.62f, 0.30f),
                BlockType.Lichen => new Vector3(0.58f, 0.62f, 0.52f),
                BlockType.Lavender => new Vector3(0.62f, 0.42f, 0.82f),
                BlockType.LilyPad => new Vector3(0.32f, 0.68f, 0.28f),
                BlockType.Vine => new Vector3(0.36f, 0.66f, 0.26f),
                BlockType.BerryBush => new Vector3(0.38f, 0.68f, 0.28f),
                BlockType.Seagrass => new Vector3(0.28f, 0.62f, 0.52f),
                _ => new Vector3(0.75f, 0.72f, 0.68f)
            };
        }

        public static Vector3 Vary(Vector3 baseColor, Random rng, float amount = 0.12f)
        {
            float r = 1f + ((float)rng.NextDouble() * 2f - 1f) * amount;
            return new Vector3(
                Math.Clamp(baseColor.X * r, 0f, 1f),
                Math.Clamp(baseColor.Y * r, 0f, 1f),
                Math.Clamp(baseColor.Z * r, 0f, 1f));
        }
    }
}

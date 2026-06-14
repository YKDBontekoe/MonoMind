using Autonocraft.Domain.World;

namespace Autonocraft.Domain.Crafting
{
    [Flags]
    public enum MaterialTag
    {
        None = 0,
        Wood = 1 << 0,
        Earth = 1 << 1,
        Ore = 1 << 2,
        Organic = 1 << 3,
        Fuel = 1 << 4,
        Stone = 1 << 5
    }

    public static class MaterialTagExtensions
    {
        public static MaterialTag GetTags(this BlockType type)
        {
            return type switch
            {
                BlockType.OakLog or BlockType.BirchLog or BlockType.PineLog
                    or BlockType.WillowLog or BlockType.PalmLog
                    or BlockType.OakPlank or BlockType.BirchPlank or BlockType.PinePlank => MaterialTag.Wood,
                BlockType.Dirt or BlockType.Grass or BlockType.Sand or BlockType.Snow or BlockType.Gravel
                    or BlockType.Clay or BlockType.Mud or BlockType.HayBale or BlockType.Ice => MaterialTag.Earth,
                BlockType.OakLeaves or BlockType.BirchLeaves or BlockType.PineLeaves
                    or BlockType.WillowLeaves or BlockType.PalmLeaves
                    or BlockType.TallGrass or BlockType.Flower or BlockType.Reed
                    or BlockType.Sunflower or BlockType.Cactus => MaterialTag.Organic,
                BlockType.CoalOre => MaterialTag.Fuel | MaterialTag.Ore,
                BlockType.IronOre or BlockType.GoldOre or BlockType.IronBlock or BlockType.GoldBlock => MaterialTag.Ore,
                BlockType.Stone or BlockType.Sandstone or BlockType.Cobblestone
                    or BlockType.Brick or BlockType.MossStone => MaterialTag.Stone,
                _ => MaterialTag.None
            };
        }

        public static bool IsAnyLog(this BlockType type)
        {
            return type is BlockType.OakLog or BlockType.BirchLog or BlockType.PineLog
                or BlockType.WillowLog or BlockType.PalmLog;
        }

        public static bool IsAnyLeaves(this BlockType type)
        {
            return type is BlockType.OakLeaves or BlockType.BirchLeaves or BlockType.PineLeaves
                or BlockType.WillowLeaves or BlockType.PalmLeaves;
        }

        public static bool MatchesTag(this BlockType type, MaterialTag tag)
        {
            if (tag == MaterialTag.None)
            {
                return false;
            }

            return (type.GetTags() & tag) == tag;
        }
    }
}

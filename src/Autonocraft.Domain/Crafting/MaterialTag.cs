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
            if (type.IsWoodMaterial())
            {
                return MaterialTag.Wood;
            }

            if (type.IsLeaf() || type.IsDecorativeFlora())
            {
                return MaterialTag.Organic;
            }

            return type switch
            {
                BlockType.Dirt or BlockType.Grass or BlockType.Sand or BlockType.RedSand or BlockType.Snow or BlockType.Gravel
                    or BlockType.Clay or BlockType.Mud or BlockType.HayBale or BlockType.Ice
                    or BlockType.Quicksand
                    or BlockType.SnowLayer1 or BlockType.SnowLayer2 or BlockType.SnowLayer3 or BlockType.SnowLayer4
                    or BlockType.SnowLayer6 or BlockType.SnowLayer7 or BlockType.SnowLayer8 or BlockType.SnowLayer9 => MaterialTag.Earth,
                BlockType.Moss => MaterialTag.Organic,
                BlockType.CoalOre => MaterialTag.Fuel | MaterialTag.Ore,
                BlockType.IronOre or BlockType.GoldOre or BlockType.IronBlock or BlockType.GoldBlock
                    or BlockType.CopperOre or BlockType.CopperBlock or BlockType.SilverOre or BlockType.SilverBlock
                    or BlockType.RubyOre or BlockType.RubyBlock or BlockType.EmeraldOre or BlockType.EmeraldBlock
                    or BlockType.DiamondOre or BlockType.DiamondBlock or BlockType.QuartzOre or BlockType.QuartzBlock => MaterialTag.Ore,
                BlockType.Stone or BlockType.Sandstone or BlockType.Cobblestone
                    or BlockType.Brick or BlockType.MossStone or BlockType.Marble or BlockType.Basalt or BlockType.Slate
                    or BlockType.Limestone or BlockType.Granite or BlockType.PolishedMarble or BlockType.PolishedGranite
                    or BlockType.Obsidian or BlockType.Amethyst or BlockType.MagmaBlock or BlockType.Dripstone
                    or BlockType.MarbleBrick or BlockType.BasaltBrick or BlockType.SlateBrick => MaterialTag.Stone,
                _ => MaterialTag.None
            };
        }

        public static bool IsAnyLog(this BlockType type)
        {
            return type.IsLog();
        }

        public static bool IsAnyLeaves(this BlockType type)
        {
            return type.IsLeaf();
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

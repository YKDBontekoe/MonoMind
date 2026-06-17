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
                    or BlockType.CherryLog or BlockType.MahoganyLog or BlockType.MapleLog
                    or BlockType.OakPlank or BlockType.BirchPlank or BlockType.PinePlank
                    or BlockType.CherryPlank or BlockType.MahoganyPlank or BlockType.MaplePlank
                    or BlockType.Bamboo => MaterialTag.Wood,
                BlockType.Dirt or BlockType.Grass or BlockType.Sand or BlockType.Snow or BlockType.Gravel
                    or BlockType.Clay or BlockType.Mud or BlockType.HayBale or BlockType.Ice
                    or BlockType.Quicksand => MaterialTag.Earth,
                BlockType.OakLeaves or BlockType.BirchLeaves or BlockType.PineLeaves
                    or BlockType.WillowLeaves or BlockType.PalmLeaves
                    or BlockType.CherryLeaves or BlockType.MahoganyLeaves or BlockType.MapleLeaves
                    or BlockType.TallGrass or BlockType.Flower or BlockType.Reed
                    or BlockType.Sunflower or BlockType.Cactus or BlockType.Fern
                    or BlockType.MushroomRed or BlockType.MushroomBrown or BlockType.DeadBush
                    or BlockType.LilyPad or BlockType.Vine or BlockType.BerryBush
                    or BlockType.Seagrass or BlockType.Glowshroom or BlockType.Lavender
                    or BlockType.Rope or BlockType.Kelp or BlockType.Shrub or BlockType.Heather
                    or BlockType.Juniper or BlockType.Moss or BlockType.Poppy or BlockType.Daisy
                    or BlockType.BlueFlax or BlockType.Tulip or BlockType.WildRose
                    or BlockType.MossCarpet or BlockType.Lichen => MaterialTag.Organic,
                BlockType.CoalOre => MaterialTag.Fuel | MaterialTag.Ore,
                BlockType.IronOre or BlockType.GoldOre or BlockType.IronBlock or BlockType.GoldBlock
                    or BlockType.CopperOre or BlockType.CopperBlock or BlockType.SilverOre or BlockType.SilverBlock
                    or BlockType.RubyOre or BlockType.RubyBlock or BlockType.EmeraldOre or BlockType.EmeraldBlock
                    or BlockType.DiamondOre or BlockType.DiamondBlock or BlockType.QuartzOre or BlockType.QuartzBlock => MaterialTag.Ore,
                BlockType.Stone or BlockType.Sandstone or BlockType.Cobblestone
                    or BlockType.Brick or BlockType.MossStone or BlockType.Marble or BlockType.Basalt or BlockType.Slate
                    or BlockType.Limestone or BlockType.Granite or BlockType.PolishedMarble or BlockType.PolishedGranite
                    or BlockType.Obsidian or BlockType.Amethyst or BlockType.MagmaBlock
                    or BlockType.MarbleBrick or BlockType.BasaltBrick or BlockType.SlateBrick => MaterialTag.Stone,
                _ => MaterialTag.None
            };
        }

        public static bool IsAnyLog(this BlockType type)
        {
            return type is BlockType.OakLog or BlockType.BirchLog or BlockType.PineLog
                or BlockType.WillowLog or BlockType.PalmLog
                or BlockType.CherryLog or BlockType.MahoganyLog or BlockType.MapleLog;
        }

        public static bool IsAnyLeaves(this BlockType type)
        {
            return type is BlockType.OakLeaves or BlockType.BirchLeaves or BlockType.PineLeaves
                or BlockType.WillowLeaves or BlockType.PalmLeaves
                or BlockType.CherryLeaves or BlockType.MahoganyLeaves or BlockType.MapleLeaves;
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

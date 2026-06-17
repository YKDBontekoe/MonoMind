
namespace Autonocraft.Items
{
    public enum BlockHarvestCategory
    {
        None,
        Stone,
        Wood,
        Earth
    }

    public static class BlockHarvestCategoryExtensions
    {
        public static BlockHarvestCategory GetHarvestCategory(this BlockType type)
        {
            return type switch
            {
                BlockType.Air or BlockType.Water or BlockType.Lava => BlockHarvestCategory.None,
                BlockType.OakLog or BlockType.BirchLog or BlockType.PineLog
                    or BlockType.WillowLog or BlockType.PalmLog
                    or BlockType.CherryLog or BlockType.MahoganyLog or BlockType.MapleLog
                    or BlockType.OakLeaves or BlockType.BirchLeaves or BlockType.PineLeaves
                    or BlockType.WillowLeaves or BlockType.PalmLeaves
                    or BlockType.CherryLeaves or BlockType.MahoganyLeaves or BlockType.MapleLeaves
                    or BlockType.OakPlank or BlockType.BirchPlank or BlockType.PinePlank
                    or BlockType.CherryPlank or BlockType.MahoganyPlank or BlockType.MaplePlank
                    or BlockType.Bamboo => BlockHarvestCategory.Wood,
                BlockType.Dirt or BlockType.Grass or BlockType.Sand or BlockType.Snow
                    or BlockType.Gravel or BlockType.Clay or BlockType.TallGrass
                    or BlockType.Flower or BlockType.Reed or BlockType.Sunflower
                    or BlockType.Mud or BlockType.HayBale or BlockType.Ice
                    or BlockType.WheatSprout or BlockType.Wheat
                    or BlockType.CarrotSprout or BlockType.Carrot
                    or BlockType.Fern or BlockType.MushroomRed or BlockType.MushroomBrown
                    or BlockType.DeadBush or BlockType.LilyPad or BlockType.Vine
                    or BlockType.BerryBush or BlockType.Seagrass
                    or BlockType.Glowshroom or BlockType.Lavender or BlockType.Rope
                    or BlockType.Kelp => BlockHarvestCategory.Earth,
                BlockType.Cobblestone or BlockType.Brick or BlockType.MossStone
                    or BlockType.Marble or BlockType.Basalt or BlockType.Slate
                    or BlockType.Limestone or BlockType.Granite or BlockType.Obsidian
                    or BlockType.Amethyst or BlockType.MagmaBlock or BlockType.Quicksand
                    or BlockType.MarbleBrick or BlockType.BasaltBrick or BlockType.SlateBrick
                    or BlockType.PolishedMarble or BlockType.PolishedGranite => BlockHarvestCategory.Stone,
                _ => BlockHarvestCategory.Stone
            };
        }

        public static ToolType? GetPreferredTool(this BlockHarvestCategory category)
        {
            return category switch
            {
                BlockHarvestCategory.Stone => ToolType.Pickaxe,
                BlockHarvestCategory.Wood => ToolType.Axe,
                BlockHarvestCategory.Earth => ToolType.Shovel,
                _ => null
            };
        }
    }
}

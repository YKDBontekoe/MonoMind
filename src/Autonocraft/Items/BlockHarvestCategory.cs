using Autonocraft.World;

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
                BlockType.Air or BlockType.Water => BlockHarvestCategory.None,
                BlockType.OakLog or BlockType.BirchLog or BlockType.PineLog
                    or BlockType.WillowLog or BlockType.PalmLog
                    or BlockType.OakLeaves or BlockType.BirchLeaves or BlockType.PineLeaves
                    or BlockType.WillowLeaves or BlockType.PalmLeaves
                    or BlockType.OakPlank or BlockType.BirchPlank or BlockType.PinePlank => BlockHarvestCategory.Wood,
                BlockType.Dirt or BlockType.Grass or BlockType.Sand or BlockType.Snow
                    or BlockType.Gravel or BlockType.Clay or BlockType.TallGrass
                    or BlockType.Flower or BlockType.Reed or BlockType.Sunflower
                    or BlockType.Mud or BlockType.HayBale or BlockType.Ice
                    or BlockType.WheatSprout or BlockType.Wheat
                    or BlockType.CarrotSprout or BlockType.Carrot
                    or BlockType.Fern or BlockType.MushroomRed or BlockType.MushroomBrown
                    or BlockType.DeadBush or BlockType.LilyPad or BlockType.Vine
                    or BlockType.BerryBush or BlockType.Seagrass => BlockHarvestCategory.Earth,
                BlockType.Cobblestone or BlockType.Brick or BlockType.MossStone => BlockHarvestCategory.Stone,
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

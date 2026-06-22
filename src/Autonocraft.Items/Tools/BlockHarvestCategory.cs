
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
            if (type.IsWoodMaterial() || type.IsLeaf() || type == BlockType.BerryBush)
            {
                return BlockHarvestCategory.Wood;
            }

            if (type.IsDecorativeFlora() || type.IsCrop())
            {
                return BlockHarvestCategory.Earth;
            }

            return type switch
            {
                BlockType.Air or BlockType.Water or BlockType.Lava => BlockHarvestCategory.None,
                BlockType.Dirt or BlockType.Grass or BlockType.Sand or BlockType.RedSand or BlockType.Snow
                    or BlockType.Gravel or BlockType.Clay or BlockType.Mud or BlockType.HayBale or BlockType.Ice
                    or BlockType.SnowLayer1 or BlockType.SnowLayer2 or BlockType.SnowLayer3 or BlockType.SnowLayer4
                    or BlockType.SnowLayer6 or BlockType.SnowLayer7 or BlockType.SnowLayer8 or BlockType.SnowLayer9 => BlockHarvestCategory.Earth,
                BlockType.Cobblestone or BlockType.Brick or BlockType.MossStone or BlockType.Dripstone
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

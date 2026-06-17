using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Crafting
{
    public static class RecipeDiscovery
    {
        public static void OnItemAcquired(DiscoveryJournal journal, ItemStack item)
        {
            if (item.IsBlock())
            {
                OnBlockAcquired(journal, item.BlockType);
                return;
            }

            if (item.IsMaterial() && item.MaterialId == ItemId.Stick)
            {
                journal.Unlock("recipe:sticks");
            }
        }

        public static void OnBlockAcquired(DiscoveryJournal journal, BlockType blockType)
        {
            if (blockType.IsAnyLog())
            {
                journal.Unlock("recipe:plank");
                journal.Unlock("recipe:birch_plank");
                journal.Unlock("recipe:pine_plank");
                journal.Unlock("recipe:cherry_plank");
                journal.Unlock("recipe:mahogany_plank");
                journal.Unlock("recipe:maple_plank");
            }

            if (blockType is BlockType.OakPlank or BlockType.BirchPlank or BlockType.PinePlank
                or BlockType.CherryPlank or BlockType.MahoganyPlank or BlockType.MaplePlank)
            {
                journal.Unlock("recipe:sticks");
            }

            if (blockType == BlockType.Stone || blockType == BlockType.Cobblestone)
            {
                journal.Unlock("recipe:cobblestone");
            }

            if (blockType == BlockType.IronBlock)
            {
                journal.Unlock("recipe:iron_block");
            }

            if (blockType == BlockType.GoldBlock)
            {
                journal.Unlock("recipe:gold_block");
            }

            if (blockType == BlockType.CopperBlock)
            {
                journal.Unlock("recipe:copper_block");
            }

            if (blockType == BlockType.SilverBlock)
            {
                journal.Unlock("recipe:silver_block");
            }

            if (blockType == BlockType.DiamondBlock)
            {
                journal.Unlock("recipe:diamond_block");
            }

            if (blockType == BlockType.EmeraldBlock)
            {
                journal.Unlock("recipe:emerald_block");
            }
        }

        public static void UnlockRelated(DiscoveryJournal journal, string recipeId)
        {
            switch (recipeId)
            {
                case "recipe:plank":
                case "recipe:birch_plank":
                case "recipe:pine_plank":
                case "recipe:cherry_plank":
                case "recipe:mahogany_plank":
                case "recipe:maple_plank":
                    journal.Unlock("recipe:sticks");
                    break;
                case "recipe:sticks":
                    journal.Unlock("recipe:wood_pickaxe");
                    journal.Unlock("recipe:wood_axe");
                    journal.Unlock("recipe:wood_shovel");
                    journal.Unlock("recipe:wood_sword");
                    break;
                case "recipe:wood_pickaxe":
                case "recipe:wood_axe":
                case "recipe:wood_shovel":
                case "recipe:wood_sword":
                    journal.Unlock("recipe:stone_pickaxe");
                    journal.Unlock("recipe:stone_axe");
                    journal.Unlock("recipe:stone_shovel");
                    journal.Unlock("recipe:stone_sword");
                    break;
                case "recipe:iron_block":
                    journal.Unlock("recipe:iron_pickaxe");
                    journal.Unlock("recipe:iron_axe");
                    journal.Unlock("recipe:iron_shovel");
                    journal.Unlock("recipe:iron_sword");
                    break;
                case "recipe:gold_block":
                    journal.Unlock("recipe:gold_pickaxe");
                    journal.Unlock("recipe:gold_axe");
                    journal.Unlock("recipe:gold_shovel");
                    journal.Unlock("recipe:gold_sword");
                    break;
                case "recipe:copper_block":
                    journal.Unlock("recipe:copper_pickaxe");
                    journal.Unlock("recipe:copper_axe");
                    journal.Unlock("recipe:copper_shovel");
                    journal.Unlock("recipe:copper_sword");
                    break;
                case "recipe:silver_block":
                    journal.Unlock("recipe:silver_pickaxe");
                    journal.Unlock("recipe:silver_axe");
                    journal.Unlock("recipe:silver_shovel");
                    journal.Unlock("recipe:silver_sword");
                    break;
                case "recipe:diamond_block":
                    journal.Unlock("recipe:diamond_pickaxe");
                    journal.Unlock("recipe:diamond_axe");
                    journal.Unlock("recipe:diamond_shovel");
                    journal.Unlock("recipe:diamond_sword");
                    break;
                case "recipe:emerald_block":
                    journal.Unlock("recipe:emerald_pickaxe");
                    journal.Unlock("recipe:emerald_axe");
                    journal.Unlock("recipe:emerald_shovel");
                    journal.Unlock("recipe:emerald_sword");
                    break;
            }
        }
    }
}

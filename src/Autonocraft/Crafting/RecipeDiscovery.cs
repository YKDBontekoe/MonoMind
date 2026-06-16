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
            }

            if (blockType is BlockType.OakPlank or BlockType.BirchPlank or BlockType.PinePlank)
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
        }

        public static void UnlockRelated(DiscoveryJournal journal, string recipeId)
        {
            switch (recipeId)
            {
                case "recipe:plank":
                case "recipe:birch_plank":
                case "recipe:pine_plank":
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
            }
        }
    }
}

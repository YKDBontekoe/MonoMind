using Autonocraft.Crafting;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Core
{
    public static class EarlySurvivalMilestones
    {
        public static void NotifyItemAcquired(Player player, ItemStack item, Action<string> showToast)
        {
            if (player.CreativeMode || player.Stats.EarlyGuideStage >= 5)
            {
                return;
            }

            var stats = player.Stats;
            if (stats.HasGatheredResource)
            {
                return;
            }

            if (!IsGatherableResource(item))
            {
                return;
            }

            stats.HasGatheredResource = true;
            showToast("First resource gathered — keep collecting wood!");
        }

        public static void NotifyCrafted(Player player, CraftRecipe recipe, Action<string> showToast)
        {
            if (player.CreativeMode || player.Stats.EarlyGuideStage >= 5)
            {
                return;
            }

            var stats = player.Stats;

            if (!stats.HasCraftedPlank && recipe.Output is BlockType.OakPlank or BlockType.BirchPlank or BlockType.PinePlank)
            {
                stats.HasCraftedPlank = true;
                showToast("Planks crafted — try sticks next (B for recipe book)!");
            }

            if (!stats.HasCraftedTool && recipe.IsToolOutput)
            {
                stats.HasCraftedTool = true;
                showToast($"Crafted {recipe.DisplayName} — you're getting equipped!");
            }
        }

        public static void NotifyFood(Player player, Action<string> showToast)
        {
            if (player.CreativeMode || player.Stats.EarlyGuideStage >= 5)
            {
                return;
            }

            if (player.Stats.HasSecuredFood)
            {
                return;
            }

            player.Stats.HasSecuredFood = true;
            showToast("Food secured — hunger won't be a surprise tonight.");
        }

        private static bool IsGatherableResource(ItemStack item)
        {
            if (item.IsBlock() && item.BlockType.IsAnyLog())
            {
                return true;
            }

            if (item.IsMaterial() && item.MaterialId == ItemId.Stick)
            {
                return true;
            }

            if (item.IsFood())
            {
                return true;
            }

            return item.IsBlock() && item.BlockType != BlockType.Air;
        }
    }
}

using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Crafting
{
    public static class RecipeInventoryExtensions
    {
        public static bool CanAffordRecipe(this IItemContainer container, CraftRecipe recipe)
        {
            foreach (var input in recipe.Inputs)
            {
                if (CountMatching(container, input) < input.Count)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryConsumeRecipeInputs(this IItemContainer container, CraftRecipe recipe)
        {
            if (!container.CanAffordRecipe(recipe))
            {
                return false;
            }

            foreach (var input in recipe.Inputs)
            {
                if (!TryConsumeCraftInput(container, input))
                {
                    return false;
                }
            }

            return true;
        }

        private static int CountMatching(IItemContainer container, CraftInput input)
        {
            int total = 0;
            for (int i = 0; i < container.SlotCount; i++)
            {
                var stack = container.GetSlot(i);
                if (stack.IsBlock() && input.Matches(stack.BlockType))
                {
                    total += stack.Count;
                }
            }

            return total;
        }

        private static bool TryConsumeCraftInput(IItemContainer container, CraftInput input)
        {
            int remaining = input.Count;
            for (int i = 0; i < container.SlotCount && remaining > 0; i++)
            {
                var stack = container.GetSlot(i);
                if (!stack.IsBlock() || !input.Matches(stack.BlockType))
                {
                    continue;
                }

                int take = Math.Min(stack.Count, remaining);
                stack.Count -= take;
                remaining -= take;
                if (stack.Count <= 0)
                {
                    stack = ItemStack.Empty;
                }

                container.SetSlot(i, stack);
            }

            return remaining == 0;
        }
    }
}

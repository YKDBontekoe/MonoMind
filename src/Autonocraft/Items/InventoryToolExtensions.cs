using Autonocraft.Crafting;
using Autonocraft.World;

namespace Autonocraft.Items
{
    public static class InventoryToolExtensions
    {
        public static int CountTools(this IItemContainer container, ToolType toolType)
        {
            int count = 0;
            for (int i = 0; i < container.SlotCount; i++)
            {
                var stack = container.GetSlot(i);
                if (!stack.IsTool() || stack.Durability <= 0)
                {
                    continue;
                }

                if (ToolRegistry.TryGet(stack.ToolId, out var def) && def.ToolType == toolType)
                {
                    count += stack.Count;
                }
            }

            return count;
        }

        public static bool TryWithdrawTool(this IItemContainer container, ToolType toolType, out ItemStack tool)
        {
            tool = ItemStack.Empty;
            int bestSlot = -1;
            float bestSpeed = -1f;
            int bestDurability = -1;

            for (int i = 0; i < container.SlotCount; i++)
            {
                var stack = container.GetSlot(i);
                if (!stack.IsTool() || stack.Durability <= 0)
                {
                    continue;
                }

                if (!ToolRegistry.TryGet(stack.ToolId, out var def) || def.ToolType != toolType)
                {
                    continue;
                }

                if (def.MiningSpeedMultiplier > bestSpeed ||
                    (def.MiningSpeedMultiplier == bestSpeed && stack.Durability > bestDurability))
                {
                    bestSlot = i;
                    bestSpeed = def.MiningSpeedMultiplier;
                    bestDurability = stack.Durability;
                }
            }

            if (bestSlot < 0)
            {
                return false;
            }

            tool = container.GetSlot(bestSlot);
            container.SetSlot(bestSlot, ItemStack.Empty);
            return true;
        }

        public static bool TryReturnTool(this IItemContainer container, ItemStack tool)
        {
            if (tool.IsEmpty || !tool.IsTool())
            {
                return true;
            }

            return container.AddItem(tool);
        }

        public static bool TryFindDamagedTool(this IItemContainer container, ToolType toolType, out int slotIndex)
        {
            slotIndex = -1;
            int lowestDurability = int.MaxValue;

            for (int i = 0; i < container.SlotCount; i++)
            {
                var stack = container.GetSlot(i);
                if (!stack.IsTool() || stack.Durability <= 0 || stack.Durability >= stack.MaxDurability)
                {
                    continue;
                }

                if (!ToolRegistry.TryGet(stack.ToolId, out var def) || def.ToolType != toolType)
                {
                    continue;
                }

                if (stack.Durability < lowestDurability)
                {
                    lowestDurability = stack.Durability;
                    slotIndex = i;
                }
            }

            return slotIndex >= 0;
        }

        public static bool TryRepairTool(this IItemContainer container, int slotIndex, int repairAmount)
        {
            if (slotIndex < 0 || slotIndex >= container.SlotCount)
            {
                return false;
            }

            var stack = container.GetSlot(slotIndex);
            if (!stack.IsTool() || stack.Durability >= stack.MaxDurability)
            {
                return false;
            }

            stack.Durability = Math.Min(stack.MaxDurability, stack.Durability + repairAmount);
            container.SetSlot(slotIndex, stack);
            return true;
        }

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

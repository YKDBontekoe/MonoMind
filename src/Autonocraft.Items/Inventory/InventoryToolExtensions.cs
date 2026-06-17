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
    }
}

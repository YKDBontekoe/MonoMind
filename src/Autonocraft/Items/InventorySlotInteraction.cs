namespace Autonocraft.Items
{
    /// <summary>Minecraft-style inventory slot click handling with a held cursor stack.</summary>
    public static class InventorySlotInteraction
    {
        public static void HandleLeftClick(ref ItemStack cursor, ref ItemStack slot)
        {
            if (cursor.IsEmpty && slot.IsEmpty)
            {
                return;
            }

            if (cursor.IsEmpty)
            {
                cursor = slot;
                slot = ItemStack.Empty;
                return;
            }

            if (slot.IsEmpty)
            {
                slot = cursor;
                cursor = ItemStack.Empty;
                return;
            }

            if (cursor.CanStackWith(slot) && slot.Count < Inventory.DefaultStackSize)
            {
                int move = Math.Min(cursor.Count, Inventory.DefaultStackSize - slot.Count);
                slot.Count += move;
                cursor.Count -= move;
                if (cursor.Count <= 0)
                {
                    cursor = ItemStack.Empty;
                }

                return;
            }

            (cursor, slot) = (slot, cursor);
        }

        public static void HandleRightClick(ref ItemStack cursor, ref ItemStack slot)
        {
            if (!cursor.IsEmpty)
            {
                if (slot.IsEmpty)
                {
                    slot = CloneSingle(cursor);
                    cursor.Count--;
                    if (cursor.Count <= 0)
                    {
                        cursor = ItemStack.Empty;
                    }

                    return;
                }

                if (cursor.CanStackWith(slot) && slot.Count < Inventory.DefaultStackSize)
                {
                    slot.Count++;
                    cursor.Count--;
                    if (cursor.Count <= 0)
                    {
                        cursor = ItemStack.Empty;
                    }
                }

                return;
            }

            if (slot.IsEmpty)
            {
                return;
            }

            if (slot.Count == 1)
            {
                cursor = slot;
                slot = ItemStack.Empty;
                return;
            }

            int half = (slot.Count + 1) / 2;
            cursor = CloneCount(slot, half);
            slot.Count -= half;
        }

        private static ItemStack CloneSingle(ItemStack source)
        {
            if (source.IsBlock())
            {
                return ItemStack.CreateBlock(source.BlockType, 1);
            }

            if (source.IsFood())
            {
                return ItemStack.CreateFood(source.FoodId, 1);
            }

            if (source.IsMaterial())
            {
                return ItemStack.CreateMaterial(source.MaterialId, 1);
            }

            if (source.IsTool())
            {
                return ItemStack.CreateTool(source.ToolId, source.Durability);
            }

            if (source.IsFluidContainer())
            {
                return ItemStack.CreateFluidContainer(source.ToolId);
            }

            return ItemStack.Empty;
        }

        private static ItemStack CloneCount(ItemStack source, int count)
        {
            if (source.IsBlock())
            {
                return ItemStack.CreateBlock(source.BlockType, count);
            }

            if (source.IsFood())
            {
                return ItemStack.CreateFood(source.FoodId, count);
            }

            if (source.IsMaterial())
            {
                return ItemStack.CreateMaterial(source.MaterialId, count);
            }

            return CloneSingle(source);
        }
    }
}

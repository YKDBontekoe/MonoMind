using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Crafting
{
    /// <summary>Item stacks arranged in a square crafting grid (2×2 or 3×3).</summary>
    public sealed class CraftingGrid
    {
        public const int MaxSize = 3;

        private readonly ItemStack[] _slots = new ItemStack[MaxSize * MaxSize];

        public CraftGridSize Size { get; private set; } = CraftGridSize.TwoByTwo;

        public int SlotCount => (int)Size * (int)Size;

        public void SetSize(CraftGridSize size) => Size = size;

        public ItemStack GetSlot(int index) =>
            index >= 0 && index < SlotCount ? _slots[index] : ItemStack.Empty;

        public void SetSlot(int index, ItemStack stack)
        {
            if (index >= 0 && index < SlotCount)
            {
                _slots[index] = stack;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = ItemStack.Empty;
            }
        }

        public BlockType[] ToBlockTypes()
        {
            var types = new BlockType[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                var slot = _slots[i];
                types[i] = slot.IsBlock() ? slot.BlockType : BlockType.Air;
            }

            return types;
        }

        public ItemStack[] GetItemStacks()
        {
            var stacks = new ItemStack[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                stacks[i] = _slots[i];
            }

            return stacks;
        }

        public void ConsumeSlots(IReadOnlyDictionary<int, int> consumption)
        {
            foreach (var (slotIndex, amount) in consumption)
            {
                if (slotIndex < 0 || slotIndex >= SlotCount || amount <= 0)
                {
                    continue;
                }

                ref var slot = ref _slots[slotIndex];
                if (slot.IsEmpty)
                {
                    continue;
                }

                slot.Count -= amount;
                if (slot.Count <= 0)
                {
                    slot = ItemStack.Empty;
                }
            }
        }

        public bool DepositFromCursor(ref ItemStack cursor, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount || cursor.IsEmpty)
            {
                return false;
            }

            if (!cursor.IsBlock() && !cursor.IsMaterial())
            {
                return false;
            }

            ref var target = ref _slots[slotIndex];
            if (target.IsEmpty)
            {
                target = CloneSingle(cursor);
                cursor.Count--;
                if (cursor.Count <= 0)
                {
                    cursor = ItemStack.Empty;
                }

                return true;
            }

            if (cursor.CanStackWith(target) && target.Count < Inventory.DefaultStackSize)
            {
                target.Count++;
                cursor.Count--;
                if (cursor.Count <= 0)
                {
                    cursor = ItemStack.Empty;
                }

                return true;
            }

            return false;
        }

        private static ItemStack CloneSingle(ItemStack source)
        {
            if (source.IsBlock())
            {
                return ItemStack.CreateBlock(source.BlockType, 1);
            }

            if (source.IsMaterial())
            {
                return ItemStack.CreateMaterial(source.MaterialId, 1);
            }

            return source;
        }

        public bool WithdrawToCursor(ref ItemStack cursor, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                return false;
            }

            ref var source = ref _slots[slotIndex];
            if (source.IsEmpty)
            {
                return false;
            }

            if (cursor.IsEmpty)
            {
                cursor = source;
                source = ItemStack.Empty;
                return true;
            }

            if (cursor.CanStackWith(source) && cursor.Count < Inventory.DefaultStackSize)
            {
                int move = Math.Min(source.Count, Inventory.DefaultStackSize - cursor.Count);
                cursor.Count += move;
                source.Count -= move;
                if (source.Count <= 0)
                {
                    source = ItemStack.Empty;
                }

                return move > 0;
            }

            return false;
        }

        public void HandleSlotClick(int index, ref ItemStack cursor, bool rightClick)
        {
            if (index < 0 || index >= SlotCount)
            {
                return;
            }

            var slot = GetSlot(index);
            if (rightClick)
            {
                InventorySlotInteraction.HandleRightClick(ref cursor, ref slot);
            }
            else
            {
                InventorySlotInteraction.HandleLeftClick(ref cursor, ref slot);
            }

            SetSlot(index, slot);
        }
    }
}

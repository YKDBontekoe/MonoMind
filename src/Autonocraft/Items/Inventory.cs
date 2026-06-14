using System;
using Autonocraft.World;

namespace Autonocraft.Items
{
    public sealed class Inventory : IItemContainer
    {
        public const int DefaultStackSize = 64;

        private readonly ItemStack[] _slots;
        public Action<string>? OnOverflow { get; set; }

        public Inventory(int slotCount)
        {
            if (slotCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount));
            }

            _slots = new ItemStack[slotCount];
        }

        public int SlotCount => _slots.Length;

        public ItemStack GetSlot(int index) => _slots[index];

        public void SetSlot(int index, ItemStack stack)
        {
            _slots[index] = stack;
        }

        public bool AddItem(ItemStack item)
        {
            if (item.IsEmpty)
            {
                return true;
            }

            if (item.IsBlock())
            {
                return AddBlockStack(item.BlockType, item.Count);
            }

            if (item.IsTool() || item.IsFluidContainer())
            {
                return AddSingleStack(item);
            }

            return false;
        }

        public bool TryConsumeBlock(BlockType blockType, int count)
        {
            if (blockType == BlockType.Air || count <= 0)
            {
                return count == 0;
            }

            int available = CountBlock(blockType);
            if (available < count)
            {
                return false;
            }

            int remaining = count;
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (!_slots[i].IsBlock() || _slots[i].BlockType != blockType)
                {
                    continue;
                }

                int take = Math.Min(_slots[i].Count, remaining);
                _slots[i].Count -= take;
                remaining -= take;
                if (_slots[i].Count <= 0)
                {
                    _slots[i] = ItemStack.Empty;
                }
            }

            return remaining == 0;
        }

        public int CountBlock(BlockType blockType)
        {
            int total = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsBlock() && _slots[i].BlockType == blockType)
                {
                    total += _slots[i].Count;
                }
            }

            return total;
        }

        public bool HasSpaceFor(ItemStack item)
        {
            if (item.IsEmpty)
            {
                return true;
            }

            if (item.IsBlock())
            {
                return CountFreeSpaceForBlock(item.BlockType) >= item.Count;
            }

            return HasEmptySlot();
        }

        private bool AddBlockStack(BlockType blockType, int count)
        {
            if (blockType == BlockType.Air || count <= 0)
            {
                return true;
            }

            int remaining = count;
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (_slots[i].IsBlock() && _slots[i].BlockType == blockType && _slots[i].Count < DefaultStackSize)
                {
                    int add = Math.Min(DefaultStackSize - _slots[i].Count, remaining);
                    _slots[i].Count += add;
                    remaining -= add;
                }
            }

            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    int add = Math.Min(DefaultStackSize, remaining);
                    _slots[i] = ItemStack.CreateBlock(blockType, add);
                    remaining -= add;
                }
            }

            if (remaining > 0)
            {
                OnOverflow?.Invoke($"Inventory full! Lost {remaining}x {blockType}");
                return false;
            }

            return true;
        }

        private bool AddSingleStack(ItemStack item)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].CanStackWith(item))
                {
                    _slots[i].Count++;
                    return true;
                }
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    _slots[i] = item;
                    return true;
                }
            }

            OnOverflow?.Invoke($"Inventory full! Cannot collect {item.GetDisplayName()}");
            return false;
        }

        private int CountFreeSpaceForBlock(BlockType blockType)
        {
            int free = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    free += DefaultStackSize;
                }
                else if (_slots[i].IsBlock() && _slots[i].BlockType == blockType)
                {
                    free += DefaultStackSize - _slots[i].Count;
                }
            }

            return free;
        }

        private bool HasEmptySlot()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

using Autonocraft.Core;
using Autonocraft.World;

namespace Autonocraft.Items
{
    /// <summary>Hotbar + main storage as a single <see cref="IItemContainer"/>.</summary>
    public sealed class PlayerInventoryAdapter : IItemContainer
    {
        private readonly Player _player;

        public PlayerInventoryAdapter(Player player) => _player = player;

        public int SlotCount => Player.StorageSlotCount + _player.Hotbar.Length;

        public ItemStack GetSlot(int index)
        {
            if (index < _player.Hotbar.Length)
            {
                return _player.Hotbar[index];
            }

            return _player.Storage.GetSlot(index - _player.Hotbar.Length);
        }

        public void SetSlot(int index, ItemStack stack)
        {
            if (index < _player.Hotbar.Length)
            {
                _player.Hotbar[index] = stack;
                return;
            }

            _player.Storage.SetSlot(index - _player.Hotbar.Length, stack);
        }

        public bool AddItem(ItemStack item) => _player.AddItem(item);

        public bool TryConsumeBlock(BlockType blockType, int count)
        {
            if (_player.CreativeMode)
            {
                return true;
            }

            int available = CountBlock(blockType);
            if (available < count)
            {
                return false;
            }

            int remaining = count;
            for (int i = 0; i < SlotCount && remaining > 0; i++)
            {
                var slot = GetSlot(i);
                if (!slot.IsBlock() || slot.BlockType != blockType)
                {
                    continue;
                }

                int take = Math.Min(slot.Count, remaining);
                slot.Count -= take;
                remaining -= take;
                if (slot.Count <= 0)
                {
                    slot = ItemStack.Empty;
                }

                SetSlot(i, slot);
            }

            return remaining == 0;
        }

        public int CountBlock(BlockType blockType)
        {
            if (_player.CreativeMode)
            {
                return int.MaxValue / 2;
            }

            int total = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                var slot = GetSlot(i);
                if (slot.IsBlock() && slot.BlockType == blockType)
                {
                    total += slot.Count;
                }
            }

            return total;
        }

        public bool HasSpaceFor(ItemStack item) => _player.HasSpaceFor(item);
    }
}

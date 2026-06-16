using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Village
{
    public sealed class VillageStorage : IItemContainer
    {
        private Inventory _inventory;

        public VillageStorage(int slotCount)
        {
            _inventory = new Inventory(slotCount);
        }

        public int SlotCount => _inventory.SlotCount;
        public Action<string>? OnOverflow
        {
            get => _inventory.OnOverflow;
            set => _inventory.OnOverflow = value;
        }

        public ItemStack GetSlot(int index) => _inventory.GetSlot(index);
        public void SetSlot(int index, ItemStack stack) => _inventory.SetSlot(index, stack);
        public bool AddItem(ItemStack item) => _inventory.AddItem(item);
        public bool TryConsumeBlock(BlockType blockType, int count) => _inventory.TryConsumeBlock(blockType, count);
        public int CountBlock(BlockType blockType) => _inventory.CountBlock(blockType);
        public bool HasSpaceFor(ItemStack item) => _inventory.HasSpaceFor(item);

        public void ExpandSlots(int additionalSlots)
        {
            if (additionalSlots <= 0)
            {
                return;
            }

            var merged = new Inventory(_inventory.SlotCount + additionalSlots);
            for (int i = 0; i < _inventory.SlotCount; i++)
            {
                merged.SetSlot(i, _inventory.GetSlot(i));
            }

            _inventory = merged;
        }
    }
}

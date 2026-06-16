using Autonocraft.World;

namespace Autonocraft.Items
{
    public interface IItemContainer
    {
        int SlotCount { get; }
        ItemStack GetSlot(int index);
        void SetSlot(int index, ItemStack stack);
        bool AddItem(ItemStack item);
        bool TryConsumeBlock(BlockType blockType, int count);
        int CountBlock(BlockType blockType);
        bool HasSpaceFor(ItemStack item);
    }
}

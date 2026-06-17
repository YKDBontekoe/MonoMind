using Autonocraft.Domain.World;

namespace Autonocraft.Items
{
    /// <summary>Player inventory surface for crafting (hotbar + storage + craft stats).</summary>
    public interface ICraftingPlayer : IItemContainer
    {
        int SelectedSlot { get; }
        bool CreativeMode { get; }
        bool TryConsumeFood(ItemId foodId, int count);
        void GiveBlocks(BlockType type, int count);
        void RecordItemCrafted();
        bool TryTakeOneFromHotbar(int hotbarIndex, out ItemStack taken);
    }
}

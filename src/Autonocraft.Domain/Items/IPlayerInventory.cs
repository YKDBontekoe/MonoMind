using Autonocraft.Domain.Crafting;
using Autonocraft.Domain.World;

namespace Autonocraft.Domain.Items
{
    /// <summary>Narrow inventory surface for crafting systems (hotbar + storage).</summary>
    public interface IPlayerInventory
    {
        int SelectedSlot { get; }
        bool CreativeMode { get; }
        bool TryConsumeBlock(BlockType blockType, int count);
        bool TryConsumeMaterial(MaterialTag material, int count);
        bool TryConsumeFood(ItemId foodId, int count);
        bool AddItem(ItemKind kind, BlockType block, ItemId toolId, int count, int durability, int maxDurability);
    }
}

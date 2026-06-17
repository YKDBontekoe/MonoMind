using Autonocraft.Domain.World;

namespace Autonocraft.Items
{
    /// <summary>Hotbar + main storage as a single <see cref="IItemContainer"/>.</summary>
    public sealed class PlayerInventoryAdapter : IItemContainer
    {
        private readonly ICraftingPlayer _player;

        public PlayerInventoryAdapter(ICraftingPlayer player) => _player = player;

        public int SlotCount => _player.SlotCount;

        public ItemStack GetSlot(int index) => _player.GetSlot(index);

        public void SetSlot(int index, ItemStack stack) => _player.SetSlot(index, stack);

        public bool AddItem(ItemStack item) => _player.AddItem(item);

        public bool TryConsumeBlock(BlockType blockType, int count) => _player.TryConsumeBlock(blockType, count);

        public int CountBlock(BlockType blockType) => _player.CountBlock(blockType);

        public bool HasSpaceFor(ItemStack item) => _player.HasSpaceFor(item);
    }
}

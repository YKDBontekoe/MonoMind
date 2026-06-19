using Autonocraft.Items;

namespace Autonocraft.Core
{
    public sealed class ChestSession
    {
        public bool IsOpen { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Z { get; private set; }
        public Inventory? ChestInventory { get; private set; }

        public void Open(int x, int y, int z, Inventory chestInventory)
        {
            IsOpen = true;
            X = x;
            Y = y;
            Z = z;
            ChestInventory = chestInventory;
        }

        public void Close()
        {
            IsOpen = false;
            ChestInventory = null;
        }

        public bool WithdrawToPlayer(Player player, int slot)
        {
            if (ChestInventory == null || slot < 0 || slot >= ChestInventory.SlotCount)
            {
                return false;
            }

            var stack = ChestInventory.GetSlot(slot);
            if (stack.IsEmpty)
            {
                return false;
            }

            if (!player.AddItem(stack))
            {
                return false;
            }

            ChestInventory.SetSlot(slot, ItemStack.Empty);
            return true;
        }

        public bool DepositFromPlayer(Player player, int slot)
        {
            if (ChestInventory == null || slot < 0 || slot >= ChestInventory.SlotCount)
            {
                return false;
            }

            if (ChestInventory.GetSlot(slot) is { IsEmpty: false })
            {
                return false;
            }

            int hotbarSlot = player.SelectedSlot;
            var stack = player.Hotbar[hotbarSlot];
            if (stack.IsEmpty)
            {
                return false;
            }

            ChestInventory.SetSlot(slot, stack);
            player.Hotbar[hotbarSlot] = ItemStack.Empty;
            return true;
        }
    }
}

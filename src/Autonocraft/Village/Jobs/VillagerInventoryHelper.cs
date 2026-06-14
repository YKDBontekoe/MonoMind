using Autonocraft.Entities;
using Autonocraft.Items;

namespace Autonocraft.Village.Jobs
{
    internal static class VillagerInventoryHelper
    {
        public static bool IsInventoryEmpty(Villager villager)
        {
            for (int i = 0; i < villager.Inventory.SlotCount; i++)
            {
                if (!villager.Inventory.GetSlot(i).IsEmpty)
                {
                    return false;
                }
            }

            return true;
        }

        public static void DepositAllToStorage(Villager villager, VillageStorage storage)
        {
            for (int i = 0; i < villager.Inventory.SlotCount; i++)
            {
                var stack = villager.Inventory.GetSlot(i);
                if (stack.IsEmpty)
                {
                    continue;
                }

                if (storage.AddItem(stack))
                {
                    villager.Inventory.SetSlot(i, ItemStack.Empty);
                }
            }
        }
    }
}

using Autonocraft.Items;
using Autonocraft.Village;

namespace Autonocraft.Core
{
    public static class VillageDeposits
    {
        public static bool TryDepositSelectedHotbar(Player player, Village.Village village)
        {
            ref var slot = ref player.Hotbar[player.SelectedSlot];
            if (slot.IsEmpty)
            {
                player.ShowToast?.Invoke("Select a hotbar slot with items to donate.");
                return false;
            }

            if (slot.IsTool() || slot.IsFluidContainer())
            {
                player.ShowToast?.Invoke("Donate blocks, food, or materials — tools stay on you.");
                return false;
            }

            if (TryDepositStack(player, village, ref slot, out string message))
            {
                player.ShowToast?.Invoke(message);
                return true;
            }

            player.ShowToast?.Invoke(message);
            return false;
        }

        public static bool TryDepositAllBlocks(Player player, Village.Village village)
        {
            int stacks = 0;

            for (int i = 0; i < player.Hotbar.Length; i++)
            {
                ref var slot = ref player.Hotbar[i];
                if (!CanDonateBulk(ref slot))
                {
                    continue;
                }

                if (TryDepositStack(player, village, ref slot, out _))
                {
                    stacks++;
                }
            }

            for (int i = 0; i < player.Storage.SlotCount; i++)
            {
                var stack = player.Storage.GetSlot(i);
                if (!CanDonateBulk(ref stack))
                {
                    continue;
                }

                if (TryDepositStack(player, village, ref stack, out _))
                {
                    player.Storage.SetSlot(i, stack);
                    stacks++;
                }
            }

            if (stacks == 0)
            {
                player.ShowToast?.Invoke("No blocks, food, or materials in your inventory to donate.");
                return false;
            }

            player.ShowToast?.Invoke(
                stacks == 1
                    ? "Donated items to village storage."
                    : $"Donated {stacks} stacks to village storage.");
            return true;
        }

        private static bool CanDonate(ref ItemStack stack) =>
            stack.IsBlock() || stack.IsMaterial();

        private static bool CanDonateBulk(ref ItemStack stack) =>
            stack.IsBlock() || stack.IsMaterial() || stack.IsFood();

        private static bool TryDepositStack(Player player, Village.Village village, ref ItemStack stack, out string message)
        {
            if (stack.IsEmpty)
            {
                message = string.Empty;
                return false;
            }

            if (stack.IsFood())
            {
                return TryDepositFood(player, village, ref stack, out message);
            }

            if (!stack.IsBlock() && !stack.IsMaterial())
            {
                message = "That item cannot go into village storage.";
                return false;
            }

            var copy = stack;
            if (!village.Storage.AddItem(copy))
            {
                message = "Village storage is full.";
                return false;
            }

            int donated = copy.Count;
            stack = ItemStack.Empty;
            message = $"Donated {FormatStack(copy, donated)} to village storage.";
            return true;
        }

        private static bool TryDepositFood(Player player, Village.Village village, ref ItemStack stack, out string message)
        {
            int perItem = FoodRegistry.GetHungerRestore(stack.FoodId);
            if (perItem <= 0)
            {
                message = "That food cannot be stored in the settlement.";
                return false;
            }

            float foodValue = perItem * stack.Count * 0.25f;
            village.AddFarmFood(foodValue);
            string label = FoodRegistry.GetDisplayName(stack.FoodId);
            int count = stack.Count;
            stack = ItemStack.Empty;
            message = $"Donated {count}x {label} to settlement food stock (+{foodValue:0.#}).";
            return true;
        }

        private static string FormatStack(ItemStack stack, int count) =>
            stack.IsBlock()
                ? $"{count}x {stack.BlockType}"
                : $"{count}x {stack.GetDisplayName()}";
    }
}

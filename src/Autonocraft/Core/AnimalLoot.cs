using Autonocraft.Items;

namespace Autonocraft.Core
{
    public static class AnimalLoot
    {
        public static void GrantKillLoot(Player player, Entities.AnimalType type)
        {
            switch (type)
            {
                case Entities.AnimalType.Pig:
                    player.AddItem(ItemStack.CreateFood(ItemId.RawMeat, 2));
                    break;
                case Entities.AnimalType.Sheep:
                case Entities.AnimalType.Chicken:
                case Entities.AnimalType.Wolf:
                    player.AddItem(ItemStack.CreateFood(ItemId.RawMeat, 1));
                    break;
            }
        }
    }

    public static class FoodConsumption
    {
        public static bool TryEatFromHotbar(Player player)
        {
            ref var slot = ref player.Hotbar[player.SelectedSlot];
            if (!slot.IsFood())
            {
                return false;
            }

            var foodId = slot.FoodId;
            int restore = FoodRegistry.GetHungerRestore(foodId);
            if (restore <= 0)
            {
                return false;
            }

            string name = FoodRegistry.GetDisplayName(foodId);
            player.RestoreHunger(restore);
            slot.Count--;
            if (slot.Count <= 0)
            {
                slot = ItemStack.Empty;
            }

            player.ShowToast?.Invoke($"Ate {name} (+{restore} hunger)");
            return true;
        }

        public static bool TryTakeRations(Player player, Village.Village village)
        {
            if (player.CreativeMode)
            {
                return false;
            }

            float threshold = SurvivalConstants.MaxHunger * SurvivalConstants.RationHungerThresholdFraction;
            if (player.Hunger >= threshold)
            {
                player.ShowToast?.Invoke("You are not hungry enough for rations.");
                return false;
            }

            if (village.FoodStock < SurvivalConstants.RationFoodCost)
            {
                player.ShowToast?.Invoke("Settlement food stock is empty.");
                return false;
            }

            village.FoodStock -= SurvivalConstants.RationFoodCost;
            player.RestoreHunger(SurvivalConstants.CookedMeatRestore);
            player.ShowToast?.Invoke(
                $"You took rations from the settlement (food stock: {village.FoodStock:0.#}).");
            return true;
        }
    }
}

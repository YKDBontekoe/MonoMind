using Autonocraft.Domain.Items;

namespace Autonocraft.Items
{
    public static class FoodRegistry
    {
        public static float GetHungerRestore(ItemId id) => id switch
        {
            ItemId.Berries => 3f,
            ItemId.RawMeat => 4f,
            ItemId.VillageRation => 5f,
            ItemId.Bread => 6f,
            ItemId.CookedMeat => 8f,
            _ => 0f
        };

        public static string GetDisplayName(ItemId id) => id switch
        {
            ItemId.RawMeat => "Raw Meat",
            ItemId.CookedMeat => "Cooked Meat",
            ItemId.Berries => "Berries",
            ItemId.Bread => "Bread",
            ItemId.VillageRation => "Village Ration",
            _ => id.ToString()
        };

        public static bool IsFood(ItemId id) => GetHungerRestore(id) > 0f;

        public static ItemStack Create(ItemId id, int count = 1)
        {
            return new ItemStack
            {
                Kind = ItemKind.Consumable,
                ToolId = id,
                Count = count
            };
        }
    }
}

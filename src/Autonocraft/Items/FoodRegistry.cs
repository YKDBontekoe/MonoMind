using Autonocraft.Core;

namespace Autonocraft.Items
{
    public static class FoodRegistry
    {
        public static bool TryGet(ItemId id, out FoodDefinition definition)
        {
            definition = id switch
            {
                ItemId.RawMeat => new FoodDefinition("Raw Meat", SurvivalConstants.RawMeatRestore),
                ItemId.CookedMeat => new FoodDefinition("Cooked Meat", SurvivalConstants.CookedMeatRestore),
                ItemId.Bread => new FoodDefinition("Bread", SurvivalConstants.BreadRestore),
                _ => default
            };

            return definition.HungerRestore > 0;
        }

        public static string GetDisplayName(ItemId id) =>
            TryGet(id, out var def) ? def.DisplayName : id.ToString();

        public static int GetHungerRestore(ItemId id) =>
            TryGet(id, out var def) ? def.HungerRestore : 0;
    }

    public readonly struct FoodDefinition
    {
        public string DisplayName { get; }
        public int HungerRestore { get; }

        public FoodDefinition(string displayName, int hungerRestore)
        {
            DisplayName = displayName;
            HungerRestore = hungerRestore;
        }
    }
}

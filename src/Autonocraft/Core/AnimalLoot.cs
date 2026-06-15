using Autonocraft.Domain.Items;
using Autonocraft.Entities;
using Autonocraft.Items;

namespace Autonocraft.Core
{
    public static class AnimalLoot
    {
        public static ItemStack For(AnimalType type)
        {
            return type switch
            {
                AnimalType.Sheep or AnimalType.Pig or AnimalType.Chicken => ItemStack.CreateConsumable(ItemId.RawMeat, 1),
                _ => ItemStack.Empty
            };
        }
    }
}

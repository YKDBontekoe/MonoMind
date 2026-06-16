using Autonocraft.Domain.Items;

namespace Autonocraft.Items
{
    public static class MaterialRegistry
    {
        public static string GetDisplayName(ItemId materialId) => materialId switch
        {
            ItemId.Stick => "Stick",
            _ => materialId.ToString()
        };
    }
}

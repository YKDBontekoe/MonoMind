using System.Numerics;

namespace Autonocraft.Items.Rendering
{
    public interface IItemEntityRenderView
    {
        bool ReadyForRemoval { get; }
        Vector3 Position { get; }
        float HoverTimer { get; }
        float Age { get; }
        ItemStack Item { get; }
    }
}

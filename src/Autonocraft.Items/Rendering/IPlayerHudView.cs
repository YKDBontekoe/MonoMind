using System.Numerics;

namespace Autonocraft.Items.Rendering
{
    /// <summary>Player state surfaced to the in-world HUD renderer.</summary>
    public interface IPlayerHudView
    {
        Vector3 Position { get; }
        float Yaw { get; }
        float Health { get; }
        float MaxHealth { get; }
        float Hunger { get; }
        float MaxHunger { get; }
        float Oxygen { get; }
        bool CreativeMode { get; }
        bool IsGrounded { get; }
        bool HeadUnderwater { get; }
        int SelectedSlot { get; }
        PlayerSkills Skills { get; }
        ItemStack GetHotbarSlot(int index);
        ItemStack GetSelectedStack();
    }
}

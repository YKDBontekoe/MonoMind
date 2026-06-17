using System.Numerics;

namespace Autonocraft.Domain.Rendering
{
    /// <summary>Player motion state for camera / held-item animation.</summary>
    public interface IPlayerMotionView
    {
        Vector3 Velocity { get; }
        bool IsGrounded { get; }
        bool CreativeMode { get; }
    }
}

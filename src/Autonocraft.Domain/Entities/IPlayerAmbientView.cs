using System.Numerics;

namespace Autonocraft.Domain.Entities
{
    /// <summary>Player state needed for ambient audio (position, water).</summary>
    public interface IPlayerAmbientView
    {
        Vector3 Position { get; }
        bool InWater { get; }
    }
}

using System.Numerics;

namespace Autonocraft.Domain.Entities
{
    public interface INightThreatPlayer
    {
        bool CreativeMode { get; }
        bool IsAlive { get; }
        Vector3 Position { get; }
        Action<string>? ShowToast { get; }
    }
}

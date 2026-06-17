using System.Numerics;
using Autonocraft.Domain.World;

namespace Autonocraft.Domain.Rendering
{
    public struct PlacePopEffect
    {
        public Vector3 Position;
        public BlockType BlockType;
        public float Timer;
        public float Duration;
        public bool Active;
    }
}

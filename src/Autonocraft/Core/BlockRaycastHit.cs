using System.Numerics;
using Autonocraft.World;

namespace Autonocraft.Core
{
    public readonly struct BlockRaycastHit
    {
        public static BlockRaycastHit Miss => new BlockRaycastHit(false, default, default, BlockType.Air, float.MaxValue);

        public bool HasHit { get; }
        public Vector3 BlockPos { get; }
        public Vector3 Normal { get; }
        public BlockType BlockType { get; }
        public float Distance { get; }

        public BlockRaycastHit(bool hasHit, Vector3 blockPos, Vector3 normal, BlockType blockType, float distance)
        {
            HasHit = hasHit;
            BlockPos = blockPos;
            Normal = normal;
            BlockType = blockType;
            Distance = distance;
        }
    }
}

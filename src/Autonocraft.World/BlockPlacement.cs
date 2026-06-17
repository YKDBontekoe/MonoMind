using System.Numerics;

namespace Autonocraft.World
{
    public static class BlockPlacement
    {
        public static bool TryPlaceBlock(
            VoxelWorld world,
            int x,
            int y,
            int z,
            BlockType blockType,
            float entityWidth,
            float entityHeight,
            Vector3 entityPosition,
            bool checkEntityCollision = true)
        {
            if (blockType == BlockType.Air)
            {
                return false;
            }

            if (world.GetBlock(x, y, z) != BlockType.Air)
            {
                return false;
            }

            if (checkEntityCollision &&
                entityWidth > 0f &&
                entityHeight > 0f &&
                EntityIntersectsBlock(entityWidth, entityHeight, entityPosition, x, y, z))
            {
                return false;
            }

            world.SetBlock(x, y, z, blockType);
            return true;
        }

        public static bool EntityIntersectsBlock(
            float width,
            float height,
            Vector3 position,
            int x,
            int y,
            int z)
        {
            float minX = position.X - width / 2f;
            float maxX = position.X + width / 2f;
            float minY = position.Y;
            float maxY = position.Y + height;
            float minZ = position.Z - width / 2f;
            float maxZ = position.Z + width / 2f;

            return minX < x + 1 && maxX > x &&
                   minY < y + 1 && maxY > y &&
                   minZ < z + 1 && maxZ > z;
        }
    }
}

using System.Numerics;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Core
{
    public static class BlockActionService
    {
        public const float DefaultReach = BlockInteractionSystem.RaycastRange;

        public static bool TryBreakBlock(
            VoxelWorld world,
            int x,
            int y,
            int z,
            IItemContainer? inventory = null,
            PlayerSkills? skills = null)
        {
            var block = world.GetBlock(x, y, z);
            if (block == BlockType.Air || !block.IsCollidable())
            {
                return false;
            }

            world.SetBlock(x, y, z, BlockType.Air);
            inventory?.AddItem(ItemStack.CreateBlock(block, 1));
            skills?.AddXp(PlayerSkill.Mining, 1);
            return true;
        }

        public static bool TryPlaceBlock(
            VoxelWorld world,
            int x,
            int y,
            int z,
            BlockType blockType,
            float entityWidth,
            float entityHeight,
            Vector3 entityPosition,
            IItemContainer? inventory = null,
            bool consumeFromInventory = true)
        {
            if (blockType == BlockType.Air)
            {
                return false;
            }

            if (world.GetBlock(x, y, z) != BlockType.Air)
            {
                return false;
            }

            if (EntityIntersectsBlock(entityWidth, entityHeight, entityPosition, x, y, z))
            {
                return false;
            }

            if (consumeFromInventory && inventory != null)
            {
                if (!inventory.TryConsumeBlock(blockType, 1))
                {
                    return false;
                }
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

        public static bool TryBreakAtLook(
            VoxelWorld world,
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            IItemContainer? inventory = null,
            PlayerSkills? skills = null)
        {
            var hit = BlockInteractionSystem.RaycastSolidHit(world, origin, direction, maxDistance);
            if (!hit.HasHit)
            {
                return false;
            }

            return TryBreakBlock(
                world,
                (int)hit.BlockPos.X,
                (int)hit.BlockPos.Y,
                (int)hit.BlockPos.Z,
                inventory,
                skills);
        }
    }
}

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

            if (entityWidth > 0f &&
                entityHeight > 0f &&
                EntityIntersectsBlock(entityWidth, entityHeight, entityPosition, x, y, z))
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

            return BlockPlacement.TryPlaceBlock(
                world,
                x,
                y,
                z,
                blockType,
                entityWidth,
                entityHeight,
                entityPosition,
                checkEntityCollision: entityWidth > 0f && entityHeight > 0f);
        }

        public static bool EntityIntersectsBlock(
            float width,
            float height,
            Vector3 position,
            int x,
            int y,
            int z) =>
            BlockPlacement.EntityIntersectsBlock(width, height, position, x, y, z);

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

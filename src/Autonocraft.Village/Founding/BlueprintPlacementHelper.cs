using System;
using System.Numerics;
using Autonocraft.World;

namespace Autonocraft.Village
{
    public static class BlueprintPlacementHelper
    {
        public readonly struct ResolvedAnchor
        {
            public int AnchorX { get; init; }
            public int AnchorY { get; init; }
            public int AnchorZ { get; init; }
            public bool HasHit { get; init; }
        }

        public static ResolvedAnchor ResolveFromLook(VoxelWorld world, Vector3 origin, Vector3 direction, float maxDistance)
        {
            var hit = BlockRaycast.RaycastSolidHit(world, origin, direction, maxDistance);
            if (!hit.HasHit)
            {
                return new ResolvedAnchor { HasHit = false };
            }

            int anchorX;
            int anchorZ;
            int anchorY;

            if (hit.Normal.Y > 0.5f)
            {
                anchorX = (int)MathF.Floor(hit.BlockPos.X);
                anchorZ = (int)MathF.Floor(hit.BlockPos.Z);
                anchorY = (int)MathF.Floor(hit.BlockPos.Y) + 1;
            }
            else
            {
                Vector3 place = hit.BlockPos + hit.Normal;
                anchorX = (int)MathF.Floor(place.X);
                anchorZ = (int)MathF.Floor(place.Z);
                anchorY = GetGroundAnchorY(world, anchorX, anchorZ);
            }

            return new ResolvedAnchor
            {
                AnchorX = anchorX,
                AnchorY = anchorY,
                AnchorZ = anchorZ,
                HasHit = true
            };
        }

        public static ResolvedAnchor ResolveFallbackNearPlayer(VoxelWorld world, Vector3 playerPos, Vector3 lookDirection)
        {
            Vector3 probe = playerPos + Vector3.Normalize(lookDirection) * 4f;
            int anchorX = (int)MathF.Floor(probe.X);
            int anchorZ = (int)MathF.Floor(probe.Z);
            return new ResolvedAnchor
            {
                AnchorX = anchorX,
                AnchorY = GetGroundAnchorY(world, anchorX, anchorZ),
                AnchorZ = anchorZ,
                HasHit = false
            };
        }

        public static int GetGroundAnchorY(VoxelWorld world, int anchorX, int anchorZ)
        {
            int surface = world.GetHighestSolidY(anchorX, anchorZ);
            return surface >= 0 ? surface + 1 : 1;
        }

        public static void GetWorldBounds(
            BuildingBlueprint blueprint,
            int anchorX,
            int anchorY,
            int anchorZ,
            out int minX,
            out int minY,
            out int minZ,
            out int maxX,
            out int maxY,
            out int maxZ)
        {
            minX = minY = minZ = int.MaxValue;
            maxX = maxY = maxZ = int.MinValue;

            foreach (var block in blueprint.Template.Blocks)
            {
                int wx = anchorX + block.Dx;
                int wy = anchorY + block.Dy;
                int wz = anchorZ + block.Dz;
                minX = Math.Min(minX, wx);
                minY = Math.Min(minY, wy);
                minZ = Math.Min(minZ, wz);
                maxX = Math.Max(maxX, wx);
                maxY = Math.Max(maxY, wy);
                maxZ = Math.Max(maxZ, wz);
            }

            if (minX == int.MaxValue)
            {
                minX = maxX = anchorX;
                minY = maxY = anchorY;
                minZ = maxZ = anchorZ;
            }
        }

        /// <summary>
        /// Terrain blocks that blueprints may replace during construction. Matching these in the
        /// world must not mark a building site complete before builders finish the job.
        /// </summary>
        public static bool IsNaturalTerrainBlock(BlockType current)
        {
            if (current == BlockType.Air)
            {
                return false;
            }

            if (current.IsTransparent())
            {
                return true;
            }

            return current is BlockType.Grass
                or BlockType.Dirt
                or BlockType.Sand
                or BlockType.Snow
                or BlockType.Gravel
                or BlockType.Mud;
        }

        public static bool HasClearFootprint(
            VoxelWorld world,
            BuildingBlueprint blueprint,
            int anchorX,
            int anchorY,
            int anchorZ)
        {
            foreach (var block in blueprint.Template.Blocks)
            {
                int wx = anchorX + block.Dx;
                int wy = anchorY + block.Dy;
                int wz = anchorZ + block.Dz;
                if (wy <= 0 || wy >= Chunk.Height)
                {
                    return false;
                }

                if (world.GetBlock(wx, wy, wz) != BlockType.Air)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

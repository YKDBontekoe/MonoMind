using System.Numerics;
using Autonocraft.Diagnostics;

namespace Autonocraft.World
{
    public readonly struct BlockRaycastHit
    {
        public static BlockRaycastHit Miss => new(false, default, default, BlockType.Air, float.MaxValue);

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

    public static class BlockRaycast
    {
        public static BlockRaycastHit RaycastSolidHit(
            VoxelWorld world,
            Vector3 origin,
            Vector3 direction,
            float maxDistance) =>
            RaycastHit(world, origin, direction, maxDistance, solidOnly: true);

        public static BlockRaycastHit RaycastHit(
            VoxelWorld world,
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            bool solidOnly)
        {
            if (direction.LengthSquared() < 1e-8f)
            {
                return BlockRaycastHit.Miss;
            }

            direction = Vector3.Normalize(direction);

            int x = (int)MathF.Floor(origin.X);
            int y = (int)MathF.Floor(origin.Y);
            int z = (int)MathF.Floor(origin.Z);

            int stepX = direction.X > 0f ? 1 : (direction.X < 0f ? -1 : 0);
            int stepY = direction.Y > 0f ? 1 : (direction.Y < 0f ? -1 : 0);
            int stepZ = direction.Z > 0f ? 1 : (direction.Z < 0f ? -1 : 0);

            float tDeltaX = stepX != 0 ? MathF.Abs(1f / direction.X) : float.PositiveInfinity;
            float tDeltaY = stepY != 0 ? MathF.Abs(1f / direction.Y) : float.PositiveInfinity;
            float tDeltaZ = stepZ != 0 ? MathF.Abs(1f / direction.Z) : float.PositiveInfinity;

            float fracX = origin.X - MathF.Floor(origin.X);
            float fracY = origin.Y - MathF.Floor(origin.Y);
            float fracZ = origin.Z - MathF.Floor(origin.Z);

            float tMaxX = stepX > 0 ? (1f - fracX) * tDeltaX : (stepX < 0 ? fracX * tDeltaX : float.PositiveInfinity);
            float tMaxY = stepY > 0 ? (1f - fracY) * tDeltaY : (stepY < 0 ? fracY * tDeltaY : float.PositiveInfinity);
            float tMaxZ = stepZ > 0 ? (1f - fracZ) * tDeltaZ : (stepZ < 0 ? fracZ * tDeltaZ : float.PositiveInfinity);

            Vector3? lastAirPos = null;
            float traveled = 0f;
            const int maxSteps = 256;

            for (int step = 0; step < maxSteps && traveled <= maxDistance; step++)
            {
                PerfCounters.RaycastBlockVisits++;
                var block = world.GetBlock(x, y, z);

                if (block == BlockType.Air || (solidOnly && block.IsFluid()))
                {
                    lastAirPos = new Vector3(x, y, z);
                }
                else if (block.IsFluid() && !solidOnly)
                {
                    var hitPos = new Vector3(x, y, z);
                    var normal = ComputeHitNormal(lastAirPos, hitPos);
                    float distance = traveled + Vector3.Distance(origin, hitPos + new Vector3(0.5f, 0.5f, 0.5f)) * 0.001f;
                    return new BlockRaycastHit(true, hitPos, normal, block, distance);
                }
                else if (!block.IsFluid())
                {
                    var hitPos = new Vector3(x, y, z);
                    var normal = ComputeHitNormal(lastAirPos, hitPos);
                    float distance = traveled + Vector3.Distance(origin, hitPos + new Vector3(0.5f, 0.5f, 0.5f)) * 0.001f;
                    return new BlockRaycastHit(true, hitPos, normal, block, distance);
                }
                else
                {
                    lastAirPos = new Vector3(x, y, z);
                }

                if (tMaxX < tMaxY)
                {
                    if (tMaxX < tMaxZ)
                    {
                        x += stepX;
                        traveled = tMaxX;
                        tMaxX += tDeltaX;
                    }
                    else
                    {
                        z += stepZ;
                        traveled = tMaxZ;
                        tMaxZ += tDeltaZ;
                    }
                }
                else
                {
                    if (tMaxY < tMaxZ)
                    {
                        y += stepY;
                        traveled = tMaxY;
                        tMaxY += tDeltaY;
                    }
                    else
                    {
                        z += stepZ;
                        traveled = tMaxZ;
                        tMaxZ += tDeltaZ;
                    }
                }
            }

            return BlockRaycastHit.Miss;
        }

        private static Vector3 ComputeHitNormal(Vector3? lastAirPos, Vector3 hitPos)
        {
            if (!lastAirPos.HasValue)
            {
                return Vector3.Zero;
            }

            Vector3 diff = lastAirPos.Value - hitPos;
            if (MathF.Abs(diff.X) > MathF.Abs(diff.Y) && MathF.Abs(diff.X) > MathF.Abs(diff.Z))
            {
                return new Vector3(MathF.Sign(diff.X), 0, 0);
            }

            if (MathF.Abs(diff.Y) > MathF.Abs(diff.X) && MathF.Abs(diff.Y) > MathF.Abs(diff.Z))
            {
                return new Vector3(0, MathF.Sign(diff.Y), 0);
            }

            return new Vector3(0, 0, MathF.Sign(diff.Z));
        }
    }
}

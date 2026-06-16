using System;
using System.Collections.Generic;
using System.Numerics;

namespace Autonocraft.Entities
{
    public static class EntityRaycast
    {
        public static (Animal? animal, float distance) Raycast(
            IReadOnlyList<Animal> candidates,
            Vector3 origin,
            Vector3 direction,
            float maxDistance)
        {
            direction = Vector3.Normalize(direction);

            Animal? closest = null;
            float closestDistance = float.MaxValue;

            foreach (var animal in candidates)
            {
                if (!animal.IsAlive)
                {
                    continue;
                }

                var stats = animal.Stats;
                if (TryIntersectAabb(origin, direction, animal.Position, stats.Width, stats.Height, out float distance) &&
                    distance <= maxDistance &&
                    distance < closestDistance)
                {
                    closest = animal;
                    closestDistance = distance;
                }
            }

            if (closest == null)
            {
                return (null, float.MaxValue);
            }

            return (closest, closestDistance);
        }

        public static bool TryIntersectAabb(
            Vector3 origin,
            Vector3 direction,
            Vector3 feetPosition,
            float width,
            float height,
            out float distance)
        {
            direction = Vector3.Normalize(direction);

            float minX = feetPosition.X - width / 2f;
            float maxX = feetPosition.X + width / 2f;
            float minY = feetPosition.Y;
            float maxY = feetPosition.Y + height;
            float minZ = feetPosition.Z - width / 2f;
            float maxZ = feetPosition.Z + width / 2f;

            float tMin = 0f;
            float tMax = float.MaxValue;

            if (!Slab(origin.X, direction.X, minX, maxX, ref tMin, ref tMax) ||
                !Slab(origin.Y, direction.Y, minY, maxY, ref tMin, ref tMax) ||
                !Slab(origin.Z, direction.Z, minZ, maxZ, ref tMin, ref tMax))
            {
                distance = float.MaxValue;
                return false;
            }

            distance = tMin;
            return tMin >= 0f;
        }

        private static bool Slab(float origin, float direction, float min, float max, ref float tMin, ref float tMax)
        {
            if (MathF.Abs(direction) < 1e-6f)
            {
                return origin >= min && origin <= max;
            }

            float invDir = 1f / direction;
            float t1 = (min - origin) * invDir;
            float t2 = (max - origin) * invDir;

            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
            }

            tMin = MathF.Max(tMin, t1);
            tMax = MathF.Min(tMax, t2);
            return tMin <= tMax;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Numerics;
using Autonocraft.World;

namespace Autonocraft.Entities
{
    public static class VoxelPathfinder
    {
        public const int DefaultMaxNodes = 2048;
        public const int MaxStepUp = 1;
        public const int MaxStepDown = 3;

        public static bool TryFindPath(
            VoxelWorld world,
            Vector3 startFeet,
            Vector3 targetFeet,
            int maxHorizontalRange,
            out List<Vector3> waypoints)
        {
            waypoints = new List<Vector3>();
            int sx = (int)MathF.Floor(startFeet.X);
            int sy = (int)MathF.Floor(startFeet.Y);
            int sz = (int)MathF.Floor(startFeet.Z);
            int tx = (int)MathF.Floor(targetFeet.X);
            int ty = (int)MathF.Floor(targetFeet.Y);
            int tz = (int)MathF.Floor(targetFeet.Z);

            if (MathF.Abs(tx - sx) > maxHorizontalRange || MathF.Abs(tz - sz) > maxHorizontalRange)
            {
                return false;
            }

            if (!TryFindNearestWalkable(world, sx, sy, sz, 2, out var start) ||
                !TryFindNearestWalkable(world, tx, ty, tz, 4, out var goal))
            {
                return false;
            }

            if (start == goal)
            {
                waypoints.Add(new Vector3(goal.x + 0.5f, goal.y, goal.z + 0.5f));
                return true;
            }

            var open = new PriorityQueue<(int x, int y, int z), float>();
            var cameFrom = new Dictionary<(int, int, int), (int, int, int)>();
            var gScore = new Dictionary<(int, int, int), float> { [start] = 0f };
            open.Enqueue(start, Heuristic(start, goal));

            int expanded = 0;
            while (open.Count > 0 && expanded < DefaultMaxNodes)
            {
                expanded++;
                var current = open.Dequeue();
                if (current == goal)
                {
                    ReconstructPath(cameFrom, current, waypoints);
                    return waypoints.Count > 0;
                }

                for (int i = 0; i < 8; i++)
                {
                    int nx = current.x;
                    int nz = current.z;
                    if (i == 0) nx += 1;
                    else if (i == 1) nx -= 1;
                    else if (i == 2) nz += 1;
                    else if (i == 3) nz -= 1;
                    else if (i == 4) { nx += 1; nz += 1; }
                    else if (i == 5) { nx += 1; nz -= 1; }
                    else if (i == 6) { nx -= 1; nz += 1; }
                    else { nx -= 1; nz -= 1; }

                    for (int step = -MaxStepDown; step <= MaxStepUp; step++)
                    {
                        int ny = current.y + step;

                        // Headroom check when stepping up
                        if (step > 0 && world.GetBlock(current.x, current.y + 2, current.z).IsCollidable())
                        {
                            continue;
                        }

                        // Diagonal corner-cutting check
                        if (i >= 4)
                        {
                            if (world.GetBlock(current.x, ny, nz).IsCollidable() ||
                                world.GetBlock(nx, ny, current.z).IsCollidable() ||
                                world.GetBlock(current.x, ny + 1, nz).IsCollidable() ||
                                world.GetBlock(nx, ny + 1, current.z).IsCollidable())
                            {
                                continue;
                            }
                        }

                        if (!IsWalkable(world, nx, ny, nz))
                        {
                            continue;
                        }

                        var neighbor = (nx, ny, nz);
                        float tentative = gScore[current] + (i < 4 ? 1f : 1.41f) + MathF.Abs(step) * 0.15f;
                        if (gScore.TryGetValue(neighbor, out float existing) && tentative >= existing)
                        {
                            continue;
                        }

                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentative;
                        open.Enqueue(neighbor, tentative + Heuristic(neighbor, goal));
                    }
                }
            }

            return false;
        }

        private static bool TryFindNearestWalkable(
            VoxelWorld world,
            int x,
            int y,
            int z,
            int radius,
            out (int x, int y, int z) result)
        {
            if (IsWalkable(world, x, y, z))
            {
                result = (x, y, z);
                return true;
            }

            int bestScore = int.MaxValue;
            result = default;
            for (int dy = -MaxStepDown; dy <= MaxStepUp + 2; dy++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int score = Math.Abs(dx) + Math.Abs(dz) + Math.Abs(dy);
                        if (score >= bestScore)
                        {
                            continue;
                        }

                        int nx = x + dx;
                        int ny = y + dy;
                        int nz = z + dz;
                        if (!IsWalkable(world, nx, ny, nz))
                        {
                            continue;
                        }

                        bestScore = score;
                        result = (nx, ny, nz);
                    }
                }
            }

            return bestScore < int.MaxValue;
        }

        private static float Heuristic((int x, int y, int z) a, (int x, int y, int z) b)
        {
            return MathF.Abs(a.x - b.x) + MathF.Abs(a.y - b.y) + MathF.Abs(a.z - b.z);
        }

        private static bool IsWalkable(VoxelWorld world, int x, int y, int z)
        {
            if (world.GetBlock(x, y, z).IsCollidable())
            {
                return false;
            }

            if (!world.GetBlock(x, y - 1, z).IsCollidable() && !world.GetBlock(x, y - 1, z).IsWater())
            {
                return false;
            }

            if (world.GetBlock(x, y + 1, z).IsCollidable())
            {
                return false;
            }

            return true;
        }

        private static void ReconstructPath(
            Dictionary<(int, int, int), (int, int, int)> cameFrom,
            (int x, int y, int z) current,
            List<Vector3> waypoints)
        {
            waypoints.Add(new Vector3(current.x + 0.5f, current.y, current.z + 0.5f));
            var node = current;
            while (cameFrom.TryGetValue((node.x, node.y, node.z), out var prev))
            {
                node = prev;
                waypoints.Add(new Vector3(node.x + 0.5f, node.y, node.z + 0.5f));
            }
            waypoints.Reverse();
        }
    }
}

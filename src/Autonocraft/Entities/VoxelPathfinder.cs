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

            var start = (sx, sy, sz);
            var goal = (tx, ty, tz);
            if (start == goal)
            {
                waypoints.Add(new Vector3(tx + 0.5f, ty, tz + 0.5f));
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

                foreach (var neighbor in GetNeighbors(world, current))
                {
                    float tentative = gScore[current] + 1f;
                    if (gScore.TryGetValue(neighbor, out float existing) && tentative >= existing)
                    {
                        continue;
                    }

                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentative;
                    open.Enqueue(neighbor, tentative + Heuristic(neighbor, goal));
                }
            }

            return false;
        }

        private static float Heuristic((int x, int y, int z) a, (int x, int y, int z) b)
        {
            return MathF.Abs(a.x - b.x) + MathF.Abs(a.y - b.y) + MathF.Abs(a.z - b.z);
        }

        private static IEnumerable<(int x, int y, int z)> GetNeighbors(VoxelWorld world, (int x, int y, int z) node)
        {
            int[] dx = { 1, -1, 0, 0 };
            int[] dz = { 0, 0, 1, -1 };
            for (int i = 0; i < 4; i++)
            {
                for (int step = -MaxStepDown; step <= MaxStepUp; step++)
                {
                    int nx = node.x + dx[i];
                    int ny = node.y + step;
                    int nz = node.z + dz[i];
                    if (!IsWalkable(world, nx, ny, nz))
                    {
                        continue;
                    }

                    yield return (nx, ny, nz);
                }
            }
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
            var chain = new Stack<(int, int, int)>();
            chain.Push(current);
            while (cameFrom.TryGetValue(current, out var prev))
            {
                current = prev;
                chain.Push(current);
            }

            while (chain.Count > 0)
            {
                var node = chain.Pop();
                waypoints.Add(new Vector3(node.Item1 + 0.5f, node.Item2, node.Item3 + 0.5f));
            }
        }
    }
}

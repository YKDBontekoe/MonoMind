using System;
using System.Collections.Generic;

namespace Autonocraft.World.Generation.Trees
{
    public static class TreeShapeGenerator
    {
        public static List<TreeVoxel> Generate(
            TreeSpecies species,
            int wx,
            int wz,
            int surfaceY,
            int seed,
            float treeDensity,
            float threshold,
            float scale = 1f)
        {
            var voxels = new List<TreeVoxel>();
            scale = Math.Clamp(scale, 1f, 3f);

            int heightRange = species.MaxHeight - species.MinHeight + 1;
            int treeHeight = species.MinHeight;
            if (heightRange > 1)
            {
                treeHeight += (int)((treeDensity - threshold) * 10f) % heightRange;
            }

            if (scale > 1f)
            {
                treeHeight = (int)Math.Round(treeHeight * scale);
                treeHeight = Math.Clamp(treeHeight, species.MinHeight + 2, 28);
            }
            else
            {
                treeHeight = Math.Clamp(treeHeight, species.MinHeight, species.MaxHeight);
            }

            AddTrunkScaffold(voxels, species, treeHeight, scale);

            int trunkTop = treeHeight;
            AddCanopyScaffold(voxels, species, trunkTop, scale, wx, wz, seed);

            BranchPlacer.AddBranches(voxels, species, wx, wz, seed, trunkTop, scale);

            if (species.Shape == TreeShapeKind.Weeping)
            {
                AddWeepingLayers(voxels, species, trunkTop, scale);
            }

            int blockBudget = (int)(species.MaxBlocks * scale * scale);
            blockBudget = Math.Clamp(blockBudget, species.MaxBlocks, 280);
            EnforceBlockBudget(voxels, blockBudget);
            return Deduplicate(voxels);
        }

        private static void EnforceBlockBudget(List<TreeVoxel> voxels, int maxBlocks)
        {
            var deduped = Deduplicate(voxels);
            voxels.Clear();
            for (int i = 0; i < Math.Min(maxBlocks, deduped.Count); i++)
            {
                voxels.Add(deduped[i]);
            }
        }

        private static List<TreeVoxel> Deduplicate(List<TreeVoxel> voxels)
        {
            var seen = new HashSet<(int, int, int)>();
            var result = new List<TreeVoxel>();
            foreach (var voxel in voxels)
            {
                var key = (voxel.Dx, voxel.Dy, voxel.Dz);
                if (seen.Add(key))
                {
                    result.Add(voxel);
                }
            }

            return result;
        }

        private static void AddTrunkScaffold(List<TreeVoxel> voxels, TreeSpecies species, int treeHeight, float scale)
        {
            int thickBase = scale >= 2f ? Math.Min(5, treeHeight / 3) : 0;

            for (int dy = 1; dy <= treeHeight; dy++)
            {
                voxels.Add(new TreeVoxel(0, dy, 0, species.Log));
                if (dy <= thickBase)
                {
                    voxels.Add(new TreeVoxel(1, dy, 0, species.Log));
                    voxels.Add(new TreeVoxel(0, dy, 1, species.Log));
                    voxels.Add(new TreeVoxel(1, dy, 1, species.Log));
                }
            }

            if (species.Shape == TreeShapeKind.MultiTrunk)
            {
                voxels.Add(new TreeVoxel(1, 1, 0, species.Log));
                voxels.Add(new TreeVoxel(-1, 1, 0, species.Log));
            }

            if (species.Shape == TreeShapeKind.Conical && treeHeight >= 4)
            {
                voxels.Add(new TreeVoxel(1, treeHeight - 2, 0, species.Log));
                voxels.Add(new TreeVoxel(-1, treeHeight - 1, 0, species.Log));
            }
        }

        private static void AddCanopyScaffold(
            List<TreeVoxel> voxels,
            TreeSpecies species,
            int trunkTop,
            float scale,
            int wx,
            int wz,
            int seed)
        {
            switch (species.Shape)
            {
                case TreeShapeKind.Conical:
                    AddConicalCanopy(voxels, species, trunkTop, scale, wx, wz, seed);
                    return;
                case TreeShapeKind.Fan:
                    AddPalmCanopy(voxels, species, trunkTop, scale, wx, wz, seed);
                    return;
                case TreeShapeKind.Weeping:
                    AddRoundCanopy(voxels, species, trunkTop, scale, wx, wz, seed, extraRadius: 1);
                    return;
                default:
                    AddRoundCanopy(voxels, species, trunkTop, scale, wx, wz, seed, extraRadius: 0);
                    return;
            }
        }

        private static void AddRoundCanopy(
            List<TreeVoxel> voxels,
            TreeSpecies species,
            int trunkTop,
            float scale,
            int wx,
            int wz,
            int seed,
            int extraRadius)
        {
            float baseRadius = (1.65f + extraRadius * 0.55f) * scale;
            if (species.Shape == TreeShapeKind.Weeping)
            {
                int outer = Math.Max(5, (int)Math.Round(5.2f * scale));
                int y = trunkTop - (int)Math.Round(scale);
                voxels.Add(new TreeVoxel(outer, y, 0, species.Leaves));
                voxels.Add(new TreeVoxel(-outer, y, 0, species.Leaves));
                voxels.Add(new TreeVoxel(0, y, outer, species.Leaves));
                voxels.Add(new TreeVoxel(0, y, -outer, species.Leaves));
            }

            AddLeafBlob(voxels, species, 0, trunkTop + (int)Math.Round(scale), 0, baseRadius, 1.45f * scale, wx, wz, seed, 11);

            (int x, int z)[] lobes =
            [
                (2, 0), (-2, 0), (0, 2), (0, -2),
                (1, 1), (-1, 1), (1, -1), (-1, -1)
            ];

            int lobeCount = species.Shape == TreeShapeKind.Weeping ? 8 : 6;
            for (int i = 0; i < lobeCount; i++)
            {
                int hash = Hash(wx, wz, seed, i + 19);
                var lobe = lobes[(i + hash) % lobes.Length];
                int ox = lobe.x + (hash % 3) - 1;
                int oz = lobe.z + ((hash / 5) % 3) - 1;
                int oy = trunkTop - 1 + ((hash / 11) % 3);
                float radius = baseRadius + (hash % 2) * 0.35f;
                AddLeafBlob(voxels, species, ox, oy, oz, radius, 1.15f * scale, wx, wz, seed, i + 23);
            }

            int skirtY = trunkTop - (int)Math.Round(scale);
            for (int i = 0; i < 10; i++)
            {
                int hash = Hash(wx, wz, seed, i + 43);
                var lobe = lobes[hash % lobes.Length];
                int dx = lobe.x + (hash / 7) % 2;
                int dz = lobe.z + (hash / 13) % 2;
                voxels.Add(new TreeVoxel(dx, skirtY - hash % 2, dz, species.Leaves));
            }
        }

        private static void AddConicalCanopy(
            List<TreeVoxel> voxels,
            TreeSpecies species,
            int trunkTop,
            float scale,
            int wx,
            int wz,
            int seed)
        {
            int bottom = trunkTop - (int)Math.Round(3 * scale);
            int top = trunkTop + (int)Math.Round(scale);
            for (int dy = Math.Max(bottom, trunkTop - 3); dy <= top; dy++)
            {
                voxels.Add(new TreeVoxel(1, dy, 0, species.Leaves));
                voxels.Add(new TreeVoxel(-1, dy, 0, species.Leaves));
                voxels.Add(new TreeVoxel(0, dy, 1, species.Leaves));
                voxels.Add(new TreeVoxel(0, dy, -1, species.Leaves));
                voxels.Add(new TreeVoxel(2, dy, 0, species.Leaves));
                voxels.Add(new TreeVoxel(-2, dy, 0, species.Leaves));
                voxels.Add(new TreeVoxel(0, dy, 2, species.Leaves));
                voxels.Add(new TreeVoxel(0, dy, -2, species.Leaves));
            }

            for (int dy = bottom; dy <= top; dy++)
            {
                int fromBottom = dy - bottom;
                float taper = 1f - fromBottom / Math.Max(1f, top - bottom + 1f);
                float layerRadius = Math.Max(0.7f, (1.1f + taper * 2.35f) * scale);
                if ((fromBottom & 1) == 1)
                {
                    layerRadius -= 0.35f;
                }

                int radius = (int)Math.Ceiling(layerRadius);
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        float manhattan = Math.Abs(dx) + Math.Abs(dz) * 0.85f;
                        if (manhattan > layerRadius * 1.45f)
                        {
                            continue;
                        }

                        if (dx == 0 && dz == 0 && dy <= trunkTop)
                        {
                            continue;
                        }

                        if (IsCanopyPocket(wx, wz, dy, dx, dz, seed, 13))
                        {
                            continue;
                        }

                        voxels.Add(new TreeVoxel(dx, dy, dz, species.Leaves));
                    }
                }
            }

            voxels.Add(new TreeVoxel(0, top + 1, 0, species.Leaves));
        }

        private static void AddPalmCanopy(
            List<TreeVoxel> voxels,
            TreeSpecies species,
            int trunkTop,
            float scale,
            int wx,
            int wz,
            int seed)
        {
            int crownY = trunkTop + (int)Math.Round(scale);
            voxels.Add(new TreeVoxel(0, crownY, 0, species.Leaves));
            int length = (int)Math.Round(4 * scale);
            (int x, int z)[] directions =
            [
                (1, 0), (-1, 0), (0, 1), (0, -1),
                (1, 1), (1, -1), (-1, 1), (-1, -1)
            ];

            for (int i = 0; i < directions.Length; i++)
            {
                var direction = directions[i];
                int hash = Hash(wx, wz, seed, i + 71);
                int frondLength = Math.Max(3, length - hash % 2);
                for (int step = 1; step <= frondLength; step++)
                {
                    int dx = direction.x * step;
                    int dz = direction.z * step;
                    int droop = step > frondLength / 2 ? 1 : 0;
                    int y = crownY - droop;
                    voxels.Add(new TreeVoxel(dx, y, dz, species.Leaves));
                    if (step >= 2 && (direction.x == 0 || direction.z == 0))
                    {
                        voxels.Add(new TreeVoxel(dx + (direction.z == 0 ? 0 : 1), y, dz + (direction.x == 0 ? 0 : 1), species.Leaves));
                        voxels.Add(new TreeVoxel(dx - (direction.z == 0 ? 0 : 1), y, dz - (direction.x == 0 ? 0 : 1), species.Leaves));
                    }
                }
            }
        }

        private static void AddWeepingLayers(List<TreeVoxel> voxels, TreeSpecies species, int trunkTop, float scale)
        {
            int layerCount = (int)Math.Round(3 * scale);
            int radius = (int)Math.Round(4 * scale);
            for (int layer = 1; layer <= layerCount; layer++)
            {
                int y = trunkTop - layer;
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        int distance = Math.Abs(dx) + Math.Abs(dz);
                        bool nearEdge = distance >= radius - 1;
                        if (dx * dx + dz * dz <= radius * radius && (nearEdge || (distance + layer) % 3 == 0))
                        {
                            voxels.Add(new TreeVoxel(dx, y, dz, species.Leaves));
                            if ((Math.Abs(dx) + Math.Abs(dz) + layer) % 5 == 0 && y - 1 > trunkTop - layerCount - 2)
                            {
                                voxels.Add(new TreeVoxel(dx, y - 1, dz, species.Leaves));
                            }
                        }
                    }
                }
            }
        }

        private static void AddLeafBlob(
            List<TreeVoxel> voxels,
            TreeSpecies species,
            int cx,
            int cy,
            int cz,
            float radius,
            float height,
            int wx,
            int wz,
            int seed,
            int salt)
        {
            int r = (int)Math.Ceiling(radius);
            int h = Math.Max(1, (int)Math.Ceiling(height));
            for (int dy = -h; dy <= h; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        float nx = dx / Math.Max(0.1f, radius);
                        float ny = dy / Math.Max(0.1f, height);
                        float nz = dz / Math.Max(0.1f, radius);
                        float distance = nx * nx + ny * ny * 1.25f + nz * nz;
                        if (distance > 1f)
                        {
                            continue;
                        }

                        if (dx == 0 && dz == 0 && cy + dy <= cy)
                        {
                            continue;
                        }

                        bool edge = distance > 0.68f;
                        if (edge && IsCanopyPocket(wx, wz, cy + dy, cx + dx, cz + dz, seed, salt))
                        {
                            continue;
                        }

                        voxels.Add(new TreeVoxel(cx + dx, cy + dy, cz + dz, species.Leaves));
                    }
                }
            }
        }

        private static bool IsCanopyPocket(int wx, int wz, int y, int dx, int dz, int seed, int salt)
        {
            if (Math.Abs(dx) <= 1 && Math.Abs(dz) <= 1)
            {
                return false;
            }

            return Hash(wx + dx, wz + dz, seed + y, salt) % 17 == 0;
        }

        private static int Hash(int x, int z, int seed, int salt)
        {
            unchecked
            {
                int h = seed;
                h = h * 397 ^ x;
                h = h * 397 ^ z;
                h = h * 397 ^ salt;
                return h & 0x7fffffff;
            }
        }
    }
}

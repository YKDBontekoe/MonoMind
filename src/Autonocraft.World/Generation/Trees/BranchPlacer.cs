using System;
using System.Collections.Generic;

namespace Autonocraft.World.Generation.Trees
{
    public static class BranchPlacer
    {
        public static void AddBranches(
            List<TreeVoxel> voxels,
            TreeSpecies species,
            int wx,
            int wz,
            int seed,
            int trunkTop,
            float scale = 1f)
        {
            int branchSeed = Math.Abs(wx * 31337 + wz * 7919 + seed);
            int nodeCount = Math.Min((int)Math.Round(4 * scale), trunkTop - 2);
            if (nodeCount <= 0)
            {
                return;
            }

            for (int node = 1; node <= nodeCount; node++)
            {
                int nodeHash = branchSeed + node * 97;
                if ((nodeHash % 100) / 100f > species.BranchChance)
                {
                    continue;
                }

                int startY = Math.Max(3, trunkTop - node);
                int branchLength = 2 + nodeHash % 2;
                if (species.Shape == TreeShapeKind.Round || species.Shape == TreeShapeKind.MultiTrunk)
                {
                    branchLength = 2 + nodeHash % 3;
                }

                if (scale >= 2f)
                {
                    branchLength += 1 + nodeHash % 2;
                }

                int dx = 0;
                int dz = 0;
                (int x, int z) direction = ResolveDirection(nodeHash);
                for (int step = 1; step <= branchLength; step++)
                {
                    dx += direction.x;
                    dz += direction.z;
                    if (step < branchLength)
                    {
                        voxels.Add(new TreeVoxel(dx, startY + step / 3, dz, species.Log));
                    }
                }

                AddLeafCluster(voxels, species, dx, startY + branchLength / 2, dz, scale);
            }
        }

        private static (int x, int z) ResolveDirection(int hash)
        {
            return (hash % 8) switch
            {
                0 => (1, 0),
                1 => (1, 1),
                2 => (0, 1),
                3 => (-1, 1),
                4 => (-1, 0),
                5 => (-1, -1),
                6 => (0, -1),
                _ => (1, -1)
            };
        }

        private static void AddLeafCluster(List<TreeVoxel> voxels, TreeSpecies species, int cx, int cy, int cz, float scale)
        {
            int leafRadius = scale >= 2f ? 2 : 1;
            for (int dx = -leafRadius; dx <= leafRadius; dx++)
            {
                for (int dz = -leafRadius; dz <= leafRadius; dz++)
                {
                    for (int dy = 0; dy <= (scale >= 2f ? 2 : 1); dy++)
                    {
                        if (dx * dx + dz * dz <= leafRadius * leafRadius)
                        {
                            voxels.Add(new TreeVoxel(cx + dx, cy + dy, cz + dz, species.Leaves));
                        }
                    }
                }
            }
        }
    }
}

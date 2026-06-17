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

                int startY = node + 2;
                float angle = species.BranchAngle * (nodeHash % 2 == 0 ? 1f : -1f) * (MathF.PI / 180f);
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
                for (int step = 1; step <= branchLength; step++)
                {
                    dx += (int)MathF.Round(MathF.Cos(angle));
                    dz += (int)MathF.Round(MathF.Sin(angle));
                    if (step < branchLength)
                    {
                        voxels.Add(new TreeVoxel(dx, startY + step / 2, dz, species.Log));
                    }
                }

                AddLeafCluster(voxels, species, dx, startY + branchLength / 2, dz, scale);
            }
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

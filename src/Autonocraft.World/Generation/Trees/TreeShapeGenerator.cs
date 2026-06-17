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
            AddCanopyScaffold(voxels, species, trunkTop, scale);

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

        private static void AddCanopyScaffold(List<TreeVoxel> voxels, TreeSpecies species, int trunkTop, float scale)
        {
            int canopyBottom = trunkTop - (int)Math.Round(2 * scale);
            int canopyTop = trunkTop + (int)Math.Round(scale);
            int canopyLayers = canopyTop - canopyBottom;

            for (int dy = canopyBottom; dy <= canopyTop; dy++)
            {
                int layerFromBottom = dy - canopyBottom;
                int radius = (int)Math.Round(GetScaffoldRadius(species, layerFromBottom, canopyLayers) * scale);

                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        if (dx * dx + dz * dz > radius * radius)
                        {
                            continue;
                        }

                        if (dx == 0 && dz == 0 && dy <= trunkTop)
                        {
                            continue;
                        }

                        voxels.Add(new TreeVoxel(dx, dy, dz, species.Leaves));
                    }
                }
            }

            if (species.Shape == TreeShapeKind.Fan)
            {
                int fanY = trunkTop + (int)Math.Round(scale);
                int fanRadius = (int)Math.Round(3 * scale);
                for (int dx = -fanRadius; dx <= fanRadius; dx++)
                {
                    for (int dz = -fanRadius + 1; dz <= fanRadius; dz++)
                    {
                        if (dx * dx + dz * dz <= fanRadius * fanRadius && !(dx == 0 && dz == 0))
                        {
                            voxels.Add(new TreeVoxel(dx, fanY, dz, species.Leaves));
                        }
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
                        if (dx * dx + dz * dz <= radius * radius)
                        {
                            voxels.Add(new TreeVoxel(dx, y, dz, species.Leaves));
                        }
                    }
                }
            }
        }

        private static int GetScaffoldRadius(TreeSpecies species, int layerFromBottom, int totalLayers)
        {
            return species.Shape switch
            {
                TreeShapeKind.Conical => layerFromBottom <= 1 ? 2 : 1,
                TreeShapeKind.Weeping => layerFromBottom >= totalLayers - 1 ? 4 : layerFromBottom <= 1 ? 2 : 4,
                TreeShapeKind.Fan => layerFromBottom >= totalLayers - 1 ? 2 : 0,
                TreeShapeKind.Round => Math.Max(1, 2 - Math.Abs(layerFromBottom - totalLayers / 2)),
                _ => Math.Max(1, 2 - Math.Abs(layerFromBottom - totalLayers / 2))
            };
        }
    }
}

using System;
using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.World;

namespace Autonocraft.Village
{
    public static class JobTargetScanner
    {
        public static Vector3? FindNearbyLumberTarget(
            VoxelWorld world,
            Village village,
            VillageBuilding? lumberCamp,
            Vector3 from)
        {
            if (lumberCamp != null)
            {
                int radius = BuildingEffects.GetGatherScanRadius(BuildingKind.LumberCamp);
                var target = ScanForLumber(world, village, lumberCamp.AnchorX, lumberCamp.AnchorZ, radius);
                if (target.HasValue)
                {
                    return target;
                }
            }

            return ScanForLumber(world, village, village.AnchorX, village.AnchorZ, BuildingEffects.BaseGatherScanRadius);
        }

        public static Vector3? FindNearbyMineTarget(VoxelWorld world, Village village, VillageBuilding quarry)
        {
            int centerX = quarry.AnchorX;
            int centerZ = quarry.AnchorZ;
            int radius = BuildingEffects.QuarryScanRadius;
            int minY = Math.Max(1, quarry.AnchorY - BuildingEffects.QuarryDepthLimit);
            int maxY = quarry.AnchorY;

            for (int r = 1; r <= radius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dz) != r)
                        {
                            continue;
                        }

                        int x = centerX + dx;
                        int z = centerZ + dz;
                        int topY = Math.Min(maxY, world.GetHighestSolidY(x, z));
                        for (int y = topY; y >= minY; y--)
                        {
                            var block = world.GetBlock(x, y, z);
                            if (village.IsProtectedStructureBlock(x, y, z, block))
                            {
                                continue;
                            }

                            if (block is BlockType.Stone or BlockType.Cobblestone or BlockType.CoalOre
                                or BlockType.IronOre or BlockType.GoldOre or BlockType.Gravel or BlockType.MossStone)
                            {
                                return new Vector3(x + 0.5f, y, z + 0.5f);
                            }
                        }
                    }
                }
            }

            return null;
        }

        public static Vector3? FindNearbyFarmTarget(
            VoxelWorld world,
            Village village,
            Vector3 from,
            VillageBuilding? farmPlot = null)
        {
            farmPlot ??= village.GetNearestBuilding(BuildingKind.FarmPlot, from);
            if (farmPlot == null)
            {
                return null;
            }

            return FarmCropHelper.FindBestFarmCell(world, village, farmPlot, from)
                ?? village.GetBuildingWorkPosition(farmPlot);
        }

        public static Vector3? FindNearbyStockTarget(VoxelWorld world, Village village, BlockType blockType)
        {
            int centerX = village.AnchorX;
            int centerZ = village.AnchorZ;
            int radius = BuildingEffects.QuarryScanRadius;
            int minY = Math.Max(1, village.AnchorY - BuildingEffects.QuarryDepthLimit);
            int maxY = village.AnchorY + 4;

            for (int r = 1; r <= radius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dz) != r)
                        {
                            continue;
                        }

                        int x = centerX + dx;
                        int z = centerZ + dz;
                        int topY = Math.Min(maxY, world.GetHighestSolidY(x, z));
                        for (int y = topY; y >= minY; y--)
                        {
                            if (world.GetBlock(x, y, z) == blockType)
                            {
                                return new Vector3(x + 0.5f, y, z + 0.5f);
                            }
                        }
                    }
                }
            }

            var category = GatherBlockClassifier.GetCategory(blockType);
            if (category == GatherCategory.Mine)
            {
                var quarry = village.GetNearestBuilding(BuildingKind.Quarry, village.Center);
                if (quarry != null)
                {
                    return FindNearbyMineTarget(world, village, quarry);
                }
            }
            else if (category == GatherCategory.Lumber)
            {
                var lumberCamp = village.GetNearestBuilding(BuildingKind.LumberCamp, village.Center);
                return FindNearbyLumberTarget(world, village, lumberCamp, village.Center);
            }

            return null;
        }

        private static Vector3? ScanForLumber(VoxelWorld world, Village village, int centerX, int centerZ, int radius)
        {
            for (int r = 1; r <= radius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dz) != r)
                        {
                            continue;
                        }

                        int x = centerX + dx;
                        int z = centerZ + dz;
                        int topY = world.GetHighestSolidY(x, z);
                        for (int y = Math.Max(1, topY - 8); y <= topY; y++)
                        {
                            var block = world.GetBlock(x, y, z);
                            if (village.IsProtectedStructureBlock(x, y, z, block))
                            {
                                continue;
                            }

                            if (IsLumberLog(block))
                            {
                                return new Vector3(x + 0.5f, y, z + 0.5f);
                            }
                        }
                    }
                }
            }

            return null;
        }

        private static bool IsLumberLog(BlockType block) =>
            block is BlockType.OakLog
                or BlockType.BirchLog
                or BlockType.PineLog
                or BlockType.WillowLog
                or BlockType.PalmLog;
    }
}

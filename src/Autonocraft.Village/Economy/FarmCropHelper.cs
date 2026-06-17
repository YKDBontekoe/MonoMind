using System;
using System.Numerics;
using Autonocraft.Domain.Core;
using Autonocraft.Domain.Village;
using Autonocraft.Domain.World;
using Autonocraft.World;

namespace Autonocraft.Village
{
    public enum FarmWorkKind
    {
        None = 0,
        Harvest = 1,
        Plant = 2
    }

    public static class FarmCropHelper
    {
        public const float WheatFoodValue = 1f;
        public const float CarrotFoodValue = 0.75f;
        public const float HayBaleFoodValue = 0.5f;

        public static bool IsSprout(BlockType block) =>
            block is BlockType.WheatSprout or BlockType.CarrotSprout;

        public static bool IsMatureCrop(BlockType block) =>
            block is BlockType.Wheat or BlockType.Carrot;

        public static bool IsCropBlock(BlockType block) => IsSprout(block) || IsMatureCrop(block);

        public static bool IsFoodCrop(BlockType block) =>
            block is BlockType.Wheat or BlockType.Carrot or BlockType.HayBale;

        public static bool IsPlantableSoil(BlockType block) => block == BlockType.Dirt;

        public static BlockType GetSproutBlock(BlockType crop) => crop switch
        {
            BlockType.Carrot => BlockType.CarrotSprout,
            _ => BlockType.WheatSprout
        };

        public static BlockType GetMatureBlock(BlockType sprout) => sprout switch
        {
            BlockType.CarrotSprout => BlockType.Carrot,
            _ => BlockType.Wheat
        };

        public static BlockType PickPlantCrop(int worldX, int worldZ) =>
            ((worldX ^ worldZ) & 1) == 0 ? BlockType.Wheat : BlockType.Carrot;

        public static BlockType GetHarvestProduct(BlockType matureCrop) => matureCrop;

        public static float GetFoodValue(BlockType block) => block switch
        {
            BlockType.Wheat => WheatFoodValue,
            BlockType.Carrot => CarrotFoodValue,
            BlockType.HayBale => HayBaleFoodValue,
            _ => 0f
        };

        public static FarmWorkKind ClassifyWork(BlockType block)
        {
            if (IsMatureCrop(block))
            {
                return FarmWorkKind.Harvest;
            }

            if (IsPlantableSoil(block))
            {
                return FarmWorkKind.Plant;
            }

            return FarmWorkKind.None;
        }

        public static Vector3 GetBlockCenter(int x, int y, int z) => new Vector3(x + 0.5f, y, z + 0.5f);

        public static Vector3 GetApproachPosition(VoxelWorld world, int bx, int by, int bz, Vector3 from)
        {
            int feetY = by + 1;
            Vector3? best = null;
            float bestDist = float.MaxValue;
            int[] dx = { 1, -1, 0, 0 };
            int[] dz = { 0, 0, 1, -1 };
            for (int i = 0; i < 4; i++)
            {
                int ax = bx + dx[i];
                int az = bz + dz[i];
                if (world.GetBlock(ax, feetY, az).IsCollidable())
                {
                    continue;
                }

                if (!world.GetBlock(ax, feetY - 1, az).IsCollidable())
                {
                    continue;
                }

                if (world.GetBlock(ax, feetY + 1, az).IsCollidable())
                {
                    continue;
                }

                var candidate = new Vector3(ax + 0.5f, feetY, az + 0.5f);
                float dist = Vector3.DistanceSquared(from, candidate);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = candidate;
                }
            }

            return best ?? new Vector3(bx + 0.5f, feetY, bz + 0.5f);
        }

        public static Vector3? FindBestFarmCell(
            VoxelWorld world,
            Village village,
            VillageBuilding farmPlot,
            Vector3 from,
            Vector3? skipCell = null)
        {
            if (!PlayerStructureRegistry.TryGet(farmPlot.BlueprintId, out var blueprint))
            {
                return null;
            }

            Vector3? harvestTarget = null;
            Vector3? plantTarget = null;
            float bestHarvest = float.MaxValue;
            float bestPlant = float.MaxValue;

            foreach (var block in blueprint.Template.Blocks)
            {
                if (block.Type != BlockType.Dirt)
                {
                    continue;
                }

                int wx = farmPlot.AnchorX + block.Dx;
                int wy = farmPlot.AnchorY + block.Dy;
                int wz = farmPlot.AnchorZ + block.Dz;
                var center = GetBlockCenter(wx, wy, wz);
                if (skipCell.HasValue &&
                    MathF.Abs(center.X - skipCell.Value.X) < 0.1f &&
                    MathF.Abs(center.Y - skipCell.Value.Y) < 0.1f &&
                    MathF.Abs(center.Z - skipCell.Value.Z) < 0.1f)
                {
                    continue;
                }

                var worldBlock = world.GetBlock(wx, wy, wz);
                var work = ClassifyWork(worldBlock);
                if (work == FarmWorkKind.None)
                {
                    continue;
                }

                float dist = Vector3.DistanceSquared(from, center);
                if (work == FarmWorkKind.Harvest && dist < bestHarvest)
                {
                    bestHarvest = dist;
                    harvestTarget = center;
                }
                else if (work == FarmWorkKind.Plant && dist < bestPlant)
                {
                    bestPlant = dist;
                    plantTarget = center;
                }
            }

            return harvestTarget ?? plantTarget;
        }

        public static Vector3? FindBestFarmCellAnyPlot(
            VoxelWorld world,
            Village village,
            Vector3 from,
            VillageBuilding? preferredPlot = null)
        {
            Vector3? bestHarvest = null;
            Vector3? bestPlant = null;
            float bestHarvestDist = float.MaxValue;
            float bestPlantDist = float.MaxValue;

            foreach (var building in village.Buildings)
            {
                if (building.Kind != BuildingKind.FarmPlot)
                {
                    continue;
                }

                var cell = FindBestFarmCell(world, village, building, from);
                if (!cell.HasValue)
                {
                    continue;
                }

                int bx = (int)MathF.Floor(cell.Value.X);
                int by = (int)MathF.Floor(cell.Value.Y);
                int bz = (int)MathF.Floor(cell.Value.Z);
                var work = ClassifyWork(world.GetBlock(bx, by, bz));
                float dist = Vector3.DistanceSquared(from, cell.Value);
                if (preferredPlot != null && building.Id == preferredPlot.Id)
                {
                    dist *= 0.85f;
                }

                if (work == FarmWorkKind.Harvest && dist < bestHarvestDist)
                {
                    bestHarvestDist = dist;
                    bestHarvest = cell;
                }
                else if (work == FarmWorkKind.Plant && dist < bestPlantDist)
                {
                    bestPlantDist = dist;
                    bestPlant = cell;
                }
            }

            return bestHarvest ?? bestPlant;
        }

        public static bool HasAdjacentWater(VoxelWorld world, int x, int y, int z)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0)
                    {
                        continue;
                    }

                    if (world.GetBlock(x + dx, y, z + dz).IsWater())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static float GetSeasonGrowthMultiplier(float timeOfDay)
        {
            float t = DayNightCycle.WarpTimeForSun(timeOfDay);
            float angle = t * MathF.PI * 2f;
            return 0.7f + 0.3f * MathF.Max(0f, MathF.Sin(angle - MathF.PI * 0.5f));
        }

        public static float GetRainGrowthMultiplier(VoxelWorld world, int x, int y, int z) =>
            HasAdjacentWater(world, x, y, z) ? 1.25f : 1f;
    }

}

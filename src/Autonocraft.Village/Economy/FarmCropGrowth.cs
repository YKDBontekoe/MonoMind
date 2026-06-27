using System;
using Autonocraft.Domain.Core;
using Autonocraft.Domain.Village;
using Autonocraft.Domain.World;
using Autonocraft.World;

namespace Autonocraft.Village
{
    public static class FarmCropGrowth
    {
        public const float BaseGrowthInterval = 45f;

        public static void Advance(VoxelWorld world, Village village, float deltaTime, float timeOfDay, float timeScale = DayNightCycle.DefaultTimeScale)
        {
            if (!village.HasBuilding(BuildingKind.FarmPlot))
            {
                return;
            }

            float bonus = 0f;
            int samples = 0;
            foreach (var building in village.Buildings)
            {
                if (building.Kind != BuildingKind.FarmPlot ||
                    !PlayerStructureRegistry.TryGet(building.BlueprintId, out var blueprint))
                {
                    continue;
                }

                foreach (var block in blueprint.Template.Blocks)
                {
                    if (block.Type != BlockType.Dirt)
                    {
                        continue;
                    }

                    int wx = building.AnchorX + block.Dx;
                    int wy = building.AnchorY + block.Dy;
                    int wz = building.AnchorZ + block.Dz;
                    if (!FarmCropHelper.IsSprout(world.GetBlock(wx, wy, wz)))
                    {
                        continue;
                    }

                    bonus += FarmCropHelper.GetSeasonGrowthMultiplier(timeOfDay)
                        * FarmCropHelper.GetRainGrowthMultiplier(world, wx, wy, wz);
                    samples++;
                }
            }

            if (samples == 0)
            {
                return;
            }

            float interval = BaseGrowthInterval / Math.Max(0.5f, bonus / samples);
            if (village.HasBuilding(BuildingKind.Well))
            {
                interval /= 1.15f;
            }

            float speedFactor = MathF.Max(1f, timeScale / DayNightCycle.DefaultTimeScale);
            village.FarmGrowthAccumulator += deltaTime * speedFactor;
            while (village.FarmGrowthAccumulator >= interval)
            {
                village.FarmGrowthAccumulator -= interval;
                if (!TryMatureOneSprout(world, village))
                {
                    break;
                }
            }
        }

        private static bool TryMatureOneSprout(VoxelWorld world, Village village)
        {
            VillageBuilding? chosenPlot = null;
            int chosenX = 0;
            int chosenY = 0;
            int chosenZ = 0;
            int bestKey = int.MaxValue;

            foreach (var building in village.Buildings)
            {
                if (building.Kind != BuildingKind.FarmPlot ||
                    !PlayerStructureRegistry.TryGet(building.BlueprintId, out var blueprint))
                {
                    continue;
                }

                foreach (var block in blueprint.Template.Blocks)
                {
                    if (block.Type != BlockType.Dirt)
                    {
                        continue;
                    }

                    int wx = building.AnchorX + block.Dx;
                    int wy = building.AnchorY + block.Dy;
                    int wz = building.AnchorZ + block.Dz;
                    var worldBlock = world.GetBlock(wx, wy, wz);
                    if (!FarmCropHelper.IsSprout(worldBlock))
                    {
                        continue;
                    }

                    int key = wx * 73856093 ^ wy * 19349663 ^ wz * 83492791;
                    if (key < bestKey)
                    {
                        bestKey = key;
                        chosenPlot = building;
                        chosenX = wx;
                        chosenY = wy;
                        chosenZ = wz;
                    }
                }
            }

            if (chosenPlot == null)
            {
                return false;
            }

            var sprout = world.GetBlock(chosenX, chosenY, chosenZ);
            world.SetBlock(chosenX, chosenY, chosenZ, FarmCropHelper.GetMatureBlock(sprout));
            return true;
        }
    }
}

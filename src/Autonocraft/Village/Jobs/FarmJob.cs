using System;
using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Village.Jobs
{
    internal sealed class FarmJob : IVillagerJob
    {
        public void Tick(Villager villager, float deltaTime, VoxelWorld world, VillageContext context)
        {
            if (context.Village == null)
            {
                villager.AssignJob(JobType.Idle, null, null);
                return;
            }

            if (!TryResolveFarmTarget(villager, world, context, out var workCell, out var approach))
            {
                villager.AssignJob(JobType.Idle, null, null);
                return;
            }

            villager.SetJobTarget(workCell);
            if (villager.AiPhase == VillagerAiPhase.PathTo)
            {
                if (VillagerMovementHelper.TryMoveAlongPath(villager, deltaTime, world) ||
                    VillagerMovementHelper.TryMoveToward(villager, deltaTime, world, approach))
                {
                    return;
                }

                villager.SetAiPhase(VillagerAiPhase.Working);
            }

            if (villager.AiPhase != VillagerAiPhase.Working)
            {
                return;
            }

            int bx = (int)MathF.Floor(workCell.X);
            int by = (int)MathF.Floor(workCell.Y);
            int bz = (int)MathF.Floor(workCell.Z);
            var block = world.GetBlock(bx, by, bz);
            var work = FarmCropHelper.ClassifyWork(block);
            if (work == FarmWorkKind.None)
            {
                villager.MarkedResource = null;
                TryAdvanceFarmTarget(villager, world, context);
                return;
            }

            villager.WorkTimer += deltaTime * villager.WorkSpeedMultiplier * villager.Skills.GetBonus(VillagerSkill.Farming);
            float workDuration = Villager.WorkInterval * (work == FarmWorkKind.Harvest ? 1f : 1.2f);
            if (villager.WorkTimer < workDuration)
            {
                return;
            }

            villager.WorkTimer = 0f;
            if (work == FarmWorkKind.Harvest)
            {
                var harvest = FarmCropHelper.GetHarvestProduct(block);
                world.SetBlock(bx, by, bz, BlockType.Dirt);
                GrantFarmYield(villager, context, FarmCropHelper.GetFoodValue(harvest));
                villager.Skills.AddXp(VillagerSkill.Farming, 1f);
                villager.Inventory.AddItem(ItemStack.CreateBlock(harvest, 1));
                VillagerCarryHelper.TryOffloadCarryToOutputChest(villager, context);
            }
            else
            {
                var crop = FarmCropHelper.PickPlantCrop(bx, bz);
                world.SetBlock(bx, by, bz, FarmCropHelper.GetSproutBlock(crop));
                villager.Skills.AddXp(VillagerSkill.Farming, 0.5f);
            }

            villager.MarkedResource = null;
            TryAdvanceFarmTarget(villager, world, context);
        }

        private static void GrantFarmYield(Villager villager, VillageContext context, float baseAmount)
        {
            if (context.Village == null)
            {
                return;
            }

            float yield = baseAmount
                * villager.Skills.GetBonus(VillagerSkill.Farming)
                * VillagerTraits.GetFarmYieldMultiplier(villager.Persona.Trait);
            context.Village.AddFarmFood(yield);
        }

        private static bool TryResolveFarmTarget(
            Villager villager,
            VoxelWorld world,
            VillageContext context,
            out Vector3 workCell,
            out Vector3 approach)
        {
            workCell = default;
            approach = default;
            if (context.Village == null)
            {
                return false;
            }

            if (villager.MarkedResource.HasValue || villager.JobTarget.HasValue)
            {
                workCell = villager.MarkedResource ?? villager.JobTarget!.Value;
                int bx = (int)MathF.Floor(workCell.X);
                int by = (int)MathF.Floor(workCell.Y);
                int bz = (int)MathF.Floor(workCell.Z);
                var block = world.GetBlock(bx, by, bz);
                if (FarmCropHelper.ClassifyWork(block) != FarmWorkKind.None)
                {
                    approach = FarmCropHelper.GetApproachPosition(world, bx, by, bz, villager.Position);
                    return true;
                }

                villager.MarkedResource = null;
                villager.ClearJobTarget();
            }

            VillageBuilding? plot = null;
            if (villager.AssignedBuildingId.HasValue &&
                context.Village.TryGetBuilding(villager.AssignedBuildingId.Value, out var assigned) &&
                assigned.Kind == BuildingKind.FarmPlot)
            {
                plot = assigned;
            }

            var next = plot != null
                ? FarmCropHelper.FindBestFarmCell(world, context.Village, plot, villager.Position)
                : FarmCropHelper.FindBestFarmCellAnyPlot(world, context.Village, villager.Position);
            if (!next.HasValue)
            {
                return false;
            }

            workCell = next.Value;
            villager.MarkedResource = workCell;
            villager.SetJobTarget(workCell);
            int wx = (int)MathF.Floor(workCell.X);
            int wy = (int)MathF.Floor(workCell.Y);
            int wz = (int)MathF.Floor(workCell.Z);
            approach = FarmCropHelper.GetApproachPosition(world, wx, wy, wz, villager.Position);
            TryBeginFarmPath(villager, world, approach);
            return true;
        }

        private static void TryAdvanceFarmTarget(Villager villager, VoxelWorld world, VillageContext context)
        {
            if (!TryResolveFarmTarget(villager, world, context, out _, out _))
            {
                villager.AssignJob(JobType.Idle, null, null);
                return;
            }

            villager.SetAiPhase(VillagerAiPhase.PathTo);
        }

        private static void TryBeginFarmPath(Villager villager, VoxelWorld world, Vector3 approach)
        {
            if (VoxelPathfinder.TryFindPath(world, villager.Position, approach, 24, out var waypoints))
            {
                villager.SetPath(waypoints);
            }
            else
            {
                villager.ClearPath();
            }
        }
    }
}

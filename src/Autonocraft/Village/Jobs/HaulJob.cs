using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Village.Jobs
{
    internal sealed class HaulJob : IVillagerJob
    {
        public void Tick(Villager villager, float deltaTime, VoxelWorld world, VillageContext context)
        {
            if (context.Village == null)
            {
                villager.AssignJob(JobType.Idle, null, null);
                return;
            }

            if (!villager.HaulIsDelivering)
            {
                if (!VillagerInventoryHelper.IsInventoryEmpty(villager))
                {
                    villager.SetHaulDelivering(true);
                    villager.ClearHaulSources();
                    PrepareDeliveryTarget(villager, context);
                    villager.SetAiPhase(VillagerAiPhase.PathTo);
                }
                else if (villager.HaulSourceChestId.HasValue || villager.HaulSourceVillagerId.HasValue)
                {
                    var pickupPos = GetHaulPickupPosition(villager, context);
                    if (!pickupPos.HasValue)
                    {
                        villager.AssignJob(JobType.Idle, null, null);
                        return;
                    }

                    if (villager.AiPhase == VillagerAiPhase.PathTo)
                    {
                        if (VillagerMovementHelper.TryMoveToward(villager, deltaTime, world, pickupPos.Value))
                        {
                            return;
                        }

                        villager.SetAiPhase(VillagerAiPhase.Working);
                    }

                    TryExecuteHaulPickup(villager, context);
                    if (VillagerInventoryHelper.IsInventoryEmpty(villager))
                    {
                        villager.AssignJob(JobType.Idle, null, null);
                        return;
                    }

                    villager.SetHaulDelivering(true);
                    villager.ClearHaulSources();
                    PrepareDeliveryTarget(villager, context);
                    villager.SetAiPhase(VillagerAiPhase.PathTo);
                }
                else
                {
                    villager.AssignJob(JobType.Idle, null, null);
                }

                return;
            }

            if (VillagerInventoryHelper.IsInventoryEmpty(villager))
            {
                villager.AssignJob(JobType.Idle, null, null);
                return;
            }

            var destination = villager.JobTarget ?? context.StoragePosition;
            if (villager.AiPhase == VillagerAiPhase.PathTo)
            {
                if (VillagerMovementHelper.TryMoveToward(villager, deltaTime, world, destination))
                {
                    return;
                }

                villager.SetAiPhase(VillagerAiPhase.Working);
            }

            DepositAtDeliveryTarget(villager, context);
            if (VillagerInventoryHelper.IsInventoryEmpty(villager))
            {
                villager.AssignJob(JobType.Idle, null, null);
                return;
            }

            PrepareDeliveryTarget(villager, context);
            villager.SetAiPhase(VillagerAiPhase.PathTo);
        }

        private static Vector3? GetHaulPickupPosition(Villager villager, VillageContext context)
        {
            if (villager.HaulSourceChestId.HasValue &&
                context.Village != null &&
                context.Village.TryGetOutputChest(villager.HaulSourceChestId.Value, out var chest))
            {
                return chest.Position;
            }

            if (villager.HaulSourceVillagerId.HasValue &&
                context.TryGetVillager(villager.HaulSourceVillagerId.Value, out var source))
            {
                return source.Position;
            }

            return villager.JobTarget;
        }

        private static void TryExecuteHaulPickup(Villager villager, VillageContext context)
        {
            if (context.Village == null)
            {
                return;
            }

            if (villager.HaulSourceChestId.HasValue &&
                context.Village.TryGetOutputChest(villager.HaulSourceChestId.Value, out var chest))
            {
                HaulLogistics.TryPickupChestToHauler(chest, villager);
                return;
            }

            if (villager.HaulSourceVillagerId.HasValue &&
                context.TryGetVillager(villager.HaulSourceVillagerId.Value, out var source))
            {
                HaulLogistics.TryPickupVillagerToHauler(source, villager);
            }
        }

        private static void PrepareDeliveryTarget(Villager villager, VillageContext context)
        {
            if (context.Village == null ||
                !HaulLogistics.TryGetHighestPriorityStack(villager.Inventory, out _, out var stack))
            {
                villager.SetJobTarget(context.StoragePosition);
                return;
            }

            villager.SetJobTarget(HaulLogistics.ResolveDeliveryTarget(
                context.Village,
                stack.BlockType,
                villager.Position,
                out _));
        }

        private static void DepositAtDeliveryTarget(Villager villager, VillageContext context)
        {
            if (context.Village == null ||
                !HaulLogistics.TryGetHighestPriorityStack(villager.Inventory, out int slot, out var stack))
            {
                return;
            }

            HaulLogistics.ResolveDeliveryTarget(
                context.Village,
                stack.BlockType,
                villager.Position,
                out bool toFoodStock);

            if (toFoodStock && stack.IsBlock() && FarmCropHelper.IsFoodCrop(stack.BlockType))
            {
                context.Village.AddFarmFood(FarmCropHelper.GetFoodValue(stack.BlockType) * stack.Count);
                villager.Inventory.SetSlot(slot, ItemStack.Empty);
                return;
            }

            if (context.Storage.AddItem(stack))
            {
                villager.Inventory.SetSlot(slot, ItemStack.Empty);
            }
        }
    }
}

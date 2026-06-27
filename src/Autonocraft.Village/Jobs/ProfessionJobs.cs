using System;
using System.Collections.Generic;
using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Village.Jobs
{
    internal sealed class HunterJob : IVillagerJob
    {
        public void Tick(Villager villager, float deltaTime, VoxelWorld world, VillageContext context)
        {
            if (context.Village == null)
            {
                villager.AssignJob(JobType.Idle, null, null);
                return;
            }

            var animal = FindNearestAnimal(context, villager.Position);
            if (animal == null)
            {
                villager.AssignJob(JobType.Idle, null, null);
                return;
            }

            var target = animal.Position;
            villager.SetJobTarget(target);
            if (villager.AiPhase == VillagerAiPhase.PathTo)
            {
                if (!(context.CreativeMode && context.IsTestMode) && VillagerMovementHelper.TryMoveToward(villager, deltaTime, world, target))
                {
                    return;
                }

                villager.SetAiPhase(VillagerAiPhase.Working);
            }

            villager.WorkTimer += deltaTime * villager.WorkSpeedMultiplier;
            if (villager.WorkTimer < Villager.WorkInterval * 1.5f)
            {
                return;
            }

            villager.WorkTimer = 0f;
            animal.TakeDamage(4f, villager.Position);

            if (!animal.IsAlive)
            {
                context.Animals?.KillAnimal(animal);
                // Hunter harvested raw meat (represented by Carrot block item in storage)
                var meat = ItemStack.CreateBlock(BlockType.Carrot, 2);
                if (!villager.Inventory.AddItem(meat))
                {
                    // If hunter inventory is full, drop it on the ground or add directly to storage as fallback
                    context.Village.Storage.AddItem(meat);
                }
                villager.AssignJob(JobType.Idle, null, null);
            }
            else
            {
                villager.SetAiPhase(VillagerAiPhase.PathTo);
            }
        }

        private static Animal? FindNearestAnimal(VillageContext context, Vector3 from)
        {
            if (context.Animals == null)
            {
                return null;
            }

            var candidates = context.Animals.GetAnimalsInRange(context.VillageCenter, context.VillageRadius * 1.5f);
            Animal? best = null;
            float bestDist = float.MaxValue;
            foreach (var animal in candidates)
            {
                if (!animal.IsAlive)
                {
                    continue;
                }

                float dist = Vector3.Distance(from, animal.Position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = animal;
                }
            }

            return best;
        }
    }

    internal sealed class MasonJob : GatherJobBase
    {
        protected override ToolType RequiredTool => ToolType.Pickaxe;

        protected override Func<BlockType, bool> IsTargetBlock => block =>
            block is BlockType.Stone or BlockType.Cobblestone or BlockType.MossStone;
    }

    internal sealed class CookJob : IVillagerJob
    {
        private BlockType? _activeIngredient;

        public void Tick(Villager villager, float deltaTime, VoxelWorld world, VillageContext context)
        {
            if (context.Village == null)
            {
                villager.AssignJob(JobType.Idle, null, null);
                return;
            }

            // Go to nearest kitchen building if it exists, otherwise village center
            var kitchen = context.Village.GetNearestBuilding(BuildingKind.Kitchen, villager.Position);
            var target = kitchen != null ? context.Village.GetBuildingWorkPosition(kitchen) : context.VillageCenter;

            villager.SetJobTarget(target);
            if (villager.AiPhase == VillagerAiPhase.PathTo)
            {
                if (!(context.CreativeMode && context.IsTestMode) && VillagerMovementHelper.TryMoveToward(villager, deltaTime, world, target))
                {
                    return;
                }

                villager.SetAiPhase(VillagerAiPhase.Working);
            }

            if (villager.AiPhase != VillagerAiPhase.Working)
            {
                return;
            }

            if (!_activeIngredient.HasValue)
            {
                // Try to consume raw ingredient from storage
                if (context.Village.Storage.TryConsumeBlock(BlockType.Carrot, 1))
                {
                    _activeIngredient = BlockType.Carrot;
                }
                else if (context.Village.Storage.TryConsumeBlock(BlockType.Wheat, 1))
                {
                    _activeIngredient = BlockType.Wheat;
                }
                else if (context.CreativeMode)
                {
                    _activeIngredient = BlockType.Carrot;
                }
                else
                {
                    // No ingredients to cook, wait/idle
                    villager.AssignJob(JobType.Idle, null, null);
                    return;
                }
            }

            villager.WorkTimer += deltaTime * villager.WorkSpeedMultiplier;
            if (villager.WorkTimer < Villager.WorkInterval * 2f)
            {
                return;
            }

            villager.WorkTimer = 0f;

            // Cooking raw ingredients produces higher value FoodStock than raw items directly
            float outputFoodValue = _activeIngredient == BlockType.Carrot ? 2.5f : 2.0f;
            context.Village.AddFarmFood(outputFoodValue);
            _activeIngredient = null;
        }
    }
}

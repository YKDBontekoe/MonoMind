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

            var target = villager.JobTarget ?? FindNearestAnimal(context, villager.Position);
            if (!target.HasValue)
            {
                villager.AssignJob(JobType.Idle, null, null);
                return;
            }

            villager.SetJobTarget(target);
            if (villager.AiPhase == VillagerAiPhase.PathTo)
            {
                if (VillagerMovementHelper.TryMoveToward(villager, deltaTime, world, target.Value))
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
            context.Village.AddFarmFood(1.5f);
            villager.SetAiPhase(VillagerAiPhase.PathTo);
        }

        private static Vector3? FindNearestAnimal(VillageContext context, System.Numerics.Vector3 from)
        {
            return context.VillageCenter + new System.Numerics.Vector3(2f, 0f, 2f);
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
        public void Tick(Villager villager, float deltaTime, VoxelWorld world, VillageContext context)
        {
            if (context.Village == null)
            {
                villager.AssignJob(JobType.Idle, null, null);
                return;
            }

            var target = villager.JobTarget ?? context.VillageCenter;
            if (villager.AiPhase == VillagerAiPhase.PathTo)
            {
                if (VillagerMovementHelper.TryMoveToward(villager, deltaTime, world, target))
                {
                    return;
                }

                villager.SetAiPhase(VillagerAiPhase.Working);
            }

            if (villager.AiPhase != VillagerAiPhase.Working)
            {
                return;
            }

            villager.WorkTimer += deltaTime * villager.WorkSpeedMultiplier;
            if (villager.WorkTimer < Villager.WorkInterval * 2f)
            {
                return;
            }

            villager.WorkTimer = 0f;
            if (context.Village.FoodStock > 0f)
            {
                context.Village.AddFarmFood(0.75f);
            }
            else
            {
                villager.AssignJob(JobType.Idle, null, null);
            }
        }
    }
}

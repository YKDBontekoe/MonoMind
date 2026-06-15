using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Village.Jobs
{
    internal sealed class CraftJob : IVillagerJob
    {
        public void Tick(Villager villager, float deltaTime, VoxelWorld world, VillageContext context)
        {
            var target = villager.JobTarget ?? context.VillageCenter;
            if (villager.AiPhase == VillagerAiPhase.PathTo)
            {
                if (!context.CreativeMode && VillagerMovementHelper.TryMoveToward(villager, deltaTime, world, target))
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
            if (villager.Role == VillagerRole.Farmer)
            {
                GrantFarmYield(villager, context, 0.5f);
                villager.Skills.AddXp(VillagerSkill.Farming, 1f);
            }
            else if (!VillageWorkshopCrafting.TrySmithWork(
                         context.Storage,
                         context.CreativeMode,
                         itemName => context.Events?.OnWorkshopCraft(itemName),
                         toolName => context.Events?.OnWorkshopRepair(toolName)))
            {
                villager.AssignJob(JobType.Idle, null, null);
                return;
            }

            villager.AssignJob(JobType.Idle, null, null);
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
    }
}

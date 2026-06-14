using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.World;

namespace Autonocraft.Village.Jobs
{
    internal sealed class BuildJob : IVillagerJob
    {
        public void Tick(Villager villager, float deltaTime, VoxelWorld world, VillageContext context)
        {
            if (!villager.AssignedBuildingSiteId.HasValue ||
                !context.TryGetBuildingSite(villager.AssignedBuildingSiteId.Value, out var site))
            {
                villager.AssignJob(JobType.Idle, null, null);
                return;
            }

            if (!site.TryGetNextBlock(out var nextBlock))
            {
                villager.AssignJob(JobType.Idle, null, null);
                return;
            }

            var targetPos = new Vector3(
                site.AnchorX + nextBlock.Dx + 0.5f,
                site.AnchorY + nextBlock.Dy,
                site.AnchorZ + nextBlock.Dz + 0.5f);
            if (villager.AiPhase == VillagerAiPhase.PathTo)
            {
                if (VillagerMovementHelper.TryMoveToward(villager, deltaTime, world, targetPos))
                {
                    return;
                }

                villager.SetAiPhase(VillagerAiPhase.Working);
            }

            villager.WorkTimer += deltaTime * villager.WorkSpeedMultiplier;
            if (villager.WorkTimer >= Villager.WorkInterval * 0.5f)
            {
                villager.WorkTimer = 0f;
                site.TryPlaceNextBlock(world, context.Storage, Villager.Width, Villager.Height, villager.Position, context.CreativeMode);
                villager.SetAiPhase(VillagerAiPhase.PathTo);
            }
        }
    }
}

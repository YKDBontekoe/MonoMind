using Autonocraft.Entities;
using Autonocraft.World;

namespace Autonocraft.Village.Jobs
{
    internal sealed class IdleJob : IVillagerJob
    {
        public void Tick(Villager villager, float deltaTime, VoxelWorld world, VillageContext context)
        {
            if (villager.AiPhase == VillagerAiPhase.PathTo &&
                VillagerMovementHelper.TryMoveAlongPath(villager, deltaTime, world))
            {
                return;
            }

            VillagerMovementHelper.UpdateWander(
                villager,
                deltaTime,
                world,
                context.VillageRadius,
                context.VillageCenter,
                villager.JobRandom);
        }
    }
}
